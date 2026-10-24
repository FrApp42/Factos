using Factos.Components;
using System.Collections.Concurrent;
using System.Text;
using Factos.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Factos.Domain;

/// "Forgot password" by e-mail, with upstream's broker settings: one mail per account per
/// 60 seconds (`throttle`), links valid 60 minutes (`expire`, set on Identity's token
/// provider). Never tells whether an address has an account.
public class PasswordResets(AppDbFactory dbs, Mailer mailer, IServiceScopeFactory scopes, ILogger<PasswordResets> logger)
{
    private static readonly TimeSpan Throttle = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<int, DateTime> _lastSent = new();

    public async Task RequestAsync(string email, string baseUrl)
    {
        using var scope = scopes.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
            return;

        var now = DateTime.UtcNow;
        if (_lastSent.TryGetValue(user.Id, out var last) && now - last < Throttle)
            return;
        _lastSent[user.Id] = now;

        var settings = await SmtpForAsync(user.Id);
        if (settings is null)
        {
            logger.LogWarning("Password reset for user {Id} not sent: no mail server configured", user.Id);
            return;
        }

        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var link = $"{baseUrl.TrimEnd('/')}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email!)}&code={token}";
        var message = new MimeMessage { Subject = Loc.T("Réinitialisation de votre mot de passe") };
        message.To.Add(MailboxAddress.Parse(user.Email!));
        message.Body = new BodyBuilder
        {
            HtmlBody = $"""
                <p>Bonjour {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
                <p>Vous avez demandé à réinitialiser votre mot de passe Factos.</p>
                <p><a href="{link}">Choisir un nouveau mot de passe</a></p>
                <p>Ce lien expire dans 60 minutes. Si vous n'êtes pas à l'origine de cette demande, ignorez cet e-mail.</p>
                """,
        }.ToMessageBody();

        try
        {
            await mailer.SendAsync(settings, message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "Password reset mail to user {Id} failed", user.Id);
        }
    }

    /// The installation's mail server, or else the first of the user's companies that has one.
    private async Task<SmtpSettings?> SmtpForAsync(int userId)
    {
        await using var db = dbs.Create();
        var companies = await db.UserCompanies.Where(uc => uc.UserId == userId && uc.CompanyId != null)
            .OrderBy(uc => uc.Id).Select(uc => uc.CompanyId!.Value).ToListAsync();
        foreach (var companyId in companies)
            if (await mailer.SettingsAsync(companyId) is { } settings)
                return settings;
        return companies.Count == 0 ? await mailer.SettingsAsync(0) : null;
    }
}
