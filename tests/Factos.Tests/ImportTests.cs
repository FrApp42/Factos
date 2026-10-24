using AnthoDingo.Setup;
using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// InvoiceShelfImport: the base fixture's database, reshaped into InvoiceShelf's columns,
/// copied into a freshly installed Factos.
public class ImportTests : ApiTestBase
{
    private readonly string _targetPath = Path.Combine(Path.GetTempPath(), $"factos-test-{Guid.NewGuid():N}.db");

    private int _invoiceId;

    /// The fixture's database in InvoiceShelf's shape, plus a customer (with its address),
    /// an item and an invoice whose line uses that item.
    private async Task<string> BuildSourceAsync()
    {
        string source = null!;
        await WithDb(async db =>
        {
            source = db.Database.GetConnectionString()!;
            var jane = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = await db.Currencies.SingleAsync(c => c.Code == "EUR");
            var company = await Companies.CreateAsync(db, jane, "Ventes SA", eur.Id);
            var customer = new Customer { Name = "Client", CompanyId = company.Id, CurrencyId = eur.Id };
            var item = new Item { Name = "Conseil", Price = 12000, CompanyId = company.Id };
            db.AddRange(customer, item);
            await db.SaveChangesAsync();
            db.Addresses.Add(new Address { CustomerId = customer.Id, CompanyId = company.Id, City = "Lyon", Type = "billing" });
            var invoice = new Invoice { CompanyId = company.Id, CustomerId = customer.Id, InvoiceNumber = "INV-000001", Total = 12000,
                InvoiceDate = new DateTime(2026, 2, 1), Status = "SENT", PaidStatus = "UNPAID", UniqueHash = "h1", CurrencyId = eur.Id };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            _invoiceId = invoice.Id;
            db.InvoiceItems.Add(new InvoiceItem { InvoiceId = invoice.Id, ItemId = item.Id, CompanyId = company.Id, Name = "Conseil", Price = 12000, Quantity = 1, Total = 12000 });
            await db.SaveChangesAsync();

            // InvoiceShelf's shape: Laravel's column names, a bcrypt password, no Identity data.
            var bcrypt = BCrypt.Net.BCrypt.HashPassword("laravel-pass", BCrypt.Net.BCrypt.GenerateSalt(10, 'y'));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE users RENAME COLUMN password_hash TO password");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE personal_access_tokens RENAME COLUMN token_hash TO token");
            await db.Database.ExecuteSqlRawAsync("UPDATE users SET user_name = NULL, normalized_email = NULL, password = {0} WHERE id = {1}", bcrypt, Jane);
        });
        return source;
    }

    /// A fresh install: schema and the wizard's admin.
    private async Task<string> NewInstallAsync()
    {
        var target = $"DataSource={_targetPath}";
        var installer = new Installer();
        await installer.InitializeDatabaseAsync(DbProvider.Sqlite, target);
        await installer.CreateAdminAsync(DbProvider.Sqlite, target, new AdminAccount("root@example.com", "long-enough"));
        return target;
    }

    [Fact]
    public async Task Imports_an_invoiceshelf_database_into_a_fresh_install()
    {
        var source = await BuildSourceAsync();
        var target = await NewInstallAsync();
        var counts = await InvoiceShelfImport.AnalyzeAsync(DbProvider.Sqlite, source);
        Assert.Equal((3L, 2L, 1L, 1L, 0L), (counts["companies"], counts["users"], counts["customers"], counts["invoices"], counts["payments"]));

        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            var report = await InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite, source);
            Assert.Contains("invoices : 1", report);
            Assert.Contains("user_logins : 0", report);

        }

        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            Assert.Equal(["Bob", "Jane"], await db.Users.OrderBy(u => u.Name).Select(u => u.Name).ToListAsync());   // root is gone
            var jane = await db.Users.SingleAsync(u => u.Id == Jane);
            Assert.Equal(("Jane@Example.com", "JANE@EXAMPLE.COM"), (jane.UserName, jane.NormalizedEmail));
            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,
                new LaravelPasswordHasher().VerifyHashedPassword(jane, jane.PasswordHash!, "laravel-pass"));
            Assert.Equal(PasswordVerificationResult.Failed,
                new LaravelPasswordHasher().VerifyHashedPassword(jane, jane.PasswordHash!, "wrong-pass"));

            var invoice = await db.Invoices.SingleAsync();
            Assert.Equal((_invoiceId, "INV-000001", 12000L, new DateTime(2026, 2, 1)), (invoice.Id, invoice.InvoiceNumber, (long)invoice.Total, invoice.InvoiceDate));
            Assert.Equal(new DateTime(2026, 3, 4, 10, 20, 30), (await db.Companies.SingleAsync(c => c.Id == CompanyA)).CreatedAt);
            Assert.Equal(3, await db.Companies.CountAsync());
            Assert.True(await db.AssignedRoles.AnyAsync(r => r.EntityId == Jane && r.Scope == CompanyA));

            // A second import would wipe real data.
            await Assert.ThrowsAsync<InvalidOperationException>(() => InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite, source));
        }
    }

    [Fact]
    public async Task Imports_only_the_chosen_categories_and_what_they_need()
    {
        var source = await BuildSourceAsync();
        var target = await NewInstallAsync();
        Assert.Equal(["customers", "invoices", "payments"], InvoiceShelfImport.WithRequirements(["payments"]).Order());

        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
            await InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite, source, ["invoices"]);
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            Assert.Equal(1, await db.Customers.CountAsync());           // required by the invoices
            Assert.Equal(0, await db.Items.CountAsync());
            var line = await db.InvoiceItems.SingleAsync();
            Assert.Equal(("Conseil", null), (line.Name, line.ItemId));   // detached from the item left behind
            Assert.Equal(1, await db.Addresses.CountAsync(a => a.CustomerId != null));
        }

        // Without customers, their addresses stay behind; the company keeps its own.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_targetPath);
        target = await NewInstallAsync();
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            var report = await InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite, source, ["expenses"]);
            Assert.Contains(report, l => l.StartsWith("addresses : ") && l.EndsWith("(1 liées à des données non importées)"));
        }
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            Assert.Equal((0, 0, 3), (await db.Customers.CountAsync(), await db.Invoices.CountAsync(), await db.Companies.CountAsync()));
            Assert.False(await db.Addresses.AnyAsync(a => a.CustomerId != null));
        }
    }

    [Fact]
    public async Task A_wrong_source_leaves_the_install_untouched()
    {
        var target = await NewInstallAsync();
        var missing = Path.Combine(Path.GetTempPath(), $"factos-missing-{Guid.NewGuid():N}.db");
        var empty = Path.Combine(Path.GetTempPath(), $"factos-empty-{Guid.NewGuid():N}.db");
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, $"DataSource={empty}"))
            await db.Database.MigrateAsync();

        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
        {
            // Read-only: a mistyped path fails instead of creating an empty database.
            await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite,
                DatabaseProviderExtensions.SourceConnectionString(DbProvider.Sqlite, missing, 0, "", "", "")));
            Assert.False(File.Exists(missing));
            await Assert.ThrowsAsync<InvalidOperationException>(() => InvoiceShelfImport.RunAsync(db, DbProvider.Sqlite,
                DatabaseProviderExtensions.SourceConnectionString(DbProvider.Sqlite, empty, 0, "", "", "")));
        }
        await using (var db = DatabaseProviderExtensions.CreateContext(DbProvider.Sqlite, target))
            Assert.Equal("root@example.com", (await db.Users.SingleAsync()).Email);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(empty);
    }

    [Fact]
    public void Converts_driver_values()
    {
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), InvoiceShelfImport.ConvertValue("2026-01-02 03:04:05", typeof(DateTime?)));
        Assert.Equal(DateTimeKind.Utc, ((DateTime)InvoiceShelfImport.ConvertValue(new DateTime(2026, 1, 2), typeof(DateTime))!).Kind);
        Assert.Equal(new DateOnly(2026, 1, 2), InvoiceShelfImport.ConvertValue("2026-01-02", typeof(DateOnly)));
        Assert.Equal(true, InvoiceShelfImport.ConvertValue(1L, typeof(bool)));
        Assert.Equal(true, InvoiceShelfImport.ConvertValue((sbyte)1, typeof(bool?)));
        Assert.Equal(7L, InvoiceShelfImport.ConvertValue(7UL, typeof(long)));
        Assert.Equal(12.5m, InvoiceShelfImport.ConvertValue(12.5, typeof(decimal?)));
        Assert.Null(InvoiceShelfImport.ConvertValue(DBNull.Value, typeof(int?)));
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_targetPath);
    }
}
