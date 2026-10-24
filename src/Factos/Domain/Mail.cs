using Factos.Components;
using Factos.Data;
using Factos.Data.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Factos.Domain;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// none, tls (STARTTLS) or ssl (implicit TLS), as upstream's mail_encryption.
    public string Encryption { get; set; } = "tls";
    public string FromName { get; set; } = "";
    public string FromMail { get; set; } = "";

    public bool IsComplete => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromMail);
}

/// SMTP delivery with upstream's company-scoped settings (`company_mail_*`, used when
/// `use_custom_mail_config` is YES) over an installation-wide default (`Mail` section of
/// the configuration, upstream's MAIL_* env). Upstream stores the SMTP password in clear;
/// here it is encrypted with ASP.NET Data Protection.
// ponytail: SMTP only; Mailgun, SES and Postmark all accept SMTP, add their HTTP drivers if needed.
public class Mailer(AppDbFactory dbs, IDataProtectionProvider protection, IConfiguration config)
{
    private IDataProtector Protector => protection.CreateProtector("Factos.CompanyMailPassword");

    public async Task<SmtpSettings?> SettingsAsync(int companyId)
    {
        await using var db = dbs.Create();
        var s = await db.CompanySettings.Where(x => x.CompanyId == companyId && x.Option.StartsWith("company_") || x.CompanyId == companyId && x.Option == "use_custom_mail_config")
            .ToDictionaryAsync(x => x.Option, x => x.Value);
        if (s.GetValueOrDefault("use_custom_mail_config") == "YES")
        {
            var company = new SmtpSettings
            {
                Host = s.GetValueOrDefault("company_mail_host", ""),
                Port = int.TryParse(s.GetValueOrDefault("company_mail_port"), out var port) ? port : 587,
                Username = s.GetValueOrDefault("company_mail_username"),
                Password = Unprotect(s.GetValueOrDefault("company_mail_password")),
                Encryption = s.GetValueOrDefault("company_mail_encryption", "tls"),
                FromName = s.GetValueOrDefault("company_from_name", ""),
                FromMail = s.GetValueOrDefault("company_from_mail", ""),
            };
            if (company.IsComplete)
                return company;
        }

        var global = config.GetSection("Mail").Get<SmtpSettings>();
        return global is { IsComplete: true } ? global : null;
    }

    /// Stores the company's SMTP settings; a blank password keeps the stored one.
    public async Task SaveAsync(int companyId, SmtpSettings settings, bool enabled)
    {
        await using var db = dbs.Create();
        var values = new Dictionary<string, string>
        {
            ["use_custom_mail_config"] = enabled ? "YES" : "NO",
            ["company_mail_driver"] = "smtp",
            ["company_mail_host"] = settings.Host.Trim(),
            ["company_mail_port"] = settings.Port.ToString(),
            ["company_mail_username"] = settings.Username?.Trim() ?? "",
            ["company_mail_encryption"] = settings.Encryption,
            ["company_from_name"] = settings.FromName.Trim(),
            ["company_from_mail"] = settings.FromMail.Trim(),
        };
        if (!string.IsNullOrEmpty(settings.Password))
            values["company_mail_password"] = Protector.Protect(settings.Password);

        var now = Data.Timestamp.Now();
        foreach (var (option, value) in values)
        {
            var row = await db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Option == option);
            if (row is null)
                db.CompanySettings.Add(row = new CompanySetting { CompanyId = companyId, Option = option, CreatedAt = now });
            row.Value = value;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
    }

    public async Task SendAsync(SmtpSettings settings, MimeMessage message, CancellationToken cancel = default)
    {
        if (message.From.Count == 0)
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromMail));

        using var client = new SmtpClient();
        var security = settings.Encryption switch
        {
            "ssl" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };
        await client.ConnectAsync(settings.Host, settings.Port, security, cancel);
        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancel);
        await client.SendAsync(message, cancel);
        await client.DisconnectAsync(true, cancel);
    }

    private string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return null;
        try { return Protector.Unprotect(stored); }
        // Keys lost (new machine, wiped key folder): the password has to be entered again.
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }
}

public class SendForm
{
    public string To { get; set; } = "";
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

/// Sending an invoice, estimate, payment receipt or expense (Factos-only) to the customer, as upstream's
/// Send*Mail mailables: the body gets the document's placeholders, a link to the public
/// page (backed by an `email_logs` token) or the PDF attached, per company setting.
public class DocumentMailer(AppDbFactory dbs, Mailer mailer, Logos logos)
{
    /// The send form, prefilled from the company's mail body setting.
    public static SendForm Prepare(DocumentData d) => new()
    {
        To = d.Customer.Email ?? "",
        Subject = d.Kind switch { DocumentData.Invoice => Loc.T("Nouvelle facture"), DocumentData.Estimate => Loc.T("Nouveau devis"), DocumentData.Expense => Loc.T("Dépense"), _ => Loc.T("Reçu de paiement") }
            + $" {d.Number}",
        Body = d.Settings.GetValueOrDefault($"{d.Kind}_mail_body") ?? Companies.DefaultMailBody(d.Kind),
    };

    public static bool AttachesPdf(DocumentData d) => d.Settings.GetValueOrDefault($"{d.Kind}_email_attachment", "NO") != "NO";

    /// The mailable kinds, in settings order.
    public static readonly (string Kind, string Label)[] Kinds =
        [(DocumentData.Invoice, "facture"), (DocumentData.Estimate, "devis"), (DocumentData.Payment, Loc.T("reçu de paiement")), (DocumentData.Expense, Loc.T("dépense"))];

