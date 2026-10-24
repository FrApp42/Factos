using System.Text.Json;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// The reference data InvoiceShelf ships (currencies, countries), planted after every
/// schema change: by the /setup wizard (Installer), after /update (FactosMigrations), and
/// at startup when the schema is current, since new seed rows ship without a migration.
/// Migrations themselves are never applied at startup: /update asks an admin first.
public static class DatabaseInitializer
{
    public static async Task SeedIfCurrentAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!(await db.Database.GetPendingMigrationsAsync()).Any())
            await SeedReferenceDataAsync(db);
    }

    /// CurrencyService::sync() (insert if absent, by code) and CountriesTableSeeder.
    public static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        var now = Data.Timestamp.Now();
        var known = (await db.Currencies.Select(c => c.Code).ToListAsync()).ToHashSet();
        foreach (var c in Load("currencies.json").Where(c => !known.Contains(c.GetProperty("code").GetString()!)))
            db.Currencies.Add(new Currency
            {
                Code = c.GetProperty("code").GetString()!,
                Name = c.GetProperty("name").GetString()!,
                Symbol = c.GetProperty("symbol").GetString(),
                Precision = c.GetProperty("precision").GetInt32(),
                ThousandSeparator = c.GetProperty("thousand_separator").GetString()!,
                DecimalSeparator = c.GetProperty("decimal_separator").GetString()!,
                SwapCurrencySymbol = c.GetProperty("swap_currency_symbol").GetBoolean(),
                CreatedAt = now,
                UpdatedAt = now,
            });

        if (!await db.Countries.AnyAsync())
            foreach (var c in Load("countries.json"))
                db.Countries.Add(new Country
                {
                    Id = c.GetProperty("id").GetInt32(),
                    Code = c.GetProperty("code").GetString()!,
                    Name = c.GetProperty("name").GetString()!,
                    Phonecode = c.GetProperty("phonecode").GetInt32(),
                });

        await db.SaveChangesAsync();
    }

    private static List<JsonElement> Load(string file)
    {
        using var stream = typeof(DatabaseInitializer).Assembly
            .GetManifestResourceStream($"Factos.Data.Seed.{file}")
            ?? throw new InvalidOperationException($"Embedded seed {file} is missing.");
        return JsonDocument.Parse(stream).RootElement.EnumerateArray().ToList();
    }
}
