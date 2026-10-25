using System.Text.Json;
using Factos.Api.Auth;
using Factos.Api.Authorization;
using Factos.Api.Resources;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api.Controllers;

/// Routes behind InvoiceShelf's `auth:sanctum` + `company` (+ `bouncer`) middleware.
[ApiController]
[Authorize(AuthenticationSchemes = SanctumTokenDefaults.Scheme)]
[ServiceFilter(typeof(CompanyScopeFilter))]
[EnableRateLimiting(RateLimits.Api)]
public abstract class CompanyApiController : ControllerBase;

/// routes/api.php `ping` and Platform/Operations/routes/version.php.
[ApiController]
[EnableRateLimiting(RateLimits.Api)]
public class SystemController(ApplicationDbContext db) : ControllerBase
{
    /// The InvoiceShelf release whose API this build reproduces. Clients gate features on it.
    public const string CompatibleVersion = "3.0.0-alpha.11";

    // Clients probe this exact string to recognise an InvoiceShelf server.
    [HttpGet("api/ping")]
    public IActionResult Ping() => Ok(new Dictionary<string, object?> { ["success"] = "invoiceshelf-self-hosted" });

    [HttpGet("api/v1/app/version")]
    public async Task<IActionResult> Version()
    {
        // Upstream writes the default channel on first read; kept, since later reads see it.
        var channel = await db.Settings.FirstOrDefaultAsync(s => s.Option == "updater_channel");
        if (channel is null)
        {
            var now = Timestamp.Now();
            channel = new Setting { Option = "updater_channel", Value = "stable", CreatedAt = now, UpdatedAt = now };
            db.Settings.Add(channel);
            await db.SaveChangesAsync();
        }

        return Ok(new Dictionary<string, object?>
        {
            ["version"] = CompatibleVersion,
            ["channel"] = channel.Value,
            ["containerized"] = false,
        });
    }
}

/// Domains/Accounts AuthController: login and logout.
[ApiController]
[Route("api/v1/auth")]
public class AuthController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private const string Rejected = "The provided credentials are incorrect.";

    public record LoginInput(string? Username, string? Password, string? DeviceName, string? InvitationToken, string? TwoFactorCode);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> Login([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LoginInput? input)
    {
        var validator = new LaravelValidator()
            .Required("username", input?.Username)
            .Required("password", input?.Password)
            .Required("device_name", input?.DeviceName);
        if (validator.Failed)
            return validator.Result();

        // LOWER(email) on both sides: matches however the address was capitalised, on every provider.
        var username = input!.Username!.ToLowerInvariant();
        var staff = await db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == username);
        if (staff is null || !await users.CheckPasswordAsync(staff, input.Password!))
            return new LaravelValidator().Add("email", Rejected).Result();

        // Factos-only: an account with TOTP enabled also needs `two_factor_code` (a code or a
        // recovery code), otherwise the API would be a way around the second factor.
        if (staff.TwoFactorEnabled)
        {
            var code = (input.TwoFactorCode ?? "").Replace(" ", "");
            if (code.Length == 0)
                return new LaravelValidator().Add("two_factor_code", "The two factor code field is required.").Result();
            var valid = await users.VerifyTwoFactorTokenAsync(staff, users.Options.Tokens.AuthenticatorTokenProvider, code)
                || (await users.RedeemTwoFactorRecoveryCodeAsync(staff, code)).Succeeded;
            if (!valid)
                return new LaravelValidator().Add("two_factor_code", "The two factor code is incorrect.").Result();
        }

        // ponytail: upstream also redeems a pending `invitation_token` here; company
        // invitations are not modelled yet, and upstream ignores unknown tokens silently.

        var plaintext = PersonalAccessToken.NewPlaintext();
        var now = Timestamp.Now();
        var token = new PersonalAccessToken
        {
            TokenableId = staff.Id,
            Name = input.DeviceName!,
            TokenHash = PersonalAccessToken.Hash(plaintext),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PersonalAccessTokens.Add(token);
        await db.SaveChangesAsync();

        return Ok(new Dictionary<string, object?> { ["type"] = "Bearer", ["token"] = $"{token.Id}|{plaintext}" });
    }

    /// Revokes only the token that carried this request; other devices stay signed in.
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = SanctumTokenDefaults.Scheme)]
    [EnableRateLimiting(RateLimits.Api)]
    public async Task<IActionResult> Logout()
    {
        var tokenId = int.Parse(User.FindFirst(SanctumTokenHandler.TokenIdClaim)!.Value);
        await db.PersonalAccessTokens.Where(t => t.Id == tokenId).ExecuteDeleteAsync();
        return Ok(new Dictionary<string, object?> { ["success"] = true });
    }
}