    /// Factos-only company setting listing the kinds a customer does not receive, comma-separated
    /// (no row = receives everything). A setting rather than a `customers` column: no migration.
    public static string CustomerOption(long customerId) => $"factos_customer_mail_off.{customerId}";

    public static HashSet<string> CustomerOff(Dictionary<string, string> settings, long customerId)
        => [.. settings.GetValueOrDefault(CustomerOption(customerId), "").Split(',', StringSplitOptions.RemoveEmptyEntries)];

    /// Factos-only `{kind}_email_enabled` company setting (on unless NO), then the customer's own choice.
    public static bool Enabled(DocumentData d) => d.Settings.GetValueOrDefault($"{d.Kind}_email_enabled", "YES") != "NO"
        && !CustomerOff(d.Settings, d.Customer.Id).Contains(d.Kind);

    public async Task SendAsync(DocumentData d, SendForm form, string? baseUrl)
    {
        if (!Enabled(d))
            throw new SalesException(Loc.T("L'envoi de ce type de document par e-mail est désactivé (Paramètres › E-mails ou fiche du client)."));
        if (string.IsNullOrWhiteSpace(form.To) || string.IsNullOrWhiteSpace(form.Subject) || string.IsNullOrWhiteSpace(form.Body))
            throw new SalesException(Loc.T("Le destinataire, l'objet et le message sont obligatoires."));
        var smtp = await mailer.SettingsAsync(d.Company.Id)
            ?? throw new SalesException(Loc.T("Aucun serveur d'envoi n'est configuré : renseignez-le dans Paramètres › E-mails."));

        // A subject is plain text: HTML-escaping it would show "&#233;" for "é".
        var subject = d.Render(form.Subject, html: false);
        var body = d.Render(form.Body);
        // Without a public address the customer gets the PDF instead of a link.
        var attach = AttachesPdf(d) || string.IsNullOrEmpty(baseUrl);

        await using var db = dbs.Create();
        var now = Data.Timestamp.Now();
        var log = new EmailLog
        {
            From = smtp.FromMail, To = form.To, Subject = subject, Body = body,
            MailableType = d.Kind, MailableId = d.Id.ToString(), Token = Companies.PublicToken(),
            CreatedAt = now, UpdatedAt = now,
        };
        db.EmailLogs.Add(log);
        await db.SaveChangesAsync();

        var message = new MimeMessage { Subject = subject };
        foreach (var list in new[] { (form.To, message.To), (form.Cc, message.Cc), (form.Bcc, message.Bcc) })
            foreach (var address in (list.Item1 ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Item2.Add(MailboxAddress.Parse(address));

        var link = attach ? null : $"{baseUrl!.TrimEnd('/')}/customer/{d.Kind}s/view/{log.Token}";
        var builder = new BodyBuilder();
        var logo = logos.Read(d.Company.Logo);
        string? logoCid = null;
        if (logo is not null)
        {
            // Embedded rather than linked: mail clients block remote images by default.
            var image = builder.LinkedResources.Add("logo" + Path.GetExtension(d.Company.Logo), logo);
            image.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            logoCid = image.ContentId;
        }
        // Factos-only `mail_tracking`: a remote 1x1 image, so it needs a public address.
        var pixel = d.Settings.GetValueOrDefault("mail_tracking") == "YES" && !string.IsNullOrEmpty(baseUrl)
            ? $"{baseUrl.TrimEnd('/')}/customer/mails/{log.Token}/open.gif" : null;
        builder.HtmlBody = Html(d, body, link, logoCid, pixel);
        if (attach)
            builder.Attachments.Add(d.FileName, DocumentPdf.Render(d, logo), new ContentType("application", "pdf"));
        message.Body = builder.ToMessageBody();

        try
        {
            await mailer.SendAsync(smtp, message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            db.EmailLogs.Remove(log);
            await db.SaveChangesAsync();
            throw new SalesException(Loc.F("L'envoi a échoué : {0}", error.Message));
        }

        // A draft becomes SENT once it has gone out.
        if (d.Kind == DocumentData.Invoice)
            await db.Invoices.Where(i => i.Id == d.Id && i.Status == Sales.Draft)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, Sales.Sent).SetProperty(i => i.Sent, true).SetProperty(i => i.UpdatedAt, now));
        else if (d.Kind == DocumentData.Estimate)
            await db.Estimates.Where(e => e.Id == d.Id && e.Status == Sales.Draft)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, Sales.Sent).SetProperty(e => e.UpdatedAt, now));
    }

    /// The mail layout: company name, the rendered body, the view button.
    private static string Html(DocumentData d, string body, string? link, string? logoCid, string? pixel) => $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:600px;margin:0 auto;color:#333">
          {(logoCid is null
              ? $"<h2 style=\"color:#1b6ec2\">{System.Net.WebUtility.HtmlEncode(d.Company.Name)}</h2>"
              : $"<img src=\"cid:{logoCid}\" alt=\"{System.Net.WebUtility.HtmlEncode(d.Company.Name)}\" style=\"max-height:60px;max-width:220px\" />")}
          <div style="line-height:1.5">{body}</div>
          {(link is null ? "" : $"""<p style="margin:24px 0"><a href="{link}" style="background:#1b6ec2;color:#fff;padding:10px 18px;border-radius:4px;text-decoration:none">Voir le document</a></p>""")}
          {(pixel is null ? "" : $"""<img src="{pixel}" width="1" height="1" alt="" style="display:block;border:0" />""")}
        </div>
        """;
}
