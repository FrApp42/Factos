using AnthoDingo.Setup;
using Factos.Data.Contexts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Factos.Data;

public enum DatabaseProvider { Sqlite, Postgres, MySql }

/// Short-lived contexts for Blazor components: a circuit lives as long as the browser
/// tab, far too long for one DbContext, so each operation creates and disposes its own.
public sealed class AppDbFactory(Func<ApplicationDbContext> create)
{
    public ApplicationDbContext Create() => create();
}

public static class DatabaseProviderExtensions
{
    /// Registers the context for the configured provider and aliases ApplicationDbContext
    /// to it, so nothing outside this file needs to know which provider is in use.
    /// Both keys are what the /setup wizard writes to appsettings.local.json.
    public static DatabaseProvider AddFactosDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<DatabaseProvider?>("Setup:Provider") ?? DatabaseProvider.Sqlite;
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                Register<SqliteDbContext>(services, o => o.UseSqlite(connectionString));
                break;
            case DatabaseProvider.Postgres:
                Register<PostgresDbContext>(services, o => o.UseNpgsql(connectionString));
                break;
            case DatabaseProvider.MySql:
                Register<MySqlDbContext>(services, o => o.UseMySQL(connectionString));
                break;
            default:
                throw new InvalidOperationException($"Unsupported provider '{provider}'.");
        }
        return provider;
    }

    /// The drivers the /setup wizard offers (AnthoDingo.Setup): one per supported provider,
    /// each already brought in by its EF Core package. The first is preselected.
    public static void AddSetupDrivers(SetupOptions o)
    {
        o.Providers[DbProvider.Sqlite] = SqliteFactory.Instance;
        o.Providers[DbProvider.Postgres] = NpgsqlFactory.Instance;
        o.Providers[DbProvider.MySql] = MySqlClientFactory.Instance;
    }

    /// A context outside DI, for the wizard: the configuration is not written yet.
    public static ApplicationDbContext CreateContext(DbProvider provider, string connectionString) => provider switch
    {
        DbProvider.Sqlite => new SqliteDbContext(new DbContextOptionsBuilder<SqliteDbContext>().UseSqlite(connectionString).Options),
        DbProvider.Postgres => new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options),
        DbProvider.MySql => new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().UseMySQL(connectionString).Options),
        _ => throw new InvalidOperationException($"Unsupported provider '{provider}'."),
    };

    /// The InvoiceShelf database to import from (Paramètres › Import InvoiceShelf). SQLite is
    /// opened read-only: a mistyped path must fail, not create an empty database.
    public static string SourceConnectionString(DbProvider provider, string hostOrPath, int port, string database, string user, string password) => provider switch
    {
        DbProvider.Sqlite => new SqliteConnectionStringBuilder { DataSource = hostOrPath, Mode = SqliteOpenMode.ReadOnly }.ToString(),
        DbProvider.Postgres => new NpgsqlConnectionStringBuilder { Host = hostOrPath, Port = port, Database = database, Username = user, Password = password }.ToString(),
        DbProvider.MySql => new MySqlConnectionStringBuilder { Server = hostOrPath, Port = (uint)port, Database = database, UserID = user, Password = password }.ToString(),
        _ => throw new InvalidOperationException($"Unsupported provider '{provider}'."),
    };

    /// After rows were inserted with explicit ids: PostgreSQL's identity sequences do not
    /// follow them (SQLite and MySQL do), so the next insert would reuse an id.
    public static async Task SyncIdentitiesAsync(this ApplicationDbContext db, IEnumerable<string> tables, CancellationToken ct = default)
    {
        if (db is not PostgresDbContext)
            return;
        foreach (var table in tables)
            await db.Database.ExecuteSqlRawAsync($"""
                SELECT setval(pg_get_serial_sequence('"{table}"', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM "{table}"
                """, ct);
    }

    private static void Register<T>(IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where T : ApplicationDbContext
    {
        services.AddDbContextFactory<T>(configure);
        // Per-request context for controllers and Identity; disposed with the scope.
        services.AddScoped<ApplicationDbContext>(sp => sp.GetRequiredService<IDbContextFactory<T>>().CreateDbContext());
        services.AddSingleton(sp => new AppDbFactory(() => sp.GetRequiredService<IDbContextFactory<T>>().CreateDbContext()));
    }
}
