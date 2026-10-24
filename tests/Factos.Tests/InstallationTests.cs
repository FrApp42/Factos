using System.Net;
using AnthoDingo.Setup;
using Factos.Data;
using Factos.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Factos.Tests;

/// The /setup wizard's database work (AnthoDingo.Setup) and the /update gate (AnthoDingo.Update).
public class InstallationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"factos-test-{Guid.NewGuid():N}.db");
    private string Cs => $"DataSource={_dbPath}";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Wizard_creates_schema_admin_and_company()
    {
        var installer = new Installer();
        await installer.InitializeDatabaseAsync(DbProvider.Sqlite, Cs);
        await installer.CreateAdminAsync(DbProvider.Sqlite, Cs, new AdminAccount("Root@Example.com", "long-enough", "Root"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            installer.CreateAdminAsync(DbProvider.Sqlite, Cs, new AdminAccount("root@example.com", "long-enough")));

        var step = new CompanySetupStep();
        var context = new SetupExtraStepContext("Factos", DbProvider.Sqlite, Cs, "token", null, null);
        Assert.Contains("""<option value="EUR" selected>""", await step.RenderAsync(context, default));
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "Acme", ["currency"] = "EUR", ["time_zone"] = "Europe/Paris",
        });
        Assert.False((await step.HandleAsync(context, new FormCollection(new() { ["name"] = "", ["currency"] = "EUR", ["time_zone"] = "Europe/Paris" }), default)).IsSuccess);
        Assert.True((await step.HandleAsync(context, form, default)).IsSuccess);
        Assert.True((await step.HandleAsync(context, form, default)).IsSuccess); // resubmitted

        await using var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, Cs);
        var admin = await db.Users.SingleAsync();
        Assert.Equal(("Root", "super admin", "ROOT@EXAMPLE.COM"), (admin.Name, admin.Role, admin.NormalizedEmail));
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<ApplicationUser>().VerifyHashedPassword(admin, admin.PasswordHash!, "long-enough"));
        var company = await db.Companies.SingleAsync();
        Assert.Equal(("Acme", admin.Id), (company.Name, company.OwnerId));
        Assert.Equal("Europe/Paris", await db.CompanySettings
            .Where(s => s.CompanyId == company.Id && s.Option == "time_zone").Select(s => s.Value).SingleAsync());
    }

    [Fact]
    public void Each_driver_accepts_the_wizards_connection_strings()
    {
        // The wizard writes its keywords for MySqlConnector; Factos ships Oracle's MySql.Data.
        var options = new SetupOptions();
        DatabaseProviderExtensions.AddSetupDrivers(options);
        var setup = new SetupService(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { ContentRootPath = Path.GetTempPath() },
            null!, Microsoft.Extensions.Options.Options.Create(options),
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SetupService>.Instance);

        Assert.Contains("SslMode=Preferred", setup.BuildMySqlConnectionString("db", 3306, "factos", "u", "p"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SSL Mode=Prefer", setup.BuildPostgresConnectionString("db", 5432, "factos", "u", "p"));
        Assert.Contains(_dbPath, setup.BuildSqliteConnectionString(_dbPath));
    }

    [Fact]
    public async Task Pending_migrations_send_everything_to_update()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Setup:IsComplete", "true");
            b.UseSetting("Setup:Provider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", Cs);
            b.UseSetting("Recurring:Enabled", "false");
        });
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        // An install one release behind: every migration but the last.
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, Cs))
        {
            var all = db.Database.GetMigrations().ToList();
            await db.GetService<IMigrator>().MigrateAsync(all[^2]);
        }

        // Anonymous: pages under [Authorize] go to sign-in first, the gate comes after.
        var page = await client.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/update?returnUrl=%2Fapi%2Fping", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/v1/auth/login", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);

        await new Installer().InitializeDatabaseAsync(DbProvider.Sqlite, Cs);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/ping")).StatusCode);
    }
}

public class UpdatePolicyTests : ApiTestBase
{
    [Fact]
    public async Task Only_a_super_admin_may_apply_migrations()
    {
        await WithDb(db => db.Users.Where(u => u.Id == Bob)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, Installer.SuperAdmin)));
        await InScope(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var claims = sp.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
            var auth = sp.GetRequiredService<IAuthorizationService>();
            async Task<bool> Allowed(int id) => (await auth.AuthorizeAsync(
                await claims.CreateAsync((await users.FindByIdAsync(id.ToString()))!), Installer.SuperAdmin)).Succeeded;

            Assert.True(await Allowed(Bob));
            Assert.False(await Allowed(Jane)); // company owner, but `users.role` = user
        });
    }
}
