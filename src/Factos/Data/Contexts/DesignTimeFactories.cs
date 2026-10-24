using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Factos.Data.Contexts;

// `dotnet ef` needs each provider context buildable without running the app. Connection
// strings come from appsettings so `dotnet ef database update --context X` hits the real database.
public abstract class DesignTimeFactory
{
    protected static string ConnectionString(string name)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        return config.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' not found.");
    }
}

public class SqliteDesignTimeFactory : DesignTimeFactory, IDesignTimeDbContextFactory<SqliteDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqliteDbContext>().UseSqlite(ConnectionString("Sqlite")).Options);
}

public class PostgresDesignTimeFactory : DesignTimeFactory, IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(ConnectionString("Postgres")).Options);
}

public class MySqlDesignTimeFactory : DesignTimeFactory, IDesignTimeDbContextFactory<MySqlDbContext>
{
    public MySqlDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<MySqlDbContext>().UseMySQL(ConnectionString("MySql")).Options);
}
