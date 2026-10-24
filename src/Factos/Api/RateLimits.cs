using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Factos.Api;

/// InvoiceShelf's throttles: `throttleApi('180,1')` on the API group and
/// `throttle:10,1,login` on the login route. 429 bodies use Laravel's message.
public static class RateLimits
{
    public const string Api = "api";
    public const string Login = "login";

    public static IServiceCollection AddFactosRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            // ponytail: keyed by client IP for every request; Laravel keys signed-in callers
            // by user id. Rate limiting runs before the Sanctum scheme authenticates.
            options.AddPolicy(Api, ctx => PerMinute(ctx, Api, 180));
            options.AddPolicy(Login, ctx => PerMinute(ctx, Login, 10));
            options.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                await ctx.HttpContext.Response.WriteAsJsonAsync(new { message = LaravelResponse.TooManyAttempts }, token);
            };
        });

    private static RateLimitPartition<string> PerMinute(HttpContext ctx, string name, int limit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{name}:{ctx.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
