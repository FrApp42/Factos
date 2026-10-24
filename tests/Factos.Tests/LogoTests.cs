using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Factos.Data;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

public class LogoTests : ApiTestBase
{
    // A valid 1×1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public void The_type_comes_from_the_content_not_the_name()
    {
        Assert.Equal(".png", Logos.DetectExtension(Png));
        Assert.Equal(".jpg", Logos.DetectExtension([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(".gif", Logos.DetectExtension("GIF89a"u8));
        Assert.Null(Logos.DetectExtension("<svg xmlns='http://www.w3.org/2000/svg'/>"u8));
    }

    [Fact]
    public async Task A_logo_is_served_publicly_returned_by_the_api_and_removable()
    {
        var logos = Service<Logos>();
        await Assert.ThrowsAsync<SalesException>(() => logos.SaveAsync(CompanyA, "not an image"u8.ToArray()));

        await logos.SaveAsync(CompanyA, Png);
        string hash = "", file = "";
        await WithDb(async db =>
        {
            var company = await db.Companies.SingleAsync(c => c.Id == CompanyA);
            company.UniqueHash = "acme-hash";
            await db.SaveChangesAsync();
            (hash, file) = (company.UniqueHash, company.Logo!);
        });
        Assert.Equal($"{CompanyA}.png", file);

        var served = await Client.GetAsync($"/logos/{hash}");
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await served.Content.ReadAsByteArrayAsync());

        await SignIn(company: CompanyA);
        var companies = await GetJson("/api/v1/companies");
        var acme = companies.GetProperty("data").EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == CompanyA);
        Assert.EndsWith($"/logos/{hash}", acme.GetProperty("logo").GetString());

        await logos.RemoveAsync(CompanyA);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/logos/{hash}")).StatusCode);
        Assert.Null(logos.Read(file));
    }

    [Fact]
    public async Task The_pdf_embeds_the_logo()
    {
        // A PDF with an image is clearly larger than the same PDF without it.
        var d = new DocumentData
        {
            Kind = DocumentData.Payment, Id = 1, Company = new Data.Entities.Company { Name = "Acme" },
            Customer = new Data.Entities.Customer { Name = "Client" }, Settings = [], Number = "PAY-1",
            Date = new DateTime(2026, 1, 1), Status = "PAID", Amount = 100,
        };
        var without = DocumentPdf.Render(d);
        var with = DocumentPdf.Render(d, Png);
        Assert.True(with.Length > without.Length);
        await Task.CompletedTask;
    }
}
