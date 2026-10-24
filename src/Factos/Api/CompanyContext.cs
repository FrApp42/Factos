using System.Security.Claims;
using Factos.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api;

/// The company a request acts on, resolved the way InvoiceShelf's CompanyMiddleware
/// and ScopeBouncer do it from the `company` request header.
public class CompanyContext(ApplicationDbContext db)
{
    /// The `company` header after CompanyMiddleware rewrote it: the requested company
    /// when the user belongs to it, else the user's first company. Null only for a
    /// super admin who sent no header, or a user with no company.
    public int? CompanyId { get; private set; }

    /// Bouncer's scope: CompanyId, or the user's first company when there is none.
    public int? BouncerScope { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    /// The token that authenticated the request (Sanctum's currentAccessToken()).
    public int? TokenId { get; private set; }

    public async Task ResolveAsync(HttpRequest request, ClaimsPrincipal principal)
    {
        var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        User = await db.Users.SingleAsync(u => u.Id == userId);
        TokenId = int.TryParse(principal.FindFirstValue(Auth.SanctumTokenHandler.TokenIdClaim), out var t) ? t : null;

        var companies = await UserCompanyIdsAsync(userId);
        int? fallback = companies.Count > 0 ? companies[0] : null;
        int? requested = int.TryParse(request.Headers["company"], out var h) ? h : null;

        if (fallback is null)
            CompanyId = requested;              // middleware passes the header through untouched
        else if (User.Role == "super admin" && request.Headers["company"].Count == 0)
            CompanyId = null;
        else
            CompanyId = requested is { } r && companies.Contains(r) ? r : fallback;

        BouncerScope = CompanyId ?? fallback;
    }

    /// `$user->companies()`: user_company rows, in insertion order.
    public Task<List<int>> UserCompanyIdsAsync(int userId) => db.UserCompanies
        .Where(uc => uc.UserId == userId && uc.CompanyId != null)
        .OrderBy(uc => uc.Id)
        .Select(uc => uc.CompanyId!.Value)
        .ToListAsync();
}

/// Applies CompanyContext resolution to every action of a company-scoped controller
/// (InvoiceShelf's `company` + `bouncer` middleware).
public class CompanyScopeFilter(CompanyContext context) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext filterContext, ActionExecutionDelegate next)
    {
        await context.ResolveAsync(filterContext.HttpContext.Request, filterContext.HttpContext.User);
        await next();
    }
}
