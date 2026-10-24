using Factos.Components;
using System.Net;
using System.Text.RegularExpressions;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// Everything needed to show, print or mail one invoice, estimate, payment receipt or expense —
/// the counterpart of upstream's getPdfData() / GeneratesPdf. One loader feeds the admin
/// view, the public page, the PDF and the e-mail.
public sealed class DocumentData
{
    public const string Invoice = "invoice", Estimate = "estimate", Payment = "payment", Expense = "expense";

    public required string Kind { get; init; }
    public required long Id { get; init; }
    public required Company Company { get; init; }
    public Address? CompanyAddress { get; init; }
    public string? CompanyCountry { get; init; }
    public required Customer Customer { get; init; }
    public Address? BillingAddress { get; init; }
    public string? BillingCountry { get; init; }
    public Currency? Currency { get; init; }
    public required Dictionary<string, string> Settings { get; init; }

    public required string Number { get; init; }
    public required DateTime Date { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public required string Status { get; init; }
    public string? PaidStatus { get; init; }
    public string? UniqueHash { get; init; }
    /// Name of the DocumentTemplate to print with (`template_name`).
    public string? TemplateName { get; init; }

    // Invoices and estimates.
    public List<DraftItem> Items { get; init; } = [];
    /// Document taxes, or per-item taxes collapsed by tax type as getPdfData() does.
    public List<DraftTax> Taxes { get; init; } = [];
    public bool TaxPerItem { get; init; }
    public bool DiscountPerItem { get; init; }
    public bool TaxIncluded { get; init; }
    public long SubTotal { get; init; }
    public long DiscountVal { get; init; }
    public long Total { get; init; }
    public long DueAmount { get; init; }
    public bool Sent { get; init; }

    // Credit notes.
    public bool IsCreditNote { get; init; }
    public int? RelatedInvoiceId { get; init; }
    public string? RelatedNumber { get; init; }
    public string? CreditReason { get; init; }

    // Payments.
    public long Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public List<(string Number, long Amount)> PaidInvoices { get; init; } = [];

    // Expenses (Factos-only: upstream does not mail them).
    public string? Category { get; init; }

    /// Custom fields marked `placement = document`, printed beside the number and dates.
    public List<(string Label, string Value)> PrintedFields { get; private set; } = [];
    /// `{CUSTOM_…}` tokens from the document's and the customer's answers.
    public Dictionary<string, string?> CustomPlaceholders { get; private set; } = [];

    /// Invoices and estimates have item lines and totals; receipts and expenses one amount.
    public bool HasLines => Kind is Invoice or Estimate;

    public string Title => Kind switch { Invoice => IsCreditNote ? Loc.T("Note de crédit") : Loc.T("Facture"), Estimate => Loc.T("Devis"), Expense => Loc.T("Dépense"), _ => Loc.T("Reçu de paiement") };
    public string FileName => $"{Number}.pdf";

    public string Money(long cents) => Domain.Money.Format(cents, Currency);
    public string FormatDate(DateTime? value) => value is { } v ? Api.PhpDate.Format(v, Settings.GetValueOrDefault("carbon_date_format", "Y/m/d")) : "";
    public string FormatDate(DateOnly? value) => FormatDate(value?.ToDateTime(TimeOnly.MinValue));

    /// Address lines in print order, empty ones dropped.
    public static IEnumerable<string> Lines(Address? a, string? country) => a is null
        ? []
        : new[] { a.AddressStreet1, a.AddressStreet2, string.Join(" ", new[] { a.Zip, a.City }.Where(x => !string.IsNullOrEmpty(x))), a.State, country, a.Phone }
            .Where(l => !string.IsNullOrWhiteSpace(l)).OfType<string>();

    // ---- placeholders (GeneratesPdf::getFieldsArray + each model's getExtraFields) ---------

    public Dictionary<string, string?> Placeholders()
    {
        var fields = new Dictionary<string, string?>();
        // Shipping addresses are not captured yet: their tokens resolve to nothing.
        foreach (var (prefix, address, country) in new[] { ("BILLING", BillingAddress, BillingCountry), ("SHIPPING", (Address?)null, (string?)null) })
        {
            fields[$"{{{prefix}_ADDRESS_NAME}}"] = address?.Name;
            fields[$"{{{prefix}_COUNTRY}}"] = country;
            fields[$"{{{prefix}_STATE}}"] = address?.State;
            fields[$"{{{prefix}_CITY}}"] = address?.City;
            fields[$"{{{prefix}_ADDRESS_STREET_1}}"] = address?.AddressStreet1;
            fields[$"{{{prefix}_ADDRESS_STREET_2}}"] = address?.AddressStreet2;
            fields[$"{{{prefix}_PHONE}}"] = address?.Phone;
            fields[$"{{{prefix}_ZIP_CODE}}"] = address?.Zip;
        }
        fields["{COMPANY_NAME}"] = Company.Name;
        fields["{COMPANY_COUNTRY}"] = CompanyCountry;
        fields["{COMPANY_STATE}"] = CompanyAddress?.State;
        fields["{COMPANY_CITY}"] = CompanyAddress?.City;
        fields["{COMPANY_ADDRESS_STREET_1}"] = CompanyAddress?.AddressStreet1;
        fields["{COMPANY_ADDRESS_STREET_2}"] = CompanyAddress?.AddressStreet2;
        fields["{COMPANY_PHONE}"] = CompanyAddress?.Phone;
        fields["{COMPANY_ZIP_CODE}"] = CompanyAddress?.Zip;
        fields["{COMPANY_VAT}"] = Company.VatId;
        fields["{COMPANY_TAX}"] = Company.TaxId;
        fields["{CONTACT_DISPLAY_NAME}"] = Customer.Name;
        fields["{PRIMARY_CONTACT_NAME}"] = Customer.ContactName;
        fields["{CONTACT_EMAIL}"] = Customer.Email;
        fields["{CONTACT_PHONE}"] = Customer.Phone;
        fields["{CONTACT_WEBSITE}"] = Customer.Website;
        fields["{CONTACT_TAX_ID}"] = "Tax-ID: " + Customer.TaxId;

        switch (Kind)
        {
            case Invoice:
                fields["{INVOICE_DATE}"] = FormatDate(Date);
                fields["{INVOICE_DUE_DATE}"] = FormatDate(DueDate);
                fields["{INVOICE_NUMBER}"] = Number;
                fields["{INVOICE_REF_NUMBER}"] = Reference;
                break;
            case Estimate:
                fields["{ESTIMATE_DATE}"] = FormatDate(Date);
                fields["{ESTIMATE_EXPIRY_DATE}"] = FormatDate(DueDate);
                fields["{ESTIMATE_NUMBER}"] = Number;
                fields["{ESTIMATE_REF_NUMBER}"] = Reference;
                break;
            case Expense:
                fields["{EXPENSE_DATE}"] = FormatDate(Date);
                fields["{EXPENSE_NUMBER}"] = Number;
                fields["{EXPENSE_AMOUNT}"] = Money(Amount);
                fields["{EXPENSE_CATEGORY}"] = Category;
                fields["{PAYMENT_MODE}"] = PaymentMethod;
                break;
            default:
                fields["{PAYMENT_DATE}"] = FormatDate(Date);
                fields["{PAYMENT_MODE}"] = PaymentMethod;
                fields["{PAYMENT_NUMBER}"] = Number;
                fields["{PAYMENT_AMOUNT}"] = Money(Amount);
                break;
        }
        foreach (var (token, value) in CustomPlaceholders)
            fields[token] = value;
        return fields;
    }

    /// getEmailString(): placeholders replaced (HTML-escaped unless for the PDF), unknown tokens removed.
    public string Render(string template, bool html = true)
    {
        var text = template;
        foreach (var (token, value) in Placeholders())
            text = text.Replace(token, html ? WebUtility.HtmlEncode(value ?? "") : value ?? "");
        return Regex.Replace(text, "{(.*?)}", "");
    }

    // ---- loading ------------------------------------------------------------------------

    public static async Task<DocumentData?> LoadAsync(AppDbFactory dbs, Sales sales, string kind, long id, int? companyId = null)
    {
        var data = await LoadCoreAsync(dbs, sales, kind, id, companyId);
        if (data is null) return null;
        await using var db = dbs.Create();
        var model = char.ToUpperInvariant(kind[0]) + kind[1..];
        data.PrintedFields = await CustomFields.PrintedAsync(db, data.Company.Id, model, id, data.Settings.GetValueOrDefault("carbon_date_format", "Y/m/d"));
        data.CustomPlaceholders = await CustomFields.PlaceholdersAsync(db, (model, id), ("Customer", data.Customer.Id));
        return data;
    }

    private static async Task<DocumentData?> LoadCoreAsync(AppDbFactory dbs, Sales sales, string kind, long id, int? companyId)
    {
        await using var db = dbs.Create();
        int docCompany;
        long? customerId;
        int? currencyId;
        switch (kind)
        {
            case Invoice:
                var i = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (i is null) return null;
                (docCompany, customerId, currencyId) = (i.CompanyId!.Value, i.CustomerId, i.CurrencyId);
                break;
            case Estimate:
                var e = await db.Estimates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                (docCompany, customerId, currencyId) = (e.CompanyId!.Value, e.CustomerId, e.CurrencyId);
                break;
            case Payment:
                var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (p is null) return null;
                (docCompany, customerId, currencyId) = (p.CompanyId!.Value, p.CustomerId, p.CurrencyId);
                break;
            case Expense:
                var x = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (x is null) return null;
                (docCompany, customerId, currencyId) = (x.CompanyId!.Value, x.CustomerId, x.CurrencyId);
                break;
            default:
                return null;
        }
        if (companyId is { } expected && expected != docCompany)
            return null;

        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == docCompany);
        var settings = await Companies.SettingsAsync(db, docCompany);
        // An expense need not be billed to a customer: the recipient is then typed in the send form.
        var customer = customerId is null ? new Customer() : await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        var companyAddress = await db.Addresses.AsNoTracking()
            .Where(a => a.CompanyId == docCompany && a.CustomerId == null && a.UserId == null).OrderBy(a => a.Id).FirstOrDefaultAsync();
        var billing = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(a => a.CustomerId == customer.Id && a.Type == "billing");
        var countries = new[] { companyAddress?.CountryId, billing?.CountryId }.OfType<int>().ToList();
        var countryNames = await db.Countries.Where(c => countries.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        var currency = currencyId is { } cid ? await db.Currencies.FindAsync(cid)
            : int.TryParse(settings.GetValueOrDefault("currency"), out var cc) ? await db.Currencies.FindAsync(cc) : null;

        string? Country(Address? a) => a?.CountryId is { } c ? countryNames.GetValueOrDefault(c) : null;

        if (kind == Payment)
        {
            var p = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == id);
            var paid = await db.PaymentAllocations.Where(a => a.PaymentId == id)
                .Join(db.Invoices, a => a.InvoiceId, inv => inv.Id, (a, inv) => new { inv.InvoiceNumber, a.Amount })
                .ToListAsync();
            return new DocumentData
            {
                Kind = kind, Id = id, Company = company, CompanyAddress = companyAddress, CompanyCountry = Country(companyAddress),
                Customer = customer, BillingAddress = billing, BillingCountry = Country(billing), Currency = currency, Settings = settings,
                Number = p.PaymentNumber, Date = p.PaymentDate.ToDateTime(TimeOnly.MinValue), Notes = p.Notes, Status = "PAID",
                UniqueHash = p.UniqueHash, Amount = p.Amount, Total = p.Amount,
                PaymentMethod = p.PaymentMethodId is { } m ? await db.PaymentMethods.Where(x => x.Id == m).Select(x => x.Name).FirstOrDefaultAsync() : null,
                PaidInvoices = paid.Select(x => (x.InvoiceNumber, x.Amount)).ToList(),
            };
        }

        if (kind == Expense)
        {
            var x = await db.Expenses.AsNoTracking().SingleAsync(e => e.Id == id);
            return new DocumentData
            {
                Kind = kind, Id = id, Company = company, CompanyAddress = companyAddress, CompanyCountry = Country(companyAddress),
                Customer = customer, BillingAddress = billing, BillingCountry = Country(billing), Currency = currency, Settings = settings,
                Number = x.ExpenseNumber ?? $"DEP-{x.Id}", Date = x.ExpenseDate.ToDateTime(TimeOnly.MinValue), Notes = x.Notes, Status = "PAID",
                Amount = x.Amount, Total = x.Amount,
                Category = await db.ExpenseCategories.Where(c => c.Id == x.ExpenseCategoryId).Select(c => c.Name).FirstOrDefaultAsync(),
                PaymentMethod = x.PaymentMethodId is { } m ? await db.PaymentMethods.Where(pm => pm.Id == m).Select(pm => pm.Name).FirstOrDefaultAsync() : null,
            };
        }

        var form = await sales.LoadAsync(docCompany, kind, (int)id);
        var taxes = form.Draft.TaxPerItem
            ? form.Draft.Items.SelectMany(i => i.Taxes).GroupBy(t => t.TaxTypeId)
                .Select(g => new DraftTax { TaxTypeId = g.Key, Name = g.First().Name, Percent = g.First().Percent, Amount = g.Sum(t => t.Amount) }).ToList()
            : form.Draft.Taxes;

        if (kind == Invoice)
        {
            var i = await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == id);
            return new DocumentData
            {
                Kind = kind, Id = id, Company = company, CompanyAddress = companyAddress, CompanyCountry = Country(companyAddress),
                Customer = customer, BillingAddress = billing, BillingCountry = Country(billing), Currency = currency, Settings = settings,
                Number = i.InvoiceNumber, Date = i.InvoiceDate, DueDate = i.DueDate, Reference = i.ReferenceNumber, Notes = i.Notes,
                Status = i.Status, PaidStatus = i.PaidStatus, UniqueHash = i.UniqueHash, Sent = i.Sent, TemplateName = i.TemplateName,
                IsCreditNote = i.Type == CreditNotes.Type, RelatedInvoiceId = i.RelatedInvoiceId, CreditReason = i.CreditReason,
                RelatedNumber = i.RelatedInvoiceId is { } rel ? await db.Invoices.Where(x => x.Id == rel).Select(x => x.InvoiceNumber).FirstOrDefaultAsync() : null,
                Items = form.Draft.Items, Taxes = taxes, TaxPerItem = form.Draft.TaxPerItem, DiscountPerItem = form.Draft.DiscountPerItem,
                TaxIncluded = i.TaxIncluded, SubTotal = i.SubTotal, DiscountVal = i.DiscountVal ?? 0, Total = i.Total, DueAmount = i.DueAmount,
            };
        }

        var est = await db.Estimates.AsNoTracking().SingleAsync(x => x.Id == id);
        return new DocumentData
        {
            Kind = kind, Id = id, Company = company, CompanyAddress = companyAddress, CompanyCountry = Country(companyAddress),
            Customer = customer, BillingAddress = billing, BillingCountry = Country(billing), Currency = currency, Settings = settings,
            Number = est.EstimateNumber, Date = est.EstimateDate.ToDateTime(TimeOnly.MinValue), DueDate = est.ExpiryDate,
            Reference = est.ReferenceNumber, Notes = est.Notes, Status = est.Status, UniqueHash = est.UniqueHash, TemplateName = est.TemplateName,
            Items = form.Draft.Items, Taxes = taxes, TaxPerItem = form.Draft.TaxPerItem, DiscountPerItem = form.Draft.DiscountPerItem,
            TaxIncluded = est.TaxIncluded, SubTotal = est.SubTotal, DiscountVal = est.DiscountVal ?? 0, Total = est.Total, DueAmount = est.Total,
        };
    }
}
