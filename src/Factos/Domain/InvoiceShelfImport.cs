using Factos.Components;
using System.Data.Common;
using System.Globalization;
using AnthoDingo.Setup;
using Factos.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Factos.Domain;

/// What the user picks from (Paramètres › Import InvoiceShelf). `Count` is the table whose
/// rows are shown; tables in no category (accounts, companies, settings, taxes, units…) are
/// always imported. A table may belong to several categories (lines and taxes).
public sealed record ImportCategory(string Key, string Label, string Count, string[] Tables, string[] Requires);

/// Copies an InvoiceShelf database into a freshly installed Factos one. The schemas share
/// table and column names, so every table Factos models is read with `SELECT *` and its
/// matching columns copied, ids included. Tables Factos does not model (media, jobs, oauth…)
/// are left behind, as are files: logos must be uploaded again.
public static class InvoiceShelfImport
{
    public static readonly ImportCategory[] Categories =
    [
        new("customers", "Clients", "customers", ["customers"], []),
        new("items", "Articles", "items", ["items", "taxes"], []),
        new("estimates", "Devis", "estimates", ["estimates", "estimate_items", "taxes"], ["customers"]),
        new("invoices", "Factures", "invoices", ["invoices", "invoice_items", "taxes"], ["customers"]),
        new("recurring", "Factures récurrentes", "recurring_invoices", ["recurring_invoices", "recurrence_occurrences", "invoice_items", "taxes"], ["customers"]),
        new("payments", "Paiements", "payments", ["payments", "payment_allocations", "transactions"], ["customers", "invoices"]),
        new("expenses", "Dépenses", "expenses", ["expenses"], []),
    ];

    // Factos column -> InvoiceShelf column, where the two differ.
    private static readonly Dictionary<(string Table, string Column), string> Renamed = new()
    {
        [("users", "password_hash")] = "password",
        [("personal_access_tokens", "token_hash")] = "token",   // both a SHA-256 hex digest
    };

    // A row whose foreign key points to a row left behind is skipped (a customer's address,
    // an invoice's taxes…), except for these links, which are cut: the row stands on its own.
    private static readonly HashSet<(string Table, string Column)> Detachable =
    [
        ("invoice_items", "item_id"), ("estimate_items", "item_id"),
        ("invoices", "recurring_invoice_id"), ("payments", "transaction_id"),
    ];

    /// The selection plus what it needs: no invoice without its customer.
    public static HashSet<string> WithRequirements(IEnumerable<string> keys)
    {
        var selected = keys.ToHashSet();
        foreach (var category in Categories.Where(c => selected.Contains(c.Key)))
            selected.UnionWith(category.Requires);
        return selected;
    }

    /// Opens the source and counts its rows per category, plus "companies" and "users".
    /// Fails like the import would when the source is unreachable or not InvoiceShelf.
    public static async Task<Dictionary<string, long>> AnalyzeAsync(DbProvider sourceProvider, string sourceConnection, CancellationToken ct = default)
    {
        await using var source = DatabaseProviderExtensions.CreateContext(sourceProvider, sourceConnection);
        var connection = source.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        var quote = source.GetService<ISqlGenerationHelper>();

        async Task<long> Count(string table)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {quote.DelimitIdentifier(table)}";
            try { return Convert.ToInt64(await command.ExecuteScalarAsync(ct)); }
            catch (DbException) { return 0; }
        }

