using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AnthoDingo.Update;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Factos.Tests;

/// A migrated SQLite database per test class instance, seeded with:
/// jane — owner of Acme (role "owner" there), member of Beta;
/// bob  — member of Acme with no role.
public abstract class ApiTestBase : IAsyncLifetime
{
    protected const string Password = "Secret-pass1";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"factos-test-{Guid.NewGuid():N}.db");
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"factos-test-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program> _factory = null!;
    protected HttpClient Client = null!;
    protected int CompanyA, CompanyB, Jane, Bob;
    protected long OwnerRoleA;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Setup:IsComplete", "true");
            b.UseSetting("Setup:Provider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", $"DataSource={_dbPath}");
            b.UseSetting("Recurring:Enabled", "false");
            // Files and keys of a test run never touch the developer's own Data/ folder.
            b.UseSetting("Storage:UploadsPath", Path.Combine(_scratch, "uploads"));
            b.UseSetting("DataProtection:KeysPath", Path.Combine(_scratch, "keys"));
        });
        Client = _factory.CreateClient();

        // What an admin confirms on /update: migrations, then reference data.
        using (var migrate = _factory.Services.CreateScope())
            await migrate.ServiceProvider.GetRequiredService<IMigrationManager>().ApplyPendingMigrationsAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = new DateTime(2026, 3, 4, 10, 20, 30, DateTimeKind.Utc);

        var jane = new ApplicationUser { UserName = "jane@example.com", Email = "Jane@Example.com", Name = "Jane", CreatedAt = created, UpdatedAt = created };
        var bob = new ApplicationUser { UserName = "bob@example.com", Email = "bob@example.com", Name = "Bob", CreatedAt = created, UpdatedAt = created };
        Assert.True((await users.CreateAsync(jane, Password)).Succeeded);
        Assert.True((await users.CreateAsync(bob, Password)).Succeeded);
        Jane = jane.Id;
        Bob = bob.Id;

        var a = new Company { Name = "Acme", Slug = "acme", OwnerId = jane.Id, CreatedAt = created, UpdatedAt = created };
        var b = new Company { Name = "Beta", Slug = "beta", CreatedAt = created, UpdatedAt = created };
        db.Companies.AddRange(a, b);
        await db.SaveChangesAsync();
        CompanyA = a.Id;
        CompanyB = b.Id;

        db.UserCompanies.AddRange(
            new UserCompany { UserId = jane.Id, CompanyId = a.Id },
            new UserCompany { UserId = jane.Id, CompanyId = b.Id },
            new UserCompany { UserId = bob.Id, CompanyId = a.Id });
        db.CompanySettings.Add(new CompanySetting { CompanyId = a.Id, Option = "carbon_date_format", Value = "d M Y" });
        var owner = new Role { Name = "owner", Title = "Owner", Scope = a.Id };
        db.Roles.Add(owner);
        await db.SaveChangesAsync();
        OwnerRoleA = owner.Id;
        db.AssignedRoles.Add(new AssignedRole { RoleId = owner.Id, EntityId = jane.Id, EntityType = "user", Scope = a.Id });
        await db.SaveChangesAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    protected T Service<T>() where T : notnull => _factory.Services.GetRequiredService<T>();

    protected async Task WithDb(Func<ApplicationDbContext, Task> action)
        => await InScope(sp => action(sp.GetRequiredService<ApplicationDbContext>()));

    protected async Task InScope(Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    /// Signs in and sends the token (and optionally a company header) on every request.
    protected async Task SignIn(string email = "jane@example.com", int? company = null)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(email));
        Client.DefaultRequestHeaders.Remove("company");
        if (company is not null)
            Client.DefaultRequestHeaders.Add("company", company.ToString());
    }

    protected async Task<string> Login(string username = "jane@example.com", string password = Password)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { username, password, device_name = "phone" });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", json.GetProperty("type").GetString());
        return json.GetProperty("token").GetString()!;
    }

    protected async Task<JsonElement> GetJson(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();
}
