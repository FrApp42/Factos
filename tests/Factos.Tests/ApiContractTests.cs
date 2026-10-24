using System.Text.Json;
using Factos.Api;
using Factos.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Guards the pieces of the Laravel/InvoiceShelf API contract that are easy to break silently.
public class ApiContractTests
{
    [Fact]
    public void Token_plaintext_round_trips_through_its_hash()
    {
        var plaintext = PersonalAccessToken.NewPlaintext();
        Assert.Equal(48, plaintext.Length); // 40 random + crc32b hex, as Sanctum 4
        Assert.Equal(PersonalAccessToken.Crc32b(System.Text.Encoding.ASCII.GetBytes(plaintext[..40])).ToString("x8"), plaintext[40..]);
        Assert.Equal(0x3610a686u, PersonalAccessToken.Crc32b("hello"u8)); // PHP: hash('crc32b', 'hello')
        Assert.Equal(64, PersonalAccessToken.Hash(plaintext).Length);
        Assert.Equal(PersonalAccessToken.Hash(plaintext), PersonalAccessToken.Hash(plaintext));
        Assert.NotEqual(PersonalAccessToken.Hash(plaintext), PersonalAccessToken.Hash(PersonalAccessToken.NewPlaintext()));
        // Sanctum's reference hash for a known value.
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", PersonalAccessToken.Hash("hello"));
    }

    [Fact]
    public void Validation_error_uses_laravels_422_envelope()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("CustomerName", "The customer name field is required.");

        var result = (Microsoft.AspNetCore.Mvc.ObjectResult)LaravelResponse.ValidationError(modelState);
        Assert.Equal(422, result.StatusCode);

        var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement;
        Assert.Equal("The customer name field is required.", json.GetProperty("message").GetString());
        Assert.Equal(
            "The customer name field is required.",
            json.GetProperty("errors").GetProperty("customer_name")[0].GetString());
    }

    [Theory]
    [InlineData("d M Y", "04 Mar 2026")]
    [InlineData("Y/m/d", "2026/03/04")]
    [InlineData("D, jS F y g:i A", "Wed, 4th March 26 9:05 PM")]
    [InlineData(@"\Y\e\a\r: Y", "Year: 2026")]
    public void Php_date_formats_render_like_carbon(string format, string expected)
        => Assert.Equal(expected, PhpDate.Format(new DateTime(2026, 3, 4, 21, 5, 9), format));

    [Fact]
    public async Task Pagination_matches_laravels_length_aware_paginator()
    {
        await using var db = NewContext();
        for (var i = 1; i <= 25; i++)
            db.PersonalAccessTokens.Add(new PersonalAccessToken { Name = $"token-{i}", TokenHash = PersonalAccessToken.Hash(PersonalAccessToken.NewPlaintext()) });
        await db.SaveChangesAsync();

        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString("localhost");
        request.Path = "/api/v1/tokens";
        request.QueryString = new QueryString("?page=2&limit=10");

        var json = JsonDocument.Parse(JsonSerializer.Serialize(
            await LaravelResponse.PaginateAsync(db.PersonalAccessTokens.OrderBy(t => t.Id), request))).RootElement;

        Assert.Equal(10, json.GetProperty("data").GetArrayLength());
        var meta = json.GetProperty("meta");
        Assert.Equal(2, meta.GetProperty("current_page").GetInt32());
        Assert.Equal(11, meta.GetProperty("from").GetInt32());
        Assert.Equal(20, meta.GetProperty("to").GetInt32());
        Assert.Equal(3, meta.GetProperty("last_page").GetInt32());
        Assert.Equal(25, meta.GetProperty("total").GetInt32());
        Assert.Equal("http://localhost/api/v1/tokens?page=3", json.GetProperty("links").GetProperty("next").GetString());
        Assert.Equal("http://localhost/api/v1/tokens?page=1", json.GetProperty("links").GetProperty("prev").GetString());
    }

    [Fact]
    public async Task Every_provider_builds_the_same_model()
    {
        // A model that only one provider can map is a migration that will fail on the others.
        var entityCounts = new[]
        {
            NewContext().Model.GetEntityTypes().Count(),
            new Factos.Data.Contexts.PostgresDbContext(
                new DbContextOptionsBuilder<Factos.Data.Contexts.PostgresDbContext>()
                    .UseNpgsql("Host=localhost;Database=factos").Options).Model.GetEntityTypes().Count(),
            new Factos.Data.Contexts.MySqlDbContext(
                new DbContextOptionsBuilder<Factos.Data.Contexts.MySqlDbContext>()
                    .UseMySQL("Server=localhost;Database=factos;User=root").Options).Model.GetEntityTypes().Count(),
        };
        Assert.Single(entityCounts.Distinct());
        await Task.CompletedTask;
    }

    private static Factos.Data.Contexts.SqliteDbContext NewContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new Factos.Data.Contexts.SqliteDbContext(
            new DbContextOptionsBuilder<Factos.Data.Contexts.SqliteDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }
}
