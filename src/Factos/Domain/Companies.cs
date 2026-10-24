using System.Security.Cryptography;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// Company creation as InvoiceShelf's CompanyService does it: membership, owner role,
/// default settings, payment methods and units.
public static class Companies
{
    public static async Task<Company> CreateAsync(ApplicationDbContext db, ApplicationUser owner, string name, int currencyId)
    {
        var now = Data.Timestamp.Now();
        var company = new Company
        {
            Name = name,
            OwnerId = owner.Id,
            Slug = await UniqueSlugAsync(db, name),
            UniqueHash = PublicToken(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.UserCompanies.Add(new UserCompany { UserId = owner.Id, CompanyId = company.Id, CreatedAt = now, UpdatedAt = now });
        db.Addresses.Add(new Address { CompanyId = company.Id, CreatedAt = now, UpdatedAt = now });

        var role = new Role { Name = "owner", Title = "Owner", Scope = company.Id, CreatedAt = now, UpdatedAt = now };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.AssignedRoles.Add(new AssignedRole { RoleId = role.Id, EntityId = owner.Id, EntityType = "user", Scope = company.Id });

        foreach (var (option, value) in DefaultSettings(currencyId))
            db.CompanySettings.Add(new CompanySetting { CompanyId = company.Id, Option = option, Value = value, CreatedAt = now, UpdatedAt = now });

        // EloquentBusinessDefaultsProvisioner
        foreach (var method in new[] { "Cash", "Check", "Credit Card", "Bank Transfer" })
            db.PaymentMethods.Add(new PaymentMethod { Name = method, CompanyId = company.Id, CreatedAt = now, UpdatedAt = now });
        foreach (var unit in new[] { "box", "cm", "dz", "ft", "g", "in", "kg", "km", "lb", "mg", "pc" })
            db.Units.Add(new Unit { Name = unit, CompanyId = company.Id, CreatedAt = now, UpdatedAt = now });

        await db.SaveChangesAsync();
        return company;
    }

    /// CompanyService::setupDefaultSettings(), minus the address formats (the PDF layout
    /// is fixed for now).
    // ponytail: time_zone stays upstream's literal default; nothing reads it yet.
    public static Dictionary<string, string> DefaultSettings(int currencyId) => new()
    {
        ["currency"] = currencyId.ToString(),
        ["time_zone"] = "Asia/Kolkata",
        ["language"] = "en",
        ["fiscal_year"] = "1-12",
        ["carbon_date_format"] = "Y/m/d",
        ["moment_date_format"] = "YYYY/MM/DD",
        ["carbon_time_format"] = "H:i",
        ["moment_time_format"] = "HH:mm",
        ["invoice_use_time"] = "NO",
        ["notify_invoice_viewed"] = "NO",
        ["notify_estimate_viewed"] = "NO",
        ["tax_per_item"] = "NO",
        ["discount_per_item"] = "NO",
        ["retrospective_edits"] = "allow",
        ["invoice_number_format"] = "{{SERIES:INV}}{{DELIMITER:-}}{{SEQUENCE:6}}",
        ["credit_note_number_format"] = "{{SERIES:CN}}{{DELIMITER:-}}{{SEQUENCE:6}}",
        ["estimate_number_format"] = "{{SERIES:EST}}{{DELIMITER:-}}{{SEQUENCE:6}}",
        ["payment_number_format"] = "{{SERIES:PAY}}{{DELIMITER:-}}{{SEQUENCE:6}}",
        ["estimate_set_expiry_date_automatically"] = "YES",
        ["estimate_expiry_date_days"] = "7",
        ["invoice_set_due_date_automatically"] = "YES",
        ["invoice_due_date_days"] = "7",
        ["bulk_exchange_rate_configured"] = "YES",
        ["estimate_convert_action"] = "no_action",
        ["invoice_mail_body"] = DefaultMailBody("invoice"),
        ["estimate_mail_body"] = DefaultMailBody("estimate"),
        ["payment_mail_body"] = DefaultMailBody("payment"),
        ["invoice_email_attachment"] = "NO",
        ["estimate_email_attachment"] = "NO",
        ["payment_email_attachment"] = "NO",
        ["automatically_expire_public_links"] = "YES",
        ["link_expiry_days"] = "7",
    };

    /// Upstream's INVOICE/ESTIMATE/PAYMENT_MAIL_BODY constants, verbatim.
    public static string DefaultMailBody(string kind) => kind switch
    {
        "invoice" => "You have received a new invoice from <b>{COMPANY_NAME}</b>.</br> Please download using the button below:",
        "estimate" => "You have received a new estimate from <b>{COMPANY_NAME}</b>.</br> Please download using the button below:",
        // Factos-only: upstream does not mail expenses.
        "expense" => "You have received an expense from <b>{COMPANY_NAME}</b>.</br> Please download using the button below:",
        _ => "Thank you for the payment.</b></br> Please download your payment receipt using the button below:",
    };

    public static async Task<Dictionary<string, string>> SettingsAsync(ApplicationDbContext db, int companyId)
        => await db.CompanySettings.Where(s => s.CompanyId == companyId)
            .ToDictionaryAsync(s => s.Option, s => s.Value);

    /// A random URL-safe token, like upstream's PublicToken::make().
    public static string PublicToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(30)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static async Task<string> UniqueSlugAsync(ApplicationDbContext db, string name)
    {
        var baseSlug = new string(name.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (baseSlug == "")
            baseSlug = "company";
        var slug = baseSlug;
        for (var i = 1; await db.Companies.AnyAsync(c => c.Slug == slug); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }
}
