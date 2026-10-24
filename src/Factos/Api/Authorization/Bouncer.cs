using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api.Authorization;

/// Port of silber/bouncer 1.0.4, the authorization engine InvoiceShelf runs before its
/// policies (Gate::before). Abilities reach a user through a role, directly, or through
/// "everyone" permissions, all filtered by the current scope (the company).
public class Bouncer(ApplicationDbContext db, CompanyContext context)
{
    /// Morph aliases from InvoiceShelf's ModelIdentityMap.
    public const string UserType = "user";
    public const string RoleType = "bouncer_role";

    public const string Unauthorized = "This action is unauthorized.";

    /// Gate::authorize(): Bouncer decides first — an explicit forbid denies, an allow
    /// grants — and only when it has no opinion does the policy decide.
    public async Task<bool> AllowsAsync(string ability, BouncerModel? model, Func<bool> policy)
        => await CheckAsync(context.User.Id, ability, model) ?? policy();

    /// Clipboard::checkGetId(): false when forbidden, true when allowed, null when Bouncer
    /// has no ability on file for the request.
    public async Task<bool?> CheckAsync(int userId, string ability, BouncerModel? model)
    {
        if (await HasAbilityQuery(userId, ability, model, allowed: false).AnyAsync())
            return false;
        if (await HasAbilityQuery(userId, ability, model, allowed: true).AnyAsync())
            return true;
        return null;
    }

    private IQueryable<Ability> HasAbilityQuery(int userId, string ability, BouncerModel? model, bool allowed)
    {
        var query = ForAuthority(userId, allowed);

        if (model is null || model.OwnerUserId != userId)
            query = query.Where(a => !a.OnlyOwned);

        // constrainToSimpleAbility()
        if (model is null)
            return query.Where(a =>
                (a.Name == ability && a.EntityType == null) ||
                (a.Name == "*" && (a.EntityType == null || a.EntityType == "*")));

        // byName() non-strict, then forModel() non-strict.
        var names = ability == "*" ? new[] { "*" } : new[] { ability, "*" };
        query = query.Where(a => names.Contains(a.Name));

        if (model.Type == "*")
            return query.Where(a => a.EntityType == "*");

        return model.Id is { } id
            ? query.Where(a => a.EntityType == "*" || (a.EntityType == model.Type && (a.EntityId == null || a.EntityId == id)))
            : query.Where(a => a.EntityType == "*" || (a.EntityType == model.Type && a.EntityId == null));
    }

    /// Queries\Abilities::forAuthority().
    private IQueryable<Ability> ForAuthority(int userId, bool allowed)
    {
        var forbidden = !allowed;
        var scope = context.BouncerScope;

        return db.Abilities.Where(a =>
            // Through a role the user holds.
            db.Roles.Any(r =>
                (r.Scope == null || r.Scope == scope) &&
                db.Permissions.Any(p =>
                    p.EntityId == r.Id && p.AbilityId == a.Id && p.Forbidden == forbidden &&
                    p.EntityType == RoleType && (p.Scope == null || p.Scope == scope)) &&
                db.AssignedRoles.Any(ar =>
                    ar.RoleId == r.Id && ar.EntityType == UserType && ar.EntityId == userId &&
                    (ar.Scope == null || ar.Scope == scope)))
            // Granted to the user directly.
            || ((a.Scope == null || a.Scope == scope) && db.Permissions.Any(p =>
                    p.EntityId == userId && p.AbilityId == a.Id && p.Forbidden == forbidden &&
                    p.EntityType == UserType && (p.Scope == null || p.Scope == scope)))
            // Granted to everyone.
            || db.Permissions.Any(p =>
                    p.AbilityId == a.Id && p.Forbidden == forbidden && p.EntityId == null &&
                    (p.Scope == null || p.Scope == scope)));
    }

    /// Bouncer::sync($user)->roles($names): the user's roles in the current scope become
    /// exactly the named ones that exist. Missing roles are not created (getRoleKeys()).
    public async Task SyncRolesAsync(int userId, params string[] roleNames)
    {
        var scope = context.BouncerScope;
        var wanted = await db.Roles
            .Where(r => roleNames.Contains(r.Name) && (r.Scope == null || r.Scope == scope))
            .Select(r => r.Id)
            .ToListAsync();

        var assigned = db.AssignedRoles.Where(ar =>
            ar.EntityType == UserType && ar.EntityId == userId && (ar.Scope == null || ar.Scope == scope));
        var current = await assigned.Select(ar => ar.RoleId).ToListAsync();

        var detach = current.Except(wanted).ToList();
        if (detach.Count > 0)
            await assigned.Where(ar => detach.Contains(ar.RoleId)).ExecuteDeleteAsync();

        foreach (var roleId in wanted.Except(current))
            db.AssignedRoles.Add(new AssignedRole { RoleId = roleId, EntityId = userId, EntityType = UserType, Scope = scope });
        await db.SaveChangesAsync();
    }
}

/// The model an ability is checked against: its morph alias, its key when it exists,
/// and the user id it belongs to when it has one (Bouncer's `user_id` ownership).
public record BouncerModel(string Type, long? Id = null, int? OwnerUserId = null)
{
    public static BouncerModel Of(Company company) => new("company", company.Id);
}