/// Domains/Accounts routes/company.php (profile and devices) and management.php (companies).
public class ProfileController(ApplicationDbContext db, CompanyContext context, AccountResources resources)
    : CompanyApiController
{
    private const string TokenModel = @"Laravel\Sanctum\PersonalAccessToken";

    /// A bare boolean body: the SPA reads it as a liveness ping.
    [HttpGet("api/v1/auth/check")]
    public IActionResult Check() => Ok(true);

    [HttpGet("api/v1/auth/tokens")]
    public async Task<IActionResult> Tokens()
    {
        var tokens = await db.PersonalAccessTokens
            .Where(t => t.TokenableType == "user" && t.TokenableId == context.User.Id)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .ToListAsync();
        return Ok(new Dictionary<string, object?> { ["data"] = tokens.Select(resources.Token).ToList() });
    }

    /// Someone else's token id is answered 404, exactly like an id nobody holds.
    [HttpDelete("api/v1/auth/tokens/{id}")]
    public async Task<IActionResult> RevokeToken(string id)
    {
        var deleted = int.TryParse(id, out var tokenId) && await db.PersonalAccessTokens
            .Where(t => t.Id == tokenId && t.TokenableType == "user" && t.TokenableId == context.User.Id)
            .ExecuteDeleteAsync() > 0;
        return deleted ? NoContent() : LaravelResponse.ModelNotFound(TokenModel);
    }

    [HttpGet("api/v1/me")]
    public async Task<IActionResult> Me()
        => Ok(new Dictionary<string, object?> { ["data"] = await resources.UserAsync(context.User) });

    public record ProfileInput(string? Name, string? Email, string? Password);

    [HttpPut("api/v1/me")]
    public async Task<IActionResult> UpdateMe(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ProfileInput? input,
        [FromServices] UserManager<ApplicationUser> users)
    {
        var user = context.User;
        var email = input?.Email;
        var validator = new LaravelValidator()
            .Required("name", input?.Name)
            .Min("password", input?.Password, 8)
            .Required("email", email)
            .IdnEmail("email", email);
        if (!validator.Has("email") && !string.IsNullOrEmpty(email))
            validator.Unique("email", await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id));
        if (validator.Failed)
            return validator.Result();

        user.Name = input!.Name!;
        user.Email = email;
        user.UserName = email;
        // ponytail: `customFields` are written with the custom-fields domain.
        // Upstream answers 500 for an explicit null or empty password (its mutator takes
        // a non-null string); here that simply leaves the password unchanged.
        if (!string.IsNullOrEmpty(input.Password))
        {
            user.PasswordHash = users.PasswordHasher.HashPassword(user, input.Password);
            await users.UpdateSecurityStampAsync(user);
        }
        user.UpdatedAt = Timestamp.Now();
        await users.UpdateAsync(user);   // also refreshes the normalized email and user name

        return Ok(new Dictionary<string, object?> { ["data"] = await resources.UserAsync(user) });
    }

    [HttpGet("api/v1/me/settings")]
    public async Task<IActionResult> MySettings()
    {
        if (Settings.ValidateKeys(Request) is { } invalid)
            return invalid;
        var keys = LaravelInput.QueryArray(Request, "settings")!;
        var rows = await db.UserSettings
            .Where(s => s.UserId == context.User.Id && keys.Contains(s.Key))
            .OrderBy(s => s.Id)
            .Select(s => new KeyValuePair<string, string>(s.Key, s.Value))
            .ToListAsync();
        return Ok(Settings.Flatten(rows));
    }

    [HttpPut("api/v1/me/settings")]
    public async Task<IActionResult> UpdateMySettings([FromBody] JsonElement body)
    {
        if (Settings.ValidateWrite(body) is { } invalid)
            return invalid;

        var now = Timestamp.Now();
        foreach (var (key, value) in LaravelInput.Entries(body.GetProperty("settings")))
        {
            var row = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == context.User.Id && s.Key == key);
            if (row is null)
                db.UserSettings.Add(row = new UserSetting { UserId = context.User.Id, Key = key, CreatedAt = now });
            row.Value = LaravelInput.PhpString(value);
            row.UpdatedAt = now;
            await db.SaveChangesAsync();
        }
        return Ok(new Dictionary<string, object?> { ["success"] = true });
    }

    [HttpGet("api/v1/companies")]
    public async Task<IActionResult> Companies()
    {
        var ids = await context.UserCompanyIdsAsync(context.User.Id);
        var companies = await db.Companies.Where(c => ids.Contains(c.Id)).ToListAsync();
        var data = new List<Dictionary<string, object?>>();
        foreach (var id in ids)
            if (companies.FirstOrDefault(c => c.Id == id) is { } company)
                data.Add(await resources.CompanyAsync(company));
        return Ok(new Dictionary<string, object?> { ["data"] = data });
    }
}

