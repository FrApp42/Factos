using Factos.Components;
using System.Text.Json;
using System.Text.RegularExpressions;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// A PDF layout designed in Paramètres › Modèles. Upstream ships fixed Blade templates
/// picked by `template_name`; here that name points at one of these instead.
public sealed class DocumentTemplate
{
    public const string Header = "header", Customer = "customer", Intro = "intro", Lines = "lines", Totals = "totals", Notes = "notes";

    public static readonly (string Key, string Label)[] AllBlocks =
    [
        (Header, "En-tête (société, numéro, dates)"), (Customer, "Client"), (Intro, "Texte d'introduction"),
        (Lines, "Lignes / détail du paiement"), (Totals, "Totaux"), (Notes, "Notes du document"),
    ];

    public string Name { get; set; } = "Classique";
    public string Accent { get; set; } = "#5851d8";
    public float FontSize { get; set; } = 10;
    public bool LogoRight { get; set; }
    /// The logo usually spells the name already.
    public bool HideCompanyNameWithLogo { get; set; }
    public bool FilledTableHeader { get; set; }
    /// Enabled blocks, in print order.
    public List<string> Blocks { get; set; } = [Header, Customer, Lines, Totals, Notes];
    public bool ShowDescription { get; set; } = true;
    public bool ShowUnit { get; set; } = true;
    /// Free texts; {CUSTOMER_NAME}-style placeholders are replaced like in e-mails.
    public string? IntroText { get; set; }
    public string? FooterText { get; set; }
    public bool PageNumbers { get; set; } = true;
}

/// The company's templates, stored as JSON in the `document_templates` setting.
/// The first one is the default.
public static partial class DocumentTemplates
{
    public const string Option = "document_templates";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<DocumentTemplate> Parse(string? json)
    {
        try
        {
            if (json is not null && JsonSerializer.Deserialize<List<DocumentTemplate>>(json, Json) is { Count: > 0 } list)
                return list;
        }
        catch (JsonException) { }
        return [new()];
    }

    /// The document's template, or the default when its name is unknown — upstream's
    /// "invoice1"/"estimate1" and templates renamed or deleted since.
    public static DocumentTemplate Resolve(IReadOnlyDictionary<string, string> settings, string? name)
    {
        var all = Parse(settings.GetValueOrDefault(Option));
        return all.FirstOrDefault(t => t.Name == name) ?? all[0];
    }

    public static async Task<List<DocumentTemplate>> LoadAsync(AppDbFactory dbs, int companyId)
    {
        await using var db = dbs.Create();
        return Parse(await db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == Option).Select(s => s.Value).FirstOrDefaultAsync());
    }

    public static async Task SaveAsync(AppDbFactory dbs, int companyId, List<DocumentTemplate> templates)
    {
        if (Validate(templates) is { } error)
            throw new SalesException(error);
        foreach (var t in templates)
            t.Name = t.Name.Trim();

        await using var db = dbs.Create();
        var now = Data.Timestamp.Now();
        var row = await db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Option == Option);
        if (row is null)
            db.CompanySettings.Add(row = new CompanySetting { CompanyId = companyId, Option = Option, CreatedAt = now });
        row.Value = JsonSerializer.Serialize(templates, Json);
        row.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    public static string? Validate(List<DocumentTemplate> templates)
    {
        if (templates.Count == 0)
            return "Il faut au moins un modèle.";
        foreach (var t in templates)
        {
            if (string.IsNullOrWhiteSpace(t.Name))
                return "Chaque modèle doit avoir un nom.";
            if (!HexColor().IsMatch(t.Accent))
                return Loc.F("« {0} » : couleur invalide (format #rrggbb).", t.Name);
            if (t.FontSize is < 7 or > 14)
                return Loc.F("« {0} » : taille de texte entre 7 et 14.", t.Name);
            if (t.Blocks.Except(DocumentTemplate.AllBlocks.Select(b => b.Key)).Any())
                return Loc.F("« {0} » : bloc inconnu.", t.Name);
        }
        var duplicate = templates.GroupBy(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return duplicate is null ? null : Loc.F("Deux modèles s'appellent « {0} ».", duplicate.Key);
    }

    /// What the designer previews: the company's latest invoice, or a made-up one.
    public static async Task<DocumentData> SampleAsync(AppDbFactory dbs, Sales sales, int companyId)
    {
        await using var db = dbs.Create();
        var latest = await db.Invoices.Where(i => i.CompanyId == companyId && i.Type != CreditNotes.Type)
            .OrderByDescending(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync();
        if (latest is { } id && await DocumentData.LoadAsync(dbs, sales, DocumentData.Invoice, id, companyId) is { } real)
            return real;

        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId);
        var settings = await Companies.SettingsAsync(db, companyId);
        var currency = int.TryParse(settings.GetValueOrDefault("currency"), out var cc) ? await db.Currencies.FindAsync(cc) : null;
        var address = await db.Addresses.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.CustomerId == null && a.UserId == null).OrderBy(a => a.Id).FirstOrDefaultAsync();
        var items = new List<DraftItem>
        {
            new() { Name = "Prestation de conseil", Description = "Atelier de cadrage", UnitName = "jour", Quantity = 2, Price = 45000 },
            new() { Name = "Licence annuelle", Quantity = 1, Price = 12000 },
        };
        var subTotal = items.Sum(i => i.Total);
        return new DocumentData
        {
            Kind = DocumentData.Invoice, Id = 0, Company = company, CompanyAddress = address, Currency = currency, Settings = settings,
            Customer = new Customer { Name = "Client exemple", Email = "client@example.com" },
            BillingAddress = new Address { AddressStreet1 = "1 rue de l'Exemple", Zip = "75001", City = "Paris" },
            Number = "INV-000042", Date = DateTime.Today, DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Notes = "Paiement à 30 jours.", Status = Sales.Draft, Items = items, SubTotal = subTotal, Total = subTotal, DueAmount = subTotal,
        };
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
