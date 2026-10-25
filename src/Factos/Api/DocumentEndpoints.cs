using System.Security.Claims;
using Factos.Api.Auth;
using Factos.Components.Pages;
using Factos.Data;
using Factos.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api;

/// Upstream's web routes for documents: `/{kind}s/pdf/{unique_hash}` for signed-in
/// members (`pdf-auth`), and the customer links `/customer/{kind}s/view/{token}` sent by
/// e-mail (`?pdf` streams the PDF, otherwise the page).
public static class DocumentEndpoints
{
    private static readonly Dictionary<string, string> Kinds = new()
    {
        ["invoices"] = DocumentData.Invoice,
        ["estimates"] = DocumentData.Estimate,
        ["payments"] = DocumentData.Payment,
        // Factos-only, for the e-mailed link (expenses have no unique_hash, so no /pdf route).
        ["expenses"] = DocumentData.Expense,
    };

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        // pdf-auth: the web session or an API token.
        var signedIn = new AuthorizeAttribute
        {
            AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{SanctumTokenDefaults.Scheme}",
        };

        app.MapGet("/{kinds:regex(^(invoices|estimates|payments)$)}/pdf/{hash}",
            async (string kinds, string hash, ClaimsPrincipal user, AppDbFactory dbs, Sales sales, Logos logos) =>
            {
                var kind = Kinds[kinds];
                await using var db = dbs.Create();
                var found = kind switch
                {
                    DocumentData.Invoice => await db.Invoices.Where(x => x.UniqueHash == hash).Select(x => new { Id = (long)x.Id, x.CompanyId }).FirstOrDefaultAsync(),
                    DocumentData.Estimate => await db.Estimates.Where(x => x.UniqueHash == hash).Select(x => new { Id = (long)x.Id, x.CompanyId }).FirstOrDefaultAsync(),
                    _ => await db.Payments.Where(x => x.UniqueHash == hash).Select(x => new { x.Id, x.CompanyId }).FirstOrDefaultAsync(),
                };
                if (found is null)
                    return Results.NotFound();
                var (id, companyId) = (found.Id, found.CompanyId);
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
                // A document of a company the user does not belong to is answered like a missing one.
                if (!await db.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.CompanyId == companyId))
                    return Results.NotFound();
                var data = await DocumentData.LoadAsync(dbs, sales, kind, id);
                return data is null ? Results.NotFound() : Pdf(data, logos);
            })
            .RequireAuthorization(signedIn);

        // The logo is public branding: it is printed on every document sent to customers.
        app.MapGet("/logos/{hash}", async (string hash, AppDbFactory dbs, Logos logos) =>
        {
            await using var db = dbs.Create();
            var file = await db.Companies.Where(c => c.UniqueHash == hash).Select(c => c.Logo).FirstOrDefaultAsync();
            return logos.Read(file) is { } bytes ? Results.File(bytes, Logos.ContentType(file!)) : Results.NotFound();
        });

        // Factos-only expense attachments: members of the expense's company only, always a download.
        app.MapGet("/expenses/files/{id:long}/{name}", async (long id, string name, ClaimsPrincipal user, AppDbFactory dbs, ExpenseFiles files) =>
        {
            await using var db = dbs.Create();
            var companyId = await db.Expenses.Where(e => e.Id == id).Select(e => (int?)e.CompanyId).FirstOrDefaultAsync();
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (companyId is null || !await db.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.CompanyId == companyId))
                return Results.NotFound();
            return files.Find(id, name) is { } path ? Results.File(File.OpenRead(path), "application/octet-stream", fileDownloadName: name) : Results.NotFound();
        })
        .RequireAuthorization(signedIn);

        // Factos-only mail tracking pixel (`mail_tracking`): the first load stamps opened_at.
        app.MapGet("/customer/mails/{token}/open.gif", async (string token, HttpResponse response, AppDbFactory dbs) =>
        {
            await using var db = dbs.Create();
            await db.EmailLogs.Where(l => l.Token == token && l.OpenedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.OpenedAt, Timestamp.Now()));
            response.Headers.CacheControl = "no-store";
            return Results.File(Pixel, "image/gif");
        });

        app.MapGet("/customer/{kinds:regex(^(invoices|estimates|payments|expenses)$)}/view/{token}",
            async (string kinds, string token, HttpRequest request, AppDbFactory dbs, Sales sales, Logos logos) =>
            {
                var kind = Kinds[kinds];
                await using var db = dbs.Create();
                var log = await db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Token == token);
                if (log is null || log.MailableType != kind || !long.TryParse(log.MailableId, out var id))
                    return Results.NotFound();
                var data = await DocumentData.LoadAsync(dbs, sales, kind, id);
                if (data is null)
                    return Results.NotFound();
                if (Expired(log.CreatedAt, data.Settings))
                    return Results.Text("Link Expired.", statusCode: StatusCodes.Status403Forbidden);

                await RecordReadingAsync(db, kind, id);

                return request.Query.ContainsKey("pdf")
                    ? Pdf(data, logos)
                    : new RazorComponentResult<PublicDocument>(new { D = data, PdfUrl = $"{request.Path}?pdf" });
            });
    }

    /// A transparent 1x1 GIF.
    private static readonly byte[] Pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    private static IResult Pdf(DocumentData data, Logos logos)
        => Results.File(DocumentPdf.Render(data, logos.Read(data.Company.Logo)), "application/pdf", fileDownloadName: data.FileName);

    /// EmailLog::isExpired(): compared by calendar day, only when expiry is enabled.
    private static bool Expired(DateTime? createdAt, Dictionary<string, string> settings)
    {
        if (settings.GetValueOrDefault("automatically_expire_public_links") != "YES")
            return false;
        var days = int.TryParse(settings.GetValueOrDefault("link_expiry_days"), out var d) ? d : 0;
        var lastValidDay = (createdAt ?? DateTime.UtcNow).AddDays(days).Date;
        return DateTime.UtcNow.Date > lastValidDay;
    }

    /// recordReading(): a draft or sent document opened by the customer becomes VIEWED.
    // ponytail: the "document viewed" notification e-mail (notify_*_viewed) is not sent yet.
    private static async Task RecordReadingAsync(ApplicationDbContext db, string kind, long id)
    {
        string[] unread = [Sales.Sent, Sales.Draft];
        if (kind == DocumentData.Invoice)
            await db.Invoices.Where(i => i.Id == id && unread.Contains(i.Status))
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, Sales.Viewed).SetProperty(i => i.Viewed, true));
        else if (kind == DocumentData.Estimate)
            await db.Estimates.Where(e => e.Id == id && unread.Contains(e.Status))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, Sales.Viewed));
    }
}