        var counts = new Dictionary<string, long> { ["companies"] = await Count("companies"), ["users"] = await Count("users") };
        if (counts["companies"] == 0)
            throw new InvalidOperationException("La source ne contient aucune société : ce n'est pas une base InvoiceShelf.");
        foreach (var category in Categories)
            counts[category.Key] = await Count(category.Count);
        return counts;
    }

    /// Replaces everything in `target` with the source's data, limited to the chosen
    /// categories (all when null). Refuses a Factos database that already holds business
    /// data: only what /setup creates (admin, company) is discarded.
    public static async Task<List<string>> RunAsync(ApplicationDbContext target, DbProvider sourceProvider, string sourceConnection,
        IEnumerable<string>? categories = null, CancellationToken ct = default)
    {
        if ((await target.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Le schéma Factos n'est pas à jour : appliquer d'abord les mises à jour (/update).");
        if (await target.Customers.AnyAsync(ct) || await target.Invoices.AnyAsync(ct) || await target.Estimates.AnyAsync(ct)
            || await target.Payments.AnyAsync(ct) || await target.Expenses.AnyAsync(ct) || await target.Items.AnyAsync(ct))
            throw new InvalidOperationException("La base Factos contient déjà des données : l'import se fait dans une installation neuve.");

        var chosen = WithRequirements(categories ?? Categories.Select(c => c.Key));
        var optional = Categories.SelectMany(c => c.Tables).ToHashSet();
        var wanted = Categories.Where(c => chosen.Contains(c.Key)).SelectMany(c => c.Tables).ToHashSet();

        await using var source = DatabaseProviderExtensions.CreateContext(sourceProvider, sourceConnection);
        var connection = source.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        var quoteSource = source.GetService<ISqlGenerationHelper>();
        var quoteTarget = target.GetService<ISqlGenerationHelper>();

        var tables = InDependencyOrder(target.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.GetTableName() is not null).ToList());
        var report = new List<string>();
        var imported = new Dictionary<IEntityType, HashSet<object>>();   // keys copied so far, per table
        target.ChangeTracker.AutoDetectChangesEnabled = false;

        await using var tx = await target.Database.BeginTransactionAsync(ct);
        foreach (var table in Enumerable.Reverse(tables))
            await target.Database.ExecuteSqlRawAsync($"DELETE FROM {quoteTarget.DelimitIdentifier(table.GetTableName()!)}", ct);

        foreach (var table in tables)
        {
            var name = table.GetTableName()!;
            if (optional.Contains(name) && !wanted.Contains(name))
                continue;
            var primaryKey = table.FindPrimaryKey()!.Properties;
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {quoteSource.DelimitIdentifier(name)} ORDER BY {string.Join(", ", primaryKey.Select(p => quoteSource.DelimitIdentifier(p.GetColumnName())))}";
            DbDataReader reader;
            try { reader = await command.ExecuteReaderAsync(ct); }
            catch (DbException) { report.Add($"{name} : absente de la source"); continue; }

            await using (reader)
            {
                var columns = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => i, StringComparer.OrdinalIgnoreCase);
                var mapped = table.GetProperties()
                    .Select(p => (Property: p, Column: Renamed.GetValueOrDefault((name, p.GetColumnName()), p.GetColumnName())))
                    .Where(m => columns.ContainsKey(m.Column))
                    .Select(m => (m.Property, Ordinal: columns[m.Column]))
                    .ToList();
                var links = table.GetForeignKeys().Where(fk => fk.PrincipalEntityType != table && fk.Properties.Count == 1)
                    .Select(fk => new Link(fk.Properties[0], fk.PrincipalEntityType, fk.PrincipalKey.Properties[0].ClrType,
                        Detachable.Contains((name, fk.Properties[0].GetColumnName()))))
                    .ToList();
                var keys = imported[table] = [];

                int count = 0, skipped = 0;
                while (await reader.ReadAsync(ct))
                {
                    var row = Activator.CreateInstance(table.ClrType)!;
                    foreach (var (property, ordinal) in mapped)
                        property.PropertyInfo!.SetValue(row, ConvertValue(reader.GetValue(ordinal), property.ClrType));
                    if (!KeepLinks(row, links, imported))
                    {
                        skipped++;
                        continue;
                    }
                    if (row is ApplicationUser user)
                        CompleteIdentity(user);
                    if (primaryKey is [var pk])
                        keys.Add(pk.PropertyInfo!.GetValue(row)!);
                    target.Add(row);
                    if (++count % 500 == 0)
                        await FlushAsync(target, ct);
                }
                await FlushAsync(target, ct);
                report.Add(skipped == 0 ? $"{Loc.T(name)} : {count}" : $"{Loc.T(name)} : {count} ({skipped} {Loc.T("liées à des données non importées")})");
            }
        }

        // Nothing came across (wrong database, empty file): keep the install as it was.
        if (!await target.Companies.AnyAsync(ct))
            throw new InvalidOperationException("La source ne contient aucune société : ce n'est pas une base InvoiceShelf.");
        await target.SyncIdentitiesAsync(tables.Where(t => t.FindPrimaryKey()!.Properties is [{ Name: "Id" }])
            .Select(t => t.GetTableName()!), ct);
        await tx.CommitAsync(ct);
        target.ChangeTracker.AutoDetectChangesEnabled = true;
        await DatabaseInitializer.SeedReferenceDataAsync(target);
        return report;
    }

    private sealed record Link(IProperty Property, IEntityType Principal, Type KeyType, bool Detachable);

    /// False when the row points to a row that was not imported; detachable links are cut instead.
    private static bool KeepLinks(object row, List<Link> links, Dictionary<IEntityType, HashSet<object>> imported)
    {
        foreach (var link in links)
        {
            var value = link.Property.PropertyInfo!.GetValue(row);
            if (value is null || (imported.TryGetValue(link.Principal, out var keys) && keys.Contains(Convert.ChangeType(value, link.KeyType))))
                continue;
            if (!link.Detachable)
                return false;
            link.Property.PropertyInfo.SetValue(row, null);
        }
        return true;
    }

    private static async Task FlushAsync(ApplicationDbContext db, CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// Identity's columns have no upstream counterpart. The bcrypt hash stays in
    /// password_hash: LaravelPasswordHasher accepts it and rehashes at the first sign-in.
    private static void CompleteIdentity(ApplicationUser user)
    {
        user.UserName ??= user.Email ?? $"user{user.Id}";
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.NormalizedEmail = user.Email?.ToUpperInvariant();
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.LockoutEnabled = true;
    }

    /// Principals before dependents, following the model's foreign keys (self-references
    /// are handled by reading rows in key order).
    private static List<IEntityType> InDependencyOrder(List<IEntityType> types)
    {
        var ordered = new List<IEntityType>();
        var pending = types.ToHashSet();
        while (pending.Count > 0)
        {
            var ready = pending.Where(t => t.GetForeignKeys().All(fk =>
                fk.PrincipalEntityType == t || !pending.Contains(fk.PrincipalEntityType))).ToList();
            if (ready.Count == 0)
                throw new InvalidOperationException("Cycle de clés étrangères : " + string.Join(", ", pending.Select(t => t.GetTableName())));
            ordered.AddRange(ready.OrderBy(t => t.GetTableName()));
            pending.ExceptWith(ready);
        }
        return ordered;
    }

    /// Driver values to the entity's CLR types: SQLite gives text dates and integer booleans,
    /// MySQL unsigned ids and tinyints. Laravel stores timestamps in UTC.
    internal static object? ConvertValue(object value, Type type)
    {
        if (value is DBNull)
            return null;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        var inv = CultureInfo.InvariantCulture;
        return value switch
        {
            DateTime d when t == typeof(DateTime) => DateTime.SpecifyKind(d, DateTimeKind.Utc),
            DateTime d when t == typeof(DateOnly) => DateOnly.FromDateTime(d),
            DateTimeOffset d when t == typeof(DateTime) => d.UtcDateTime,
            TimeSpan s when t == typeof(TimeOnly) => TimeOnly.FromTimeSpan(s),
            string s when t == typeof(DateTime) => DateTime.Parse(s, inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            string s when t == typeof(DateOnly) => DateOnly.FromDateTime(DateTime.Parse(s, inv)),
            string s when t == typeof(TimeOnly) => TimeOnly.Parse(s, inv),
            string s when t == typeof(bool) => s is "1" or "true" or "t",
            string s when t == typeof(decimal) => decimal.Parse(s, NumberStyles.Float, inv),
            _ when t.IsInstanceOfType(value) => value,
            _ => Convert.ChangeType(value, t, inv),
        };
    }
}

/// Accepts the bcrypt hashes of imported InvoiceShelf accounts ($2y$, Laravel's default);
/// Identity then replaces them with its own format at the next successful sign-in.
public sealed class LaravelPasswordHasher : PasswordHasher<ApplicationUser>
{
    public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (!hashedPassword.StartsWith("$2"))
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }
}
