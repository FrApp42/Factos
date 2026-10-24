using System.Net;
using System.Net.Http.Headers;
using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Document PDFs, e-mail placeholders, the customer link and the protected PDF route.
public class MailAndPdfTests : ApiTestBase
{
    private int _company;
    private long _customer;
    private int _invoice;
    private string _hash = "";

    private Sales Sales => new(Service<AppDbFactory>());

    private async Task SetUpAsync()
    {
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = new Currency { Name = "Euro", Code = "EUR", Symbol = "€", Precision = 2, ThousandSeparator = ".", DecimalSeparator = ",", SwapCurrencySymbol = true };
            db.Currencies.Add(eur);
            await db.SaveChangesAsync();
            var company = await Companies.CreateAsync(db, owner, "Ateliers <Dupont>", eur.Id);
            var customer = new Customer { Name = "Client & Fils", ContactName = "Marie", Email = "marie@client.test", CompanyId = company.Id, CurrencyId = eur.Id };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            (_company, _customer) = (company.Id, customer.Id);
        });
        _invoice = await Sales.SaveInvoiceAsync(_company, Jane, new DocumentForm
        {
            CustomerId = _customer,
            Number = "INV-000001",
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "Conseil", Price = 120000, Quantity = 1 }] },
        });
        await WithDb(async db => _hash = (await db.Invoices.SingleAsync(i => i.Id == _invoice)).UniqueHash!);
    }

    private Task<DocumentData?> LoadAsync(string kind, long id) => DocumentData.LoadAsync(Service<AppDbFactory>(), Sales, kind, id);

    [Fact]
    public async Task Placeholders_are_escaped_and_unknown_tokens_dropped()
    {
        await SetUpAsync();
        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;

        Assert.Equal("Bonjour Marie, facture INV-000001 de <b>Ateliers &lt;Dupont&gt;</b> pour Client &amp; Fils.",
            d.Render("Bonjour {PRIMARY_CONTACT_NAME}, facture {INVOICE_NUMBER} de <b>{COMPANY_NAME}</b> pour {CONTACT_DISPLAY_NAME}.{UNKNOWN_TOKEN}"));
    }

    [Fact]
    public async Task Invoices_and_receipts_render_as_pdf()
    {
        await SetUpAsync();
        await Sales.MarkInvoiceSentAsync(_company, _invoice);
        var paymentId = await Sales.SavePaymentAsync(_company, Jane, new PaymentForm { CustomerId = _customer, Amount = 50000, Number = "PAY-1", Allocations = [new() { InvoiceId = _invoice, Amount = 50000 }] });

        foreach (var (kind, id) in new[] { (DocumentData.Invoice, (long)_invoice), (DocumentData.Payment, paymentId) })
        {
            var pdf = DocumentPdf.Render((await LoadAsync(kind, id))!);
            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        }
    }

    [Fact]
    public async Task The_customer_link_shows_the_document_streams_the_pdf_and_marks_it_viewed()
    {
        await SetUpAsync();
        await Sales.MarkInvoiceSentAsync(_company, _invoice);
        var token = await LogAsync(DocumentData.Invoice, _invoice.ToString(), DateTime.UtcNow);

        var page = await Client.GetAsync($"/customer/invoices/view/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("INV-000001", await page.Content.ReadAsStringAsync());

        var pdf = await Client.GetAsync($"/customer/invoices/view/{token}?pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        await WithDb(async db =>
        {
            var invoice = await db.Invoices.SingleAsync(i => i.Id == _invoice);
            Assert.Equal((Sales.Viewed, true), (invoice.Status, invoice.Viewed));
        });

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/customer/invoices/view/nope")).StatusCode);
        // A token issued for an invoice does not open an estimate.
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/customer/estimates/view/{token}")).StatusCode);
    }

    [Fact]
    public async Task An_expired_link_is_refused()
    {
        await SetUpAsync();
        var token = await LogAsync(DocumentData.Invoice, _invoice.ToString(), DateTime.UtcNow.AddDays(-30)); // default: 7 days

        var response = await Client.GetAsync($"/customer/invoices/view/{token}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Link Expired.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_pdf_route_is_for_members_of_the_documents_company()
    {
        await SetUpAsync();

        var anonymous = await Client.GetAsync($"/invoices/pdf/{_hash}");
        Assert.NotEqual(HttpStatusCode.OK, anonymous.StatusCode);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login("bob@example.com"));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/invoices/pdf/{_hash}")).StatusCode);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login());
        var own = await Client.GetAsync($"/invoices/pdf/{_hash}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("application/pdf", own.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_disabled_kind_is_not_sent()
    {
        await SetUpAsync();
        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        Assert.True(DocumentMailer.Enabled(d));
        d.Settings["invoice_email_enabled"] = "NO";

        var error = await Assert.ThrowsAsync<SalesException>(() => Service<DocumentMailer>().SendAsync(d, DocumentMailer.Prepare(d), "http://localhost"));
        Assert.Contains("désactivé", error.Message);
        await WithDb(async db => Assert.False(await db.EmailLogs.AnyAsync()));
    }

    [Fact]
    public async Task A_customer_can_opt_out_of_one_kind()
    {
        await SetUpAsync();
        await WithDb(async db =>
        {
            db.CompanySettings.Add(new CompanySetting { CompanyId = _company, Option = DocumentMailer.CustomerOption(_customer), Value = "estimate,invoice" });
            await db.SaveChangesAsync();
        });

        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        Assert.False(DocumentMailer.Enabled(d));
        await Assert.ThrowsAsync<SalesException>(() => Service<DocumentMailer>().SendAsync(d, DocumentMailer.Prepare(d), "http://localhost"));

        d.Settings[DocumentMailer.CustomerOption(_customer)] = "estimate";
        Assert.True(DocumentMailer.Enabled(d));
    }

    [Fact]
    public async Task A_failed_delivery_leaves_no_log_and_keeps_the_draft()
    {
        await SetUpAsync();
        var mailer = Service<Mailer>();
        // Nothing listens on this port: the SMTP connection is refused.
        await mailer.SaveAsync(_company, new SmtpSettings { Host = "127.0.0.1", Port = 1, Encryption = "none", FromName = "Ateliers", FromMail = "factures@ateliers.test" }, enabled: true);

        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        var sender = Service<DocumentMailer>();
        var error = await Assert.ThrowsAsync<SalesException>(() => sender.SendAsync(d, DocumentMailer.Prepare(d), "http://localhost"));
        Assert.StartsWith("L'envoi a échoué", error.Message);

        await WithDb(async db =>
        {
            Assert.False(await db.EmailLogs.AnyAsync());
            Assert.Equal(Sales.Draft, (await db.Invoices.SingleAsync(i => i.Id == _invoice)).Status);
            // The SMTP password is never stored in clear (here: none given, so none stored).
            Assert.False(await db.CompanySettings.AnyAsync(s => s.Option == "company_mail_password"));
        });
    }

    [Fact]
    public async Task The_smtp_password_is_stored_encrypted()
    {
        await SetUpAsync();
        var mailer = Service<Mailer>();
        await mailer.SaveAsync(_company, new SmtpSettings { Host = "smtp.test", FromMail = "a@b.test", Password = "s3cret-pass" }, enabled: true);

        await WithDb(async db =>
        {
            var stored = await db.CompanySettings.SingleAsync(s => s.CompanyId == _company && s.Option == "company_mail_password");
            Assert.DoesNotContain("s3cret-pass", stored.Value);
        });
        Assert.Equal("s3cret-pass", (await mailer.SettingsAsync(_company))!.Password);
    }

    [Fact]
    public async Task Sending_delivers_the_rendered_mail_with_a_link_and_marks_the_invoice_sent()
    {
        await SetUpAsync();
        using var sink = new SmtpSink();
        await Service<Mailer>().SaveAsync(_company, new SmtpSettings { Host = "127.0.0.1", Port = sink.Port, Encryption = "none", FromName = "Ateliers", FromMail = "factures@ateliers.test" }, enabled: true);

        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        var form = DocumentMailer.Prepare(d);
        form.Subject = "Facture {INVOICE_NUMBER}";
        await Service<DocumentMailer>().SendAsync(d, form, "https://factos.test/");

        var mail = MimeKit.MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(sink.Messages))));
        Assert.Equal("Facture INV-000001", mail.Subject);
        Assert.Equal("marie@client.test", mail.To.Mailboxes.Single().Address);
        Assert.Empty(mail.Attachments); // default: link, no attachment

        await WithDb(async db =>
        {
            var log = await db.EmailLogs.SingleAsync();
            Assert.Equal(("invoice", _invoice.ToString()), (log.MailableType, log.MailableId));
            Assert.Contains($"https://factos.test/customer/invoices/view/{log.Token}", mail.HtmlBody);
            var invoice = await db.Invoices.SingleAsync(i => i.Id == _invoice);
            Assert.Equal((Sales.Sent, true), (invoice.Status, invoice.Sent));
        });
    }

    [Fact]
    public async Task With_mail_tracking_the_pixel_records_the_opening()
    {
        await SetUpAsync();
        using var sink = new SmtpSink();
        await Service<Mailer>().SaveAsync(_company, new SmtpSettings { Host = "127.0.0.1", Port = sink.Port, Encryption = "none", FromName = "Ateliers", FromMail = "factures@ateliers.test" }, enabled: true);

        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        await Service<DocumentMailer>().SendAsync(d, DocumentMailer.Prepare(d), "https://factos.test/");
        Assert.DoesNotContain("open.gif", MimeKit.MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sink.Messages[0]))).HtmlBody);

        d.Settings["mail_tracking"] = "YES";
        await Service<DocumentMailer>().SendAsync(d, DocumentMailer.Prepare(d), "https://factos.test/");
        string? token = null;
        await WithDb(async db => token = await db.EmailLogs.OrderByDescending(l => l.Id).Select(l => l.Token).FirstAsync());
        Assert.Contains($"https://factos.test/customer/mails/{token}/open.gif", MimeKit.MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sink.Messages[1]))).HtmlBody);

        var pixel = await Client.GetAsync($"/customer/mails/{token}/open.gif");
        Assert.Equal(("image/gif", HttpStatusCode.OK), (pixel.Content.Headers.ContentType?.MediaType, pixel.StatusCode));
        await WithDb(async db => Assert.Equal(1, await db.EmailLogs.CountAsync(l => l.OpenedAt != null && l.Token == token)));
    }

    [Fact]
    public async Task With_the_attachment_setting_the_pdf_is_joined()
    {
        await SetUpAsync();
        using var sink = new SmtpSink();
        await Service<Mailer>().SaveAsync(_company, new SmtpSettings { Host = "127.0.0.1", Port = sink.Port, Encryption = "none", FromName = "Ateliers", FromMail = "factures@ateliers.test" }, enabled: true);
        await WithDb(db => db.CompanySettings.Where(s => s.CompanyId == _company && s.Option == "invoice_email_attachment")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "YES")));

        var d = (await LoadAsync(DocumentData.Invoice, _invoice))!;
        await Service<DocumentMailer>().SendAsync(d, DocumentMailer.Prepare(d), "https://factos.test/");

        var mail = MimeKit.MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(sink.Messages))));
        var pdf = Assert.Single(mail.Attachments.OfType<MimeKit.MimePart>());
        Assert.Equal(("application/pdf", "INV-000001.pdf"), (pdf.ContentType.MimeType, pdf.FileName));
    }

    [Fact]
    public async Task An_expense_is_mailed_with_a_working_link()
    {
        await SetUpAsync();
        using var sink = new SmtpSink();
        await Service<Mailer>().SaveAsync(_company, new SmtpSettings { Host = "127.0.0.1", Port = sink.Port, Encryption = "none", FromName = "Ateliers", FromMail = "factures@ateliers.test" }, enabled: true);
        var expenseId = 0;
        await WithDb(async db =>
        {
            var category = new ExpenseCategory { Name = "Déplacements", CompanyId = _company };
            db.ExpenseCategories.Add(category);
            await db.SaveChangesAsync();
            var expense = new Expense { ExpenseDate = new DateOnly(2026, 3, 4), Amount = 4250, ExpenseCategoryId = category.Id, CompanyId = _company, CustomerId = _customer };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
        });

        var d = (await LoadAsync(DocumentData.Expense, expenseId))!;
        Assert.True(DocumentPdf.Render(d).Length > 1000);
        var form = DocumentMailer.Prepare(d);
        form.Subject = "{EXPENSE_CATEGORY} {EXPENSE_NUMBER}";
        await Service<DocumentMailer>().SendAsync(d, form, "https://factos.test/");

        var mail = MimeKit.MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(sink.Messages))));
        Assert.Equal($"Déplacements DEP-{expenseId}", mail.Subject);
        Assert.Equal("marie@client.test", mail.To.Mailboxes.Single().Address);
        var token = "";
        await WithDb(async db => token = (await db.EmailLogs.SingleAsync()).Token!);
        var page = await Client.GetAsync($"/customer/expenses/view/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains($"DEP-{expenseId}", await page.Content.ReadAsStringAsync());
    }

    private async Task<string> LogAsync(string type, string id, DateTime createdAt)
    {
        var token = Companies.PublicToken();
        await WithDb(async db =>
        {
            db.EmailLogs.Add(new EmailLog { From = "a@b.test", To = "c@d.test", Subject = "s", Body = "b", MailableType = type, MailableId = id, Token = token, CreatedAt = createdAt });
            await db.SaveChangesAsync();
        });
        return token;
    }
}
