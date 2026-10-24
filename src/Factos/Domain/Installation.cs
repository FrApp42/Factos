using Factos.Components;
using System.Net;
using System.Security.Claims;
using AnthoDingo.Setup;
using AnthoDingo.Update;
using Factos.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Factos.Domain;

/// First run, behind AnthoDingo.Setup's /setup wizard: schema, reference data, then the
/// first account, created `super admin` as upstream's installer does.
public sealed class Installer : ISetupInitializer
{
    public const string SuperAdmin = "super admin";

    public async Task InitializeDatabaseAsync(DbProvider provider, string connectionString, CancellationToken ct = default)
    {
        await using var db = DatabaseProviderExtensions.CreateContext(provider, connectionString);
        await db.Database.MigrateAsync(ct);
        await DatabaseInitializer.SeedReferenceDataAsync(db);
    }

    public async Task CreateAdminAsync(DbProvider provider, string connectionString, AdminAccount admin, CancellationToken ct = default)
    {
        await using var db = DatabaseProviderExtensions.CreateContext(provider, connectionString);
        var email = admin.UserName.Trim();
        // Identity's UpperInvariantLookupNormalizer, for both the user name and the e-mail.
        var normalized = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized, ct))
            throw new InvalidOperationException(Loc.T("Un compte existe déjà avec cet e-mail."));

        var now = Data.Timestamp.Now();
        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = normalized,
            Email = email,
            NormalizedEmail = normalized,
            Name = string.IsNullOrWhiteSpace(admin.DisplayName) ? "Admin" : admin.DisplayName.Trim(),
            Role = SuperAdmin,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, admin.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}

/// Last wizard step: the admin's company. Without one the UI has nothing to show.
public sealed class CompanySetupStep : ISetupExtraStep
{
    public string Id => "company";
    public string Label => Loc.T("Société");

    public async Task<string> RenderAsync(SetupExtraStepContext context, CancellationToken ct)
    {
        await using var db = DatabaseProviderExtensions.CreateContext(context.Provider, context.ConnectionString);
        var currencies = await db.Currencies.OrderBy(c => c.Code).Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        string Value(string key, string fallback) => context.Values?.TryGetValue(key, out var v) == true ? v : fallback;
        string Options(IEnumerable<(string Value, string Label)> items, string selected) => string.Concat(items.Select(i =>
            $"""<option value="{Enc(i.Value)}"{(i.Value == selected ? " selected" : "")}>{Enc(i.Label)}</option>"""));

        return $"""
            <form method="post" action="/setup">
              <input type="hidden" name="step" value="{Id}" />
              <input type="hidden" name="pendingState" value="{Enc(context.PendingStateToken)}" />
              <div class="mb-3"><label class="form-label w-100">Nom de la société
                <input type="text" class="form-control" name="name" value="{Enc(Value("name", ""))}" required /></label></div>
              <div class="mb-3"><label class="form-label w-100">Devise
                <select class="form-select" name="currency">{Options(currencies.Select(c => (c.Code, $"{c.Code} — {c.Name}")), Value("currency", "EUR"))}</select></label></div>
              <div class="mb-3"><label class="form-label w-100">Fuseau horaire
                <select class="form-select" name="time_zone">{Options(Cadence.TimeZones.Select(z => (z, z)), Value("time_zone", "Europe/Paris"))}</select></label></div>
              <button type="submit" class="btn btn-primary w-100 py-2">Créer la société et terminer</button>
            </form>
            """;
    }

    public async Task<SetupExtraStepResult> HandleAsync(SetupExtraStepContext context, IFormCollection form, CancellationToken ct)
    {
        var name = form["name"].ToString().Trim();
        var zone = form["time_zone"].ToString();
        var code = form["currency"].ToString();
        if (name.Length == 0)
            return SetupExtraStepResult.Failure(Loc.T("Le nom de la société est obligatoire."));
        if (!Cadence.TimeZones.Contains(zone))
            return SetupExtraStepResult.Failure("Fuseau horaire inconnu.");

        await using var db = DatabaseProviderExtensions.CreateContext(context.Provider, context.ConnectionString);
        var currency = await db.Currencies.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (currency is null)
            return SetupExtraStepResult.Failure(Loc.T("Devise inconnue."));

        // The account the previous step created; a resubmitted form must not add a second company.
        var admin = await db.Users.Where(u => u.Role == Installer.SuperAdmin).OrderByDescending(u => u.Id).FirstAsync(ct);
        if (await db.UserCompanies.AnyAsync(uc => uc.UserId == admin.Id, ct))
            return SetupExtraStepResult.Success();

        var company = await Companies.CreateAsync(db, admin, name, currency.Id);
        await db.CompanySettings.Where(s => s.CompanyId == company.Id && s.Option == "time_zone")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, zone), ct);
        return SetupExtraStepResult.Success();
    }

    private static string Enc(string value) => WebUtility.HtmlEncode(value);
}

/// What /update (AnthoDingo.Update) applies: EF Core's migrations, then the reference data.
public sealed class FactosMigrations(ApplicationDbContext db) : IMigrationManager
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        => (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public async Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await DatabaseInitializer.SeedReferenceDataAsync(db);
    }
}

/// Puts `users.role` in the cookie as a role claim, for the policy that guards /update.
public sealed class FactosClaimsFactory(UserManager<ApplicationUser> users, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, user.Role));
        return identity;
    }
}
