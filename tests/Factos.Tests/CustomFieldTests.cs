using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Custom fields: slug, answer validation, storage, document placeholders and printing.
public class CustomFieldTests : ApiTestBase
{
    private static CustomFieldAnswer Answer(string type, string? value, string? validation = null, string? options = null, bool required = false)
        => new() { Field = new CustomField { Label = "Champ", Type = type, Validation = validation, Options = options, IsRequired = required }, Value = value };

    private static string? Check(CustomFieldAnswer a) => CustomFields.Validate(a, TimeZoneInfo.Utc);

    [Fact]
    public void Slugs_follow_clean_slug()
    {
        Assert.Equal("CUSTOM_INVOICE_BON_DE_COMMANDE", CustomFields.Slug("Invoice", "Bon de commande !", []));
        Assert.Equal("CUSTOM_INVOICE_PO_1", CustomFields.Slug("Invoice", "PO", ["CUSTOM_INVOICE_PO"]));
        var all = new[] { "CUSTOM_X_A" }.Concat(Enumerable.Range(1, 10).Select(n => $"CUSTOM_X_A_{n}")).ToList();
        Assert.Null(CustomFields.Slug("X", "a", all));
    }

    [Fact]
    public void Answers_are_checked_against_their_definition()
    {
        Assert.NotNull(Check(Answer("Input", " ", required: true)));
        Assert.Null(Check(Answer("Switch", "0", required: true)));       // off is an answer
        Assert.Null(Check(Answer("Input", null)));
        Assert.NotNull(Check(Answer("Dropdown", "C", options: "[\"A\",\"B\"]")));
        Assert.Null(Check(Answer("Dropdown", "B", options: "[{\"name\":\"A\"},{\"name\":\"B\"}]")));
        Assert.NotNull(Check(Answer("Input", "ab", "{\"min_length\":3}")));
        Assert.NotNull(Check(Answer("Input", "abcd", "{\"max_length\":3}")));
        Assert.NotNull(Check(Answer("Number", "12", "{\"max\":10}")));
        Assert.NotNull(Check(Answer("Number", "1.5")));
        Assert.NotNull(Check(Answer("Input", "AB-12", "{\"pattern\":\"^[0-9]+$\"}")));
        Assert.Null(Check(Answer("Input", "1234", "{\"pattern\":\"^[0-9]+$\"}")));
        Assert.NotNull(Check(Answer("Date", "2026-01-01", "{\"earliest\":\"2026-02-01\"}")));
        Assert.NotNull(Check(Answer("Date", DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), "{\"latest\":\"today\"}")));
        Assert.Null(Check(Answer("DateTime", DateTime.UtcNow.ToString("yyyy-MM-ddT00:01"), "{\"latest\":\"today\"}")));
        Assert.NotNull(Check(Answer("Time", "07:30", "{\"earliest\":\"08:00\"}")));
    }

    [Fact]
    public void Each_type_writes_its_own_column()
    {
        var v = new CustomFieldValue { Type = "Date" };
        CustomFields.Write(v, "2026-03-04");
        Assert.Equal(new DateOnly(2026, 3, 4), v.DateAnswer);
        Assert.Null(v.StringAnswer);
        Assert.Equal("04/03/2026", CustomFields.Formatted(v, "d/m/Y"));

        var s = new CustomFieldValue { Type = "Switch" };
        CustomFields.Write(s, "1");
        Assert.True(s.BooleanAnswer);
        Assert.Equal("1", CustomFields.Raw(s));
        Assert.Equal("number_answer", CustomFields.ValueKey("Phone"));
    }

    [Fact]
    public async Task Answers_are_saved_printed_substituted_and_deleted_with_their_record()
    {
        var sales = new Sales(Service<AppDbFactory>());
        int company = 0;
        long customer = 0;
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = new Currency { Name = "Euro", Code = "EUR", Symbol = "€", Precision = 2, ThousandSeparator = ".", DecimalSeparator = ",", SwapCurrencySymbol = true };
            db.Currencies.Add(eur);
            await db.SaveChangesAsync();
            var c = await Companies.CreateAsync(db, owner, "Acme FR", eur.Id);
            var cust = new Customer { Name = "Client", CompanyId = c.Id, CurrencyId = eur.Id };
            db.Customers.Add(cust);
            db.CustomFields.AddRange(
                new CustomField { Name = "PO", Label = "Bon de commande", Slug = "CUSTOM_INVOICE_PO", ModelType = "Invoice", Type = "Input", CompanyId = c.Id, Placement = CustomFields.Document, StringAnswer = "PO-?" },
                new CustomField { Name = "Note", Label = "Note interne", Slug = "CUSTOM_INVOICE_NOTE", ModelType = "Invoice", Type = "Input", CompanyId = c.Id, Order = 2 },
                new CustomField { Name = "Code", Label = "Code client", Slug = "CUSTOM_CUSTOMER_CODE", ModelType = "Customer", Type = "Number", CompanyId = c.Id });
            await db.SaveChangesAsync();
            (company, customer) = (c.Id, cust.Id);
        });

        var invoice = await sales.SaveInvoiceAsync(company, Jane, new DocumentForm
        {
            CustomerId = customer, Number = "INV-1",
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "Conseil", Price = 1000, Quantity = 1 }] },
        });
        await WithDb(async db =>
        {
            var fresh = await CustomFields.LoadAsync(db, company, "Invoice", null);
            Assert.Equal(["Bon de commande", "Note interne"], fresh.Select(a => a.Field.Label));
            Assert.Equal("PO-?", fresh[0].Value);    // the default answer prefills a new record
            fresh[0].Value = "PO-42";
            fresh[1].Value = "secret";
            await CustomFields.SaveAsync(db, company, "Invoice", invoice, fresh);
            var code = await CustomFields.LoadAsync(db, company, "Customer", customer);
            code[0].Value = "77";
            await CustomFields.SaveAsync(db, company, "Customer", customer, code);

            // Saving again updates the same rows.
            var again = await CustomFields.LoadAsync(db, company, "Invoice", invoice);
            Assert.Equal("PO-42", again[0].Value);
            await CustomFields.SaveAsync(db, company, "Invoice", invoice, again);
            Assert.Equal(2, await db.CustomFieldValues.CountAsync(v => v.CustomFieldValuableType == "invoice" && v.CustomFieldValuableId == invoice));
        });

        var d = (await DocumentData.LoadAsync(Service<AppDbFactory>(), sales, DocumentData.Invoice, invoice))!;
        Assert.Equal([("Bon de commande", "PO-42")], d.PrintedFields);   // internal fields are not printed
        Assert.Equal("PO-42 / 77", d.Render("{CUSTOM_INVOICE_PO} / {CUSTOM_CUSTOMER_CODE}"));
        Assert.NotEmpty(DocumentPdf.Render(d));

        await sales.DeleteInvoiceAsync(company, invoice);
        await WithDb(async db => Assert.False(await db.CustomFieldValues.AnyAsync(v => v.CustomFieldValuableType == "invoice")));
    }
}
