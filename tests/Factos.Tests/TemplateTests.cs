using Factos.Domain;

namespace Factos.Tests;

public class TemplateTests : ApiTestBase
{
    [Fact]
    public async Task Templates_are_saved_resolved_by_name_and_rendered()
    {
        var dbs = Service<Factos.Data.AppDbFactory>();
        Assert.Equal("Classique", Assert.Single(await DocumentTemplates.LoadAsync(dbs, CompanyA)).Name);

        var bad = new List<DocumentTemplate> { new() { Name = "A" }, new() { Name = " a " } };
        await Assert.ThrowsAsync<SalesException>(() => DocumentTemplates.SaveAsync(dbs, CompanyA, bad));
        await Assert.ThrowsAsync<SalesException>(() => DocumentTemplates.SaveAsync(dbs, CompanyA, [new() { Accent = "red" }]));

        var compact = new DocumentTemplate
        {
            Name = "Compact", Accent = "#112233", LogoRight = true, FilledTableHeader = true,
            Blocks = [DocumentTemplate.Customer, DocumentTemplate.Header, DocumentTemplate.Intro, DocumentTemplate.Lines],
            IntroText = "Bonjour {CONTACT_DISPLAY_NAME}", FooterText = "SARL {COMPANY_NAME}",
        };
        await DocumentTemplates.SaveAsync(dbs, CompanyA, [new(), compact]);
        var saved = await DocumentTemplates.LoadAsync(dbs, CompanyA);
        Assert.Equal(["Classique", "Compact"], saved.Select(t => t.Name));
        Assert.Equal(compact.Blocks, saved[1].Blocks);

        var sample = await DocumentTemplates.SampleAsync(dbs, Service<Sales>(), CompanyA);
        // Upstream's "invoice1" and unknown names fall back to the default (first) template.
        Assert.Equal("Classique", DocumentTemplates.Resolve(sample.Settings, "invoice1").Name);
        Assert.Equal("Compact", DocumentTemplates.Resolve(sample.Settings, "Compact").Name);

        Assert.Equal("Bonjour Client exemple & co", new DocumentData
        {
            Kind = sample.Kind, Id = 0, Company = sample.Company, Settings = [], Number = "1", Date = sample.Date, Status = "DRAFT",
            Customer = new Factos.Data.Entities.Customer { Name = "Client exemple & co" },
        }.Render(compact.IntroText!, html: false));

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(DocumentPdf.Render(sample, null, saved[1]), 0, 4));
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], DocumentPdf.Preview(sample, null, saved[1]).Take(4));
    }
}
