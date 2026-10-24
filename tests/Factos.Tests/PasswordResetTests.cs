using System.Text;
using System.Text.RegularExpressions;
using Factos.Data;
using Factos.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Factos.Tests;

public class PasswordResetTests : ApiTestBase
{
    [Fact]
    public async Task A_reset_link_is_mailed_once_per_minute_and_resets_the_password()
    {
        using var sink = new SmtpSink();
        await Service<Mailer>().SaveAsync(CompanyA,
            new SmtpSettings { Host = "127.0.0.1", Port = sink.Port, Encryption = "none", FromName = "Factos", FromMail = "noreply@factos.test" }, enabled: true);
        var resets = Service<PasswordResets>();

        await resets.RequestAsync("nobody@example.com", "https://factos.test/");
        Assert.Empty(sink.Messages);   // unknown address: nothing sent, nothing revealed

        await resets.RequestAsync("jane@example.com", "https://factos.test/");
        await resets.RequestAsync("jane@example.com", "https://factos.test/");   // within 60 s: throttled
        var raw = Assert.Single(sink.Messages);

        var mail = MimeKit.MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)));
        var link = Regex.Match(mail.HtmlBody, "href=\"(?<url>[^\"]+)\"").Groups["url"].Value;
        Assert.StartsWith("https://factos.test/Account/ResetPassword?email=", link);
        var code = Regex.Match(link, "code=(?<c>[^&]+)").Groups["c"].Value;
        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

        using var scope = Service<IServiceScopeFactory>().CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var jane = (await users.FindByEmailAsync("jane@example.com"))!;
        Assert.True((await users.ResetPasswordAsync(jane, token, "brand-new-pass")).Succeeded);
        Assert.NotEmpty(await Login("jane@example.com", "brand-new-pass"));
        // A token is single-use.
        Assert.False((await users.ResetPasswordAsync(jane, token, "another-pass1")).Succeeded);
    }
}
