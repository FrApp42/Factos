using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Factos.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Factos.Api.Auth;

/// Laravel Sanctum 4's token guard: "{id}|{plaintext}" is looked up by id and compared
/// by hash; a bare plaintext (no "|") is looked up by hash directly (findToken()).
public class SanctumTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApplicationDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// Claim carrying the id of the token that authenticated the request
    /// (Sanctum's currentAccessToken()).
    public const string TokenIdClaim = "sanctum_token_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var raw = header["Bearer ".Length..].Trim();
        var token = await FindTokenAsync(raw);
        if (token is null || token.TokenableType != "user")
            return AuthenticateResult.Fail("Invalid token.");
        if (token.ExpiresAt is { } expiry && expiry <= DateTime.UtcNow)
            return AuthenticateResult.Fail("Expired token.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.TokenableId);
        if (user is null)
            return AuthenticateResult.Fail("Invalid token.");

        token.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(TokenIdClaim, token.Id.ToString()),
        ], SanctumTokenDefaults.Scheme);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SanctumTokenDefaults.Scheme));
    }

    private async Task<PersonalAccessToken?> FindTokenAsync(string raw)
    {
        var separator = raw.IndexOf('|');
        if (separator < 0)
        {
            var bareHash = PersonalAccessToken.Hash(raw);
            return await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.TokenHash == bareHash);
        }

        if (!int.TryParse(raw[..separator], out var id))
            return null;
        var token = await db.PersonalAccessTokens.FindAsync(id);
        var hash = PersonalAccessToken.Hash(raw[(separator + 1)..]);

        // hash_equals(): constant-time, so a wrong hash cannot be found byte by byte.
        return token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(token.TokenHash), Encoding.ASCII.GetBytes(hash))
            ? token
            : null;
    }

    /// Laravel answers an unauthenticated JSON request with 401 {"message":"Unauthenticated."}.
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new { message = LaravelResponse.Unauthenticated });
    }
}
