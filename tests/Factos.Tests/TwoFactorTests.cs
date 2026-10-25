using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Factos.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Factos.Tests;

/// TOTP second factor (Factos-only): the API login must not be a way around it.
public class TwoFactorTests : ApiTestBase
{
    // RFC 6238, SHA-1, 30 s steps, 6 digits; the key is Identity's base32 authenticator key.
    private static string Totp(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(base32.ToUpperInvariant().Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        var key = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30).Reverse().ToArray();
        var hash = HMACSHA1.HashData(key, counter);
        var o = hash[^1] & 0xF;
        var value = ((hash[o] & 0x7F) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private async Task<(string Key, string Recovery)> EnableForJane()
    {
        string key = "", recovery = "";
        await InScope(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var jane = (await users.FindByIdAsync(Jane.ToString()))!;
            await users.ResetAuthenticatorKeyAsync(jane);
            key = (await users.GetAuthenticatorKeyAsync(jane))!;
            await users.SetTwoFactorEnabledAsync(jane, true);
            recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(jane, 2))!.First();
        });
        return (key, recovery);
    }

    private Task<HttpResponseMessage> Attempt(string? code)
        => Client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "jane@example.com", password = Password, device_name = "phone", two_factor_code = code });

    [Fact]
    public async Task Login_needs_a_valid_code_once_enabled()
    {
        var (key, _) = await EnableForJane();

        var missing = await Attempt(null);
        Assert.Equal((HttpStatusCode)422, missing.StatusCode);
        Assert.Equal("The two factor code field is required.", (await Json(missing)).GetProperty("errors").GetProperty("two_factor_code")[0].GetString());

        var wrong = await Attempt(Totp(key) == "000000" ? "000001" : "000000");
        Assert.Equal((HttpStatusCode)422, wrong.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Attempt(Totp(key))).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        var (_, recovery) = await EnableForJane();
        Assert.Equal(HttpStatusCode.OK, (await Attempt(recovery)).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await Attempt(recovery)).StatusCode);
    }

    [Fact]
    public async Task Accounts_without_the_factor_are_unaffected()
        => Assert.NotEmpty(await Login("bob@example.com"));
}
