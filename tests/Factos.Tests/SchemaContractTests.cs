using System.Text.Json;
using Factos.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Factos.Tests;

/// Asserts the EF model still matches InvoiceShelf's schema, table by table and column
/// by column. upstream-schema.json is generated from upstream's migrations; a renamed
/// property or a changed nullability fails here instead of silently breaking API clients.
public class SchemaContractTests
{
    private static readonly JsonDocument Upstream =
        JsonDocument.Parse(File.ReadAllText("upstream-schema.json"));

    private static IModel Model(DbContextOptionsBuilder<SqliteDbContext> options) =>
        new SqliteDbContext(options.UseSqlite("DataSource=:memory:").Options).Model;

    [Fact]
    public void Model_covers_every_upstream_table_and_column()
    {
        var model = Model(new DbContextOptionsBuilder<SqliteDbContext>());
        var tables = model.GetEntityTypes()
            .ToDictionary(e => e.GetTableName()!, e => e.GetProperties()
                .ToDictionary(p => p.GetColumnName(), p => p));

        var missingTables = new List<string>();
        var columnProblems = new List<string>();

        foreach (var table in Upstream.RootElement.EnumerateObject())
        {
            if (!tables.TryGetValue(table.Name, out var columns))
            {
                missingTables.Add(table.Name);
                continue;
            }

            foreach (var column in table.Value.EnumerateObject())
            {
                if (!columns.TryGetValue(column.Name, out var property))
                {
                    columnProblems.Add($"{table.Name}.{column.Name} is missing");
                    continue;
                }

                var expectedNullable = column.Value.GetProperty("nullable").GetBoolean();
                if (property.IsNullable != expectedNullable)
                    columnProblems.Add(
                        $"{table.Name}.{column.Name} nullable={property.IsNullable}, upstream={expectedNullable}");
            }
        }

        Assert.Empty(missingTables);
        Assert.Empty(columnProblems);
    }

    [Fact]
    public void Upstream_fixture_is_not_empty()
    {
        // Guards against the test passing because the fixture failed to load.
        Assert.True(Upstream.RootElement.EnumerateObject().Count() >= 30);
    }
}