/// Domains/Accounts CompanySettingsController.
public class CompanySettingsController(ApplicationDbContext db, CompanyContext context, Bouncer bouncer)
    : CompanyApiController
{
    private const string UserModel = @"App\Domains\Accounts\Models\User";

    /// Only the named options that exist and are member-visible; `[]` when none are.
    [HttpGet("api/v1/company/settings")]
    public async Task<IActionResult> Show()
    {
        if (Settings.ValidateKeys(Request) is { } invalid)
            return invalid;
        var keys = LaravelInput.QueryArray(Request, "settings")!
            .Where(k => k is not null && !Settings.IsPrivate(k)).ToList();
        var rows = await db.CompanySettings
            .Where(s => s.CompanyId == context.CompanyId && keys.Contains(s.Option))
            .OrderBy(s => s.Id)
            .Select(s => new KeyValuePair<string, string>(s.Option, s.Value))
            .ToListAsync();
        return Ok(Settings.Flatten(rows));
    }

    [HttpPost("api/v1/company/settings")]
    public async Task<IActionResult> Update([FromBody] JsonElement body)
    {
        if (Settings.ValidateWrite(body) is { } invalid)
            return invalid;
        var company = await db.Companies.FindAsync(context.CompanyId);
        if (company is null || !await bouncer.AllowsAsync("manage company", BouncerModel.Of(company), () => context.User.Id == company.OwnerId))
            return LaravelResponse.Forbidden();

        var submitted = body.GetProperty("settings");
        // A refused currency move is a plain 200 with success:false, not an error.
        // The comparison is PHP's strict !==: a number never equals the stored string.
        if (submitted.ValueKind == JsonValueKind.Object && submitted.TryGetProperty("currency", out var currency))
        {
            var stored = await Setting(company.Id, "currency");
            var same = currency.ValueKind == JsonValueKind.Null
                ? stored is null
                : currency.ValueKind == JsonValueKind.String && stored == currency.GetString();
            if (!same && await HasTransactionsAsync(company.Id))
                return Ok(new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["message"] = "Cannot update company currency after transactions are created.",
                });
        }

        var now = Timestamp.Now();
        foreach (var (key, value) in LaravelInput.Entries(submitted))
        {
            var row = await db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == company.Id && s.Option == key);
            if (row is null)
                db.CompanySettings.Add(row = new CompanySetting { CompanyId = company.Id, Option = key, CreatedAt = now });
            row.Value = LaravelInput.PhpString(value);
            row.UpdatedAt = now;
            await db.SaveChangesAsync();
        }
        return Ok(new Dictionary<string, object?> { ["success"] = true });
    }

    [HttpGet("api/v1/company/has-transactions")]
    public async Task<IActionResult> HasTransactions()
    {
        var company = await db.Companies.FindAsync(context.CompanyId);
        if (company is null || !await bouncer.AllowsAsync("manage company", BouncerModel.Of(company), () => context.User.Id == company.OwnerId))
            return LaravelResponse.Forbidden();
        return Ok(new Dictionary<string, object?> { ["has_transactions"] = await HasTransactionsAsync(company.Id) });
    }

    /// Hands the company to an existing member, whose roles here become `owner` alone.
    /// The outgoing owner keeps their role assignments.
    [HttpPost("api/v1/transfer/ownership/{userId}")]
    public async Task<IActionResult> TransferOwnership(string userId)
    {
        var target = int.TryParse(userId, out var id) ? await db.Users.FindAsync(id) : null;
        if (target is null)
            return LaravelResponse.ModelNotFound(UserModel, userId);

        var company = await db.Companies.FindAsync(context.CompanyId);
        if (company is null || !await bouncer.AllowsAsync("transfer company ownership", BouncerModel.Of(company), () => context.User.Id == company.OwnerId))
            return LaravelResponse.Forbidden();

        if (!(await context.UserCompanyIdsAsync(target.Id)).Contains(company.Id))
            return Ok(new Dictionary<string, object?>
            {
                ["success"] = false,
                ["message"] = "User does not belong to this company.",
            });

        company.OwnerId = target.Id;
        company.UpdatedAt = Timestamp.Now();
        await db.SaveChangesAsync();
        await bouncer.SyncRolesAsync(target.Id, "owner");

        return Ok(new Dictionary<string, object?> { ["success"] = true });
    }

    private Task<string?> Setting(int companyId, string option) => db.CompanySettings
        .Where(s => s.CompanyId == companyId && s.Option == option)
        .Select(s => s.Value)
        .FirstOrDefaultAsync();

    /// Company::hasTransactions().
    private async Task<bool> HasTransactionsAsync(int companyId) =>
        await db.Customers.AnyAsync(x => x.CompanyId == companyId)
        || await db.Items.AnyAsync(x => x.CompanyId == companyId)
        || await db.Invoices.AnyAsync(x => x.CompanyId == companyId)
        || await db.Estimates.AnyAsync(x => x.CompanyId == companyId)
        || await db.Expenses.AnyAsync(x => x.CompanyId == companyId)
        || await db.Payments.AnyAsync(x => x.CompanyId == companyId)
        || await db.RecurringInvoices.AnyAsync(x => x.CompanyId == companyId)
        || await db.Suppliers.AnyAsync(x => x.CompanyId == companyId)
        || await db.Bills.AnyAsync(x => x.CompanyId == companyId)
        || await db.SupplierCredits.AnyAsync(x => x.CompanyId == companyId)
        || await db.SupplierPayments.AnyAsync(x => x.CompanyId == companyId)
        || await db.SupplierRefunds.AnyAsync(x => x.CompanyId == companyId);
}

