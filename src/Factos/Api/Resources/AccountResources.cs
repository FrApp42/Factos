using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api.Resources;

/// Ports of InvoiceShelf's JsonResource classes for the Accounts domain. Each method
/// mirrors one toArray(): same keys, same order. Keys behind `$this->when()` are left
/// out entirely when the condition fails, never sent as null.
///
/// Uncast boolean columns are sent as JSON booleans; upstream sends whatever the driver
/// returns (0/1 on MySQL and SQLite, true/false on PostgreSQL).
public class AccountResources(ApplicationDbContext db, CompanyContext context, IHttpContextAccessor http)
{
    // Users currently being rendered: User -> companies -> address -> user can cycle back.
    private readonly HashSet<int> _renderingUsers = [];

    // UserResource
    public async Task<Dictionary<string, object?>> UserAsync(ApplicationUser user)
    {
        _renderingUsers.Add(user.Id);
        try { return await RenderUserAsync(user); }
        finally { _renderingUsers.Remove(user.Id); }
    }

    private async Task<Dictionary<string, object?>> RenderUserAsync(ApplicationUser user)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = user.Id,
            ["name"] = user.Name,
            ["email"] = user.Email,
            ["phone"] = user.Phone,
            ["role"] = user.Role,
            ["contact_name"] = user.ContactName,
            ["company_name"] = user.CompanyName,
            ["website"] = user.Website,
            ["enable_portal"] = user.EnablePortal,
            ["currency_id"] = user.CurrencyId,
            ["facebook_id"] = user.FacebookId,
            ["google_id"] = user.GoogleId,
            ["github_id"] = user.GithubId,
            ["created_at"] = user.CreatedAt,
            ["updated_at"] = user.UpdatedAt,
            // ponytail: avatars live in the media table, not modelled yet; upstream sends 0 when there is none.
            ["avatar"] = 0,
            ["is_owner"] = await IsOwnerAsync(user),
            ["is_super_admin"] = user.Role == "super admin",
            ["roles"] = await RolesAsync(user),
            ["formatted_created_at"] = PhpDate.Format(user.CreatedAt ?? Timestamp.Now(), await DateFormatAsync(user)),
        };

        if (user.CurrencyId is { } currencyId && await db.Currencies.FindAsync(currencyId) is { } currency)
            json["currency"] = Currency(currency);

        var companyIds = await context.UserCompanyIdsAsync(user.Id);
        if (companyIds.Count > 0)
        {
            var companies = await db.Companies.Where(c => companyIds.Contains(c.Id)).ToListAsync();
            var list = new List<Dictionary<string, object?>>();
            foreach (var id in companyIds)
                if (companies.FirstOrDefault(c => c.Id == id) is { } company)
                    list.Add(await CompanyAsync(company));
            json["companies"] = list;
        }

        // ponytail: `fields` (custom field values) arrive with the custom-fields domain;
        // nothing can create a user's field values before then, so omitting it is exact.
        return json;
    }

    // CompanyResource
    public async Task<Dictionary<string, object?>> CompanyAsync(Company company)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = company.Id,
            ["name"] = company.Name,
            ["vat_id"] = company.VatId,
            ["tax_id"] = company.TaxId,
            // Upstream: the media file's full URL, null without a logo. logo_path is a disk
            // path on upstream's system disks; here the same URL.
            ["logo"] = LogoUrl(company),
            ["logo_path"] = LogoUrl(company),
            ["unique_hash"] = company.UniqueHash,
            ["owner_id"] = company.OwnerId,
            ["slug"] = company.Slug,
            ["created_at"] = company.CreatedAt,
            ["updated_at"] = company.UpdatedAt,
        };

        // Company::address() is a bare hasOne on company_id, so upstream returns the first
        // address row carrying the company id, customer addresses included. Kept as is.
        var address = await db.Addresses.Where(a => a.CompanyId == company.Id).OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (address is not null)
            json["address"] = await AddressAsync(address);

        // `owner` is only sent when the relation was eager-loaded; no endpoint here loads it.
        json["user_role"] = await db.AssignedRoles
            .Where(ar => ar.EntityType == "user" && ar.EntityId == context.User.Id && ar.Scope == company.Id)
            .Join(db.Roles, ar => ar.RoleId, r => r.Id, (ar, r) => r.Title)
            .FirstOrDefaultAsync();

        return json;
    }

    private string? LogoUrl(Company company)
    {
        if (string.IsNullOrEmpty(company.Logo) || http.HttpContext?.Request is not { } request)
            return null;
        return $"{request.Scheme}://{request.Host}{request.PathBase}/logos/{company.UniqueHash}";
    }

    // CurrencyResource
    public static Dictionary<string, object?> Currency(Currency currency) => new()
    {
        ["id"] = currency.Id,
        ["name"] = currency.Name,
        ["code"] = currency.Code,
        ["symbol"] = currency.Symbol,
        ["precision"] = currency.Precision,
        ["thousand_separator"] = currency.ThousandSeparator,
        ["decimal_separator"] = currency.DecimalSeparator,
        ["swap_currency_symbol"] = currency.SwapCurrencySymbol,
        // Not a column: upstream reads an attribute that only exists on some query results.
        ["exchange_rate"] = null,
    };

    // AddressResource
    public async Task<Dictionary<string, object?>> AddressAsync(Address address)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = address.Id,
            ["name"] = address.Name,
            ["address_street_1"] = address.AddressStreet1,
            ["address_street_2"] = address.AddressStreet2,
            ["city"] = address.City,
            ["state"] = address.State,
            ["country_id"] = address.CountryId,
            ["zip"] = address.Zip,
            ["phone"] = address.Phone,
            ["fax"] = address.Fax,
            ["type"] = address.Type,
            ["user_id"] = address.UserId,
            ["company_id"] = address.CompanyId,
            ["customer_id"] = address.CustomerId,
        };

        if (address.CountryId is { } countryId && await db.Countries.FindAsync(countryId) is { } country)
            json["country"] = Country(country);
        // Upstream recurses without end (and dies) when a company address points back at a
        // user already being rendered; Factos stops the cycle by leaving `user` out there.
        if (address.UserId is { } userId && !_renderingUsers.Contains(userId)
            && await db.Users.FindAsync(userId) is { } user)
            json["user"] = await UserAsync(user);

        return json;
    }

    // CountryResource
    public static Dictionary<string, object?> Country(Country country) => new()
    {
        ["id"] = country.Id,
        ["code"] = country.Code,
        ["name"] = country.Name,
        // Upstream reads `phone_code`, but the column is `phonecode`: always null. Kept as is.
        ["phone_code"] = null,
    };

    // PersonalAccessTokenResource
    public Dictionary<string, object?> Token(PersonalAccessToken token) => new()
    {
        ["id"] = token.Id,
        ["name"] = token.Name,
        ["last_used_at"] = token.LastUsedAt,
        ["created_at"] = token.CreatedAt,
        ["current"] = token.Id == context.TokenId,
    };

    /// Bouncer's `$user->roles`: assigned roles in the current scope (or unscoped), each
    /// serialized as the Role model with its morph pivot.
    private async Task<List<Dictionary<string, object?>>> RolesAsync(ApplicationUser user)
    {
        var scope = context.BouncerScope;
        var rows = await db.AssignedRoles
            .Where(ar => ar.EntityType == "user" && ar.EntityId == user.Id)
            .Where(ar => ar.Scope == null || ar.Scope == scope)
            .Join(db.Roles, ar => ar.RoleId, r => r.Id, (ar, r) => new { ar, r })
            .OrderBy(x => x.ar.Id)
            .ToListAsync();

        return rows.Select(x => new Dictionary<string, object?>
        {
            ["id"] = x.r.Id,
            ["name"] = x.r.Name,
            ["title"] = x.r.Title,
            ["level"] = x.r.Level,
            ["scope"] = x.r.Scope,
            ["created_at"] = x.r.CreatedAt,
            ["updated_at"] = x.r.UpdatedAt,
            ["pivot"] = new Dictionary<string, object?>
            {
                ["entity_id"] = x.ar.EntityId,
                ["role_id"] = x.ar.RoleId,
                ["entity_type"] = x.ar.EntityType,
                ["scope"] = x.ar.Scope,
            },
        }).ToList();
    }

    /// User::isOwner(): owner of the company the (rewritten) header names.
    private async Task<bool> IsOwnerAsync(ApplicationUser user)
    {
        if (context.CompanyId is not { } companyId)
            return false;
        var company = await db.Companies.FindAsync(companyId);
        return company is not null && company.OwnerId == user.Id;
    }

    /// User::contextDateFormat(): the acting company's `carbon_date_format`, falling back
    /// to the user's first company, then to "Y-m-d" for a user with no company.
    private async Task<string> DateFormatAsync(ApplicationUser user)
    {
        var scope = context.CompanyId;
        var configured = scope is not null && await db.CompanySettings.AnyAsync(s => s.CompanyId == scope);
        if (!configured)
        {
            var companies = await context.UserCompanyIdsAsync(user.Id);
            if (companies.Count == 0)
                return "Y-m-d";
            scope = companies[0];
        }

        // A missing setting formats with null upstream, which PHP renders as "".
        return await db.CompanySettings
            .Where(s => s.CompanyId == scope && s.Option == "carbon_date_format")
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? "";
    }
}
