using Factos.Components;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Factos.Domain;

/// Server-side PDF of an invoice, estimate or payment receipt (upstream's
/// getGeneratedPDFOrStream), laid out by the document's DocumentTemplate.
public static class DocumentPdf
{
    public static byte[] Render(DocumentData d, byte[]? logo = null, DocumentTemplate? template = null)
        => Build(d, logo, template).GeneratePdf();

    /// First page as PNG, for the template designer's live preview.
    public static byte[] Preview(DocumentData d, byte[]? logo, DocumentTemplate template)
        => Build(d, logo, template).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 80 }).First();

    private static Document Build(DocumentData d, byte[]? logo, DocumentTemplate? template)
    {
        var t = template ?? DocumentTemplates.Resolve(d.Settings, d.TemplateName);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(s => s.FontSize(t.FontSize));

            page.Content().Column(col =>
            {
                col.Spacing(14);
                foreach (var block in t.Blocks)
                    switch (block)
                    {
                        case DocumentTemplate.Header: Header(col, d, t, logo); break;
                        case DocumentTemplate.Customer: Customer(col, d); break;
                        case DocumentTemplate.Intro when !string.IsNullOrWhiteSpace(t.IntroText):
                            col.Item().Text(d.Render(t.IntroText, html: false));
                            break;
                        case DocumentTemplate.Lines when !d.HasLines: PaymentBody(col, d, t); break;
                        case DocumentTemplate.Lines: Lines(col, d, t); break;
                        case DocumentTemplate.Totals when d.HasLines: Totals(col, d); break;
                        case DocumentTemplate.Notes when !string.IsNullOrWhiteSpace(d.Notes):
                            col.Item().Column(c =>
                            {
                                c.Item().Text("Notes").Bold();
                                c.Item().Text(d.Notes);
                            });
                            break;
                    }
            });

            page.Footer().Column(c =>
            {
                if (!string.IsNullOrWhiteSpace(t.FooterText))
                    c.Item().AlignCenter().Text(d.Render(t.FooterText, html: false)).FontSize(t.FontSize - 2).FontColor(Colors.Grey.Darken1);
                if (t.PageNumbers)
                    c.Item().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
            });
        }));
    }

    private static void Header(ColumnDescriptor col, DocumentData d, DocumentTemplate t, byte[]? logo) => col.Item().Row(row =>
    {
        void Company(IContainer box) => box.Column(c =>
        {
            if (logo is not null)
                c.Item().PaddingBottom(6).MaxHeight(60).MaxWidth(180).Image(logo).FitArea();
            if (logo is null || !t.HideCompanyNameWithLogo)
                c.Item().Text(d.Company.Name).FontSize(t.FontSize + 6).Bold();
            foreach (var line in DocumentData.Lines(d.CompanyAddress, d.CompanyCountry))
                c.Item().Text(line);
            if (!string.IsNullOrEmpty(d.Company.VatId))
                c.Item().Text($"TVA : {d.Company.VatId}");
            if (!string.IsNullOrEmpty(d.Company.TaxId))
                c.Item().Text(Loc.F("SIRET : {0}", d.Company.TaxId));
        });

        void Meta(IContainer box) => box.Column(c =>
        {
            c.Item().Text(d.Title.ToUpperInvariant()).FontSize(t.FontSize + 10).FontColor(t.Accent).Bold();
            c.Item().Text(Loc.F("N° {0}", d.Number));
            c.Item().Text($"Date : {d.FormatDate(d.Date)}");
            if (d.DueDate is not null)
                c.Item().Text($"{(d.Kind == DocumentData.Invoice ? Loc.T("Échéance") : Loc.T("Valable jusqu'au"))} : {d.FormatDate(d.DueDate)}");
            if (d.IsCreditNote)
                c.Item().Text($"Avoir sur la facture : {d.RelatedNumber}");
            else if (!string.IsNullOrEmpty(d.Reference))
                c.Item().Text(Loc.F("Réf. : {0}", d.Reference));
            if (!string.IsNullOrEmpty(d.CreditReason))
                c.Item().Text($"Motif : {d.CreditReason}");
            foreach (var (label, value) in d.PrintedFields)
                c.Item().Text($"{label} : {value}");
        });

        // The logo travels with the company block; the document details take the other side.
        if (t.LogoRight)
        {
            Meta(row.RelativeItem());
            Company(row.RelativeItem().AlignRight());
        }
        else
        {
            Company(row.RelativeItem());
            Meta(row.RelativeItem().AlignRight());
        }
    });

    private static void Customer(ColumnDescriptor col, DocumentData d) => col.Item().Column(c =>
    {
        c.Item().Text(d.Kind switch { DocumentData.Payment => Loc.T("Reçu de"), DocumentData.Expense => Loc.T("Client"), _ => Loc.T("Facturé à") }).FontColor(Colors.Grey.Darken1);
        c.Item().Text(d.Customer.Name).Bold();
        foreach (var line in DocumentData.Lines(d.BillingAddress, d.BillingCountry))
            c.Item().Text(line);
        if (!string.IsNullOrEmpty(d.Customer.TaxId))
            c.Item().Text($"TVA : {d.Customer.TaxId}");
    });

    private static void Lines(ColumnDescriptor col, DocumentData d, DocumentTemplate t) => col.Item().Table(table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.RelativeColumn(5);
            c.RelativeColumn(1.2f);
            c.RelativeColumn(2);
            if (d.DiscountPerItem) c.RelativeColumn(1.6f);
            if (d.TaxPerItem) c.RelativeColumn(1.6f);
            c.RelativeColumn(2);
        });
        table.Header(h =>
        {
            Head(h.Cell(), t, Loc.T("Désignation"));
            Head(h.Cell(), t, Loc.T("Qté"), right: true);
            Head(h.Cell(), t, "Prix", right: true);
            if (d.DiscountPerItem) Head(h.Cell(), t, "Remise", right: true);
            if (d.TaxPerItem) Head(h.Cell(), t, "Taxes", right: true);
            Head(h.Cell(), t, Loc.T("Montant"), right: true);
        });
        foreach (var line in d.Items)
        {
            Cell(table.Cell()).Column(c =>
            {
                c.Item().Text(line.Name);
                if (t.ShowDescription && !string.IsNullOrEmpty(line.Description))
                    c.Item().Text(line.Description).FontSize(t.FontSize - 2).FontColor(Colors.Grey.Darken1);
            });
            Cell(table.Cell()).AlignRight().Text($"{line.Quantity:0.##} {(t.ShowUnit ? line.UnitName : null)}".Trim());
            Cell(table.Cell()).AlignRight().Text(d.Money(line.Price));
            if (d.DiscountPerItem) Cell(table.Cell()).AlignRight().Text(d.Money(line.DiscountVal));
            if (d.TaxPerItem) Cell(table.Cell()).AlignRight().Text(d.Money(line.Tax));
            Cell(table.Cell()).AlignRight().Text(d.Money(line.Total));
        }
    });

    private static void Totals(ColumnDescriptor col, DocumentData d) => col.Item().AlignRight().Width(240).Column(c =>
    {
        Total(c, "Sous-total", d.Money(d.SubTotal));
        if (d.DiscountVal != 0)
            Total(c, "Remise", "- " + d.Money(d.DiscountVal));
        foreach (var tax in d.Taxes)
            Total(c, tax.Percent is { } p ? $"{tax.Name} ({p:0.##} %)" : tax.Name, d.Money(tax.Amount));
        Total(c, d.TaxIncluded ? Loc.T("Total (TTC)") : Loc.T("Total"), d.Money(d.Total), bold: true);
        if (d.Kind == DocumentData.Invoice && !d.IsCreditNote && d.DueAmount != d.Total)
            Total(c, Loc.T("Reste à payer"), d.Money(d.DueAmount), bold: true);
    });

    private static void PaymentBody(ColumnDescriptor col, DocumentData d, DocumentTemplate t)
    {
        col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(c =>
        {
            c.Item().Text($"{(d.Kind == DocumentData.Expense ? Loc.T("Montant") : Loc.T("Montant reçu"))} : {d.Money(d.Amount)}").FontSize(t.FontSize + 4).Bold();
            if (!string.IsNullOrEmpty(d.Category))
                c.Item().Text(Loc.F("Catégorie : {0}", d.Category));
            if (!string.IsNullOrEmpty(d.PaymentMethod))
                c.Item().Text($"Moyen de paiement : {d.PaymentMethod}");
        });
        if (d.PaidInvoices.Count > 0)
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                table.Header(h => { Head(h.Cell(), t, Loc.T("Facture")); Head(h.Cell(), t, Loc.T("Montant réglé"), right: true); });
                foreach (var (number, amount) in d.PaidInvoices)
                {
                    Cell(table.Cell()).Text(number);
                    Cell(table.Cell()).AlignRight().Text(d.Money(amount));
                }
            });
    }

    private static void Head(IContainer cell, DocumentTemplate t, string text, bool right = false)
    {
        var c = t.FilledTableHeader
            ? cell.Background(t.Accent).PaddingVertical(4).PaddingHorizontal(4)
            : cell.BorderBottom(1).BorderColor(t.Accent).PaddingVertical(4);
        var label = (right ? c.AlignRight() : c).Text(text).Bold();
        if (t.FilledTableHeader)
            label.FontColor(Colors.White);
    }

    private static IContainer Cell(IContainer cell) => cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(2);

    private static void Total(ColumnDescriptor c, string label, string value, bool bold = false) => c.Item().PaddingVertical(2).Row(r =>
    {
        var l = r.RelativeItem().Text(label);
        var v = r.AutoItem().Text(value);
        if (bold) { l.Bold(); v.Bold(); }
    });
}