/// Shared by user and company settings: GetSettingsRequest / UpdateSettingsRequest
/// and MemberVisibleSettings.
internal static class Settings
{
    // MailConfigurationService::getCompanySettingKeys(): base fields plus every driver's.
    private static readonly HashSet<string> MailKeys = new[]
    {
        "mail_driver", "from_name", "from_mail",
        "mail_host", "mail_port", "mail_username", "mail_password", "mail_encryption",
        "mail_scheme", "mail_url", "mail_timeout", "mail_local_domain",
        "mail_ses_key", "mail_ses_secret", "mail_ses_region",
        "mail_mailgun_domain", "mail_mailgun_secret", "mail_mailgun_endpoint", "mail_mailgun_scheme",
        "mail_postmark_token", "mail_postmark_message_stream_id",
    }.Select(f => "company_" + f).Append("use_custom_mail_config").ToHashSet();

    /// Settings with their own endpoints: module settings and the mail transport.
    public static bool IsPrivate(string key) => key.StartsWith("module.") || MailKeys.Contains(key);

    /// mapWithKeys(): a JSON object, or `[]` when empty (an empty PHP collection).
    public static object Flatten(List<KeyValuePair<string, string>> rows)
    {
        if (rows.Count == 0)
            return Array.Empty<object>();
        var map = new Dictionary<string, string>();
        foreach (var (key, value) in rows)
            map[key] = value;
        return map;
    }

    /// GetSettingsRequest: `settings` required, each entry required.
    public static IActionResult? ValidateKeys(HttpRequest request)
    {
        var keys = LaravelInput.QueryArray(request, "settings");
        var validator = new LaravelValidator();
        if (keys is null || keys.Count == 0)
            validator.Required("settings", null);
        else
            for (var i = 0; i < keys.Count; i++)
                validator.Required($"settings.{i}", keys[i]);
        return validator.Failed ? validator.Result() : null;
    }

    /// UpdateSettingsRequest: bail, required, array, then no private key.
    public static IActionResult? ValidateWrite(JsonElement body)
    {
        var validator = new LaravelValidator();
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("settings", out var settings)
            || settings.ValueKind == JsonValueKind.Null
            || (settings.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(settings.GetString()))
            || (settings.ValueKind is JsonValueKind.Object or JsonValueKind.Array && !LaravelInput.Entries(settings).Any()))
            return validator.Required("settings", null).Result();
        if (settings.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            return validator.Add("settings", "The settings field must be an array.").Result();

        var refused = LaravelInput.Entries(settings).Select(e => e.Key).Where(IsPrivate).ToList();
        if (refused.Count > 0)
            return validator.Add("settings",
                $"These settings have their own page and cannot be changed here: {string.Join(", ", refused)}.").Result();
        return null;
    }
}
