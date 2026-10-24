using System.Net;
using System.Net.Http.Json;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Company settings and ownership, including the Bouncer-before-policy authorization.
public class CompanySettingsApiTests : ApiTestBase
{
    private Task<HttpResponseMessage> WriteSettings(object settings)
        => Client.PostAsJsonAsync("/api/v1/company/settings", new { settings });

    [Fact]
    public async Task Owner_writes_settings_and_reads_back_only_visible_ones()
    {
        await SignIn(company: CompanyA);

        Assert.True((await Json(await WriteSettings(new { language = "fr", fiscal_year = "1-12" }))).GetProperty("success").GetBoolean());
        await WithDb(async db =>
        {
            db.CompanySettings.Add(new CompanySetting { CompanyId = CompanyA, Option = "company_mail_host", Value = "smtp" });
            await db.SaveChangesAsync();
        });

        var settings = await GetJson("/api/v1/company/settings?settings[]=language&settings[]=company_mail_host");
        Assert.Equal("fr", settings.GetProperty("language").GetString());
        Assert.False(settings.TryGetProperty("company_mail_host", out _)); // the mail transport has its own page
    }

    [Fact]
    public async Task A_member_without_abilities_falls_through_to_the_owner_policy()
    {
        await SignIn("bob@example.com", CompanyA);

        var response = await WriteSettings(new { language = "fr" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("This action is unauthorized.", (await Json(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_role_ability_in_the_company_scope_allows_a_member()
    {
        await Grant(roleScope: CompanyA, ability: "manage company", entityType: "company");
        await SignIn("bob@example.com", CompanyA);

        Assert.Equal(HttpStatusCode.OK, (await WriteSettings(new { language = "fr" })).StatusCode);
    }

    [Fact]
    public async Task A_role_scoped_to_another_company_does_not_apply()
    {
        await Grant(roleScope: CompanyB, ability: "manage company", entityType: "company");
        await SignIn("bob@example.com", CompanyA);

        Assert.Equal(HttpStatusCode.Forbidden, (await WriteSettings(new { language = "fr" })).StatusCode);
    }

    [Fact]
    public async Task The_everything_wildcard_allows_a_member()
    {
        await WithDb(async db =>
        {
            var everything = new Ability { Name = "*", EntityType = "*" };
            db.Abilities.Add(everything);
            await db.SaveChangesAsync();
            db.Permissions.Add(new Permission { AbilityId = everything.Id, EntityId = Bob, EntityType = "user", Scope = CompanyA });
            await db.SaveChangesAsync();
        });
        await SignIn("bob@example.com", CompanyA);

        Assert.Equal(HttpStatusCode.OK, (await WriteSettings(new { language = "fr" })).StatusCode);
    }

    [Fact]
    public async Task An_explicit_forbid_beats_the_owner_policy()
    {
        await WithDb(async db =>
        {
            var ability = new Ability { Name = "manage company", EntityType = "company" };
            db.Abilities.Add(ability);
            await db.SaveChangesAsync();
            db.Permissions.Add(new Permission { AbilityId = ability.Id, EntityId = Jane, EntityType = "user", Forbidden = true, Scope = CompanyA });
            await db.SaveChangesAsync();
        });
        await SignIn(company: CompanyA);

        Assert.Equal(HttpStatusCode.Forbidden, (await WriteSettings(new { language = "fr" })).StatusCode);
    }

    [Fact]
    public async Task The_currency_freezes_once_the_company_has_transactions()
    {
        await SignIn(company: CompanyA);
        await WriteSettings(new { currency = "1" });
        Assert.False((await GetJson("/api/v1/company/has-transactions")).GetProperty("has_transactions").GetBoolean());

        await WithDb(async db =>
        {
            db.Customers.Add(new Customer { Name = "Client", CompanyId = CompanyA });
            await db.SaveChangesAsync();
        });
        Assert.True((await GetJson("/api/v1/company/has-transactions")).GetProperty("has_transactions").GetBoolean());

        // PHP's strict !==: the number 1 is not the stored string "1", so it counts as a move.
        var moved = await Json(await WriteSettings(new { currency = 1 }));
        Assert.False(moved.GetProperty("success").GetBoolean());
        Assert.Equal("Cannot update company currency after transactions are created.", moved.GetProperty("message").GetString());

        Assert.True((await Json(await WriteSettings(new { currency = "1", language = "fr" }))).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Ownership_moves_only_to_a_member_who_then_holds_the_owner_role()
    {
        await SignIn(company: CompanyA);

        var stranger = await Json(await Client.PostAsync($"/api/v1/transfer/ownership/{await Outsider()}", null));
        Assert.False(stranger.GetProperty("success").GetBoolean());
        Assert.Equal("User does not belong to this company.", stranger.GetProperty("message").GetString());

        var missing = await Client.PostAsync("/api/v1/transfer/ownership/9999", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        Assert.True((await Json(await Client.PostAsync($"/api/v1/transfer/ownership/{Bob}", null))).GetProperty("success").GetBoolean());
        await WithDb(async db =>
        {
            Assert.Equal(Bob, (await db.Companies.SingleAsync(c => c.Id == CompanyA)).OwnerId);
            Assert.True(await db.AssignedRoles.AnyAsync(ar => ar.EntityId == Bob && ar.RoleId == OwnerRoleA && ar.Scope == CompanyA));
            // The outgoing owner keeps their role assignment.
            Assert.True(await db.AssignedRoles.AnyAsync(ar => ar.EntityId == Jane && ar.RoleId == OwnerRoleA));
        });
    }

    private async Task Grant(int roleScope, string ability, string entityType)
    {
        await WithDb(async db =>
        {
            var role = new Role { Name = "manager", Title = "Manager", Scope = roleScope };
            var grant = new Ability { Name = ability, EntityType = entityType };
            db.AddRange(role, grant);
            await db.SaveChangesAsync();
            db.Permissions.Add(new Permission { AbilityId = grant.Id, EntityId = role.Id, EntityType = "bouncer_role", Scope = roleScope });
            db.AssignedRoles.Add(new AssignedRole { RoleId = role.Id, EntityId = Bob, EntityType = "user", Scope = roleScope });
            await db.SaveChangesAsync();
        });
    }

    private async Task<int> Outsider()
    {
        var id = 0;
        await WithDb(async db =>
        {
            var user = new Factos.Data.ApplicationUser { UserName = "out@example.com", Email = "out@example.com", Name = "Out" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            id = user.Id;
        });
        return id;
    }
}
