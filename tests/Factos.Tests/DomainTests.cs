using Factos.Data.Entities;
using Factos.Domain;

namespace Factos.Tests;

/// Money, document totals and numbering, checked against hand-computed upstream results.
public class DomainTests
{
    [Fact]
    public void Rounding_follows_js_on_the_form_and_php_on_the_server()
    {
        Assert.Equal(-2, Money.JsRound(-2.5m));
        Assert.Equal(3, Money.JsRound(2.5m));
        Assert.Equal(-3, Money.PhpRound(-2.5m));
        Assert.Equal(3, Money.PhpRound(2.5m));
        // MoneyConversion::toBaseMinor(150, 1.13): 169.5 -> 170.
        Assert.Equal(170, Money.ToBaseMinor(150, 1.13m));
    }

    [Fact]
    public void Amounts_format_like_format_money_ts()
    {
        var eur = new Currency { Symbol = "€", Precision = 2, ThousandSeparator = ".", DecimalSeparator = ",", SwapCurrencySymbol = true };
        var usd = new Currency { Symbol = "$", Precision = 2, ThousandSeparator = ",", DecimalSeparator = "." };
        var jpy = new Currency { Symbol = "¥", Precision = 0, ThousandSeparator = ",", DecimalSeparator = "." };

        Assert.Equal("1.234.567,89 €", Money.Format(123456789, eur));
        Assert.Equal("$ 1,234.50", Money.Format(123450, usd));
        Assert.Equal("$ -12.00", Money.Format(-1200, usd));
        Assert.Equal("¥ 1,235", Money.Format(123450, jpy));
        Assert.Equal("$ 0.05", Money.Format(5, usd));
    }

    [Fact]
    public void Document_level_discount_and_compound_tax()
    {
        var draft = new DocumentDraft
        {
            Items = [new DraftItem { Name = "A", Price = 1000, Quantity = 3 }, new DraftItem { Name = "B", Price = 550, Quantity = 2 }],
            Taxes =
            [
                new DraftTax { TaxTypeId = 1, Name = "TVA", Percent = 20 },
                new DraftTax { TaxTypeId = 2, Name = "Éco", Percent = 5, Compound = true },
            ],
            Discount = 10,
            DiscountType = "percentage",
        };
        draft.Recalculate();

        Assert.Equal(410, draft.DiscountVal);              // 10 % of 4100
        Assert.Equal(738, draft.Taxes[0].Amount);          // 20 % of 3690
        Assert.Equal(221, draft.Taxes[1].Amount);          // 5 % of 3690 + 738 = 221.4
        Assert.Equal((4100L, 959L, 4649L), draft.Totals());
    }

    [Fact]
    public void Tax_included_backs_the_tax_out_of_the_price()
    {
        var draft = new DocumentDraft
        {
            Items = [new DraftItem { Name = "A", Price = 1200, Quantity = 1 }],
            Taxes = [new DraftTax { TaxTypeId = 1, Name = "TVA", Percent = 20 }],
            TaxIncluded = true,
        };
        draft.Recalculate();

        Assert.Equal(200, draft.Taxes[0].Amount);
        Assert.Equal((1200L, 200L, 1200L), draft.Totals());
    }

    [Fact]
    public void Fixed_taxes_keep_their_amount()
    {
        var draft = new DocumentDraft
        {
            Items = [new DraftItem { Name = "A", Price = 10000, Quantity = 1 }],
            Taxes = [new DraftTax { TaxTypeId = 1, Name = "Timbre", CalculationType = "fixed", FixedAmount = 300 }],
        };
        draft.Recalculate();
        Assert.Equal((10000L, 300L, 10300L), draft.Totals());
    }

    [Fact]
    public void Per_item_taxes_share_a_document_discount_proportionally()
    {
        var draft = new DocumentDraft
        {
            TaxPerItem = true,
            Discount = 10, // fixed: 10.00
            Items =
            [
                new DraftItem { Name = "A", Price = 7500, Quantity = 1, Taxes = [new DraftTax { TaxTypeId = 1, Percent = 20 }] },
                new DraftItem { Name = "B", Price = 2500, Quantity = 1, Taxes = [new DraftTax { TaxTypeId = 1, Percent = 20 }] },
            ],
        };
        draft.Recalculate();

        // Share of 1000: A gets 0.75 -> 750, B gets 0.25 -> 250.
        Assert.Equal(1350, draft.Items[0].Taxes[0].Amount); // 20 % of 6750
        Assert.Equal(450, draft.Items[1].Taxes[0].Amount);  // 20 % of 2250
        Assert.Equal((10000L, 1800L, 10800L), draft.Totals());
    }

    [Fact]
    public void Per_item_discounts_are_capped_at_the_line_subtotal()
    {
        var draft = new DocumentDraft
        {
            DiscountPerItem = true,
            Items =
            [
                new DraftItem { Name = "A", Price = 1000, Quantity = 2, Discount = 50, DiscountType = "fixed" },      // 50.00 > 20.00
                new DraftItem { Name = "B", Price = 1000, Quantity = 1, Discount = 25, DiscountType = "percentage" },
            ],
        };
        draft.Recalculate();

        Assert.Equal(2000, draft.Items[0].DiscountVal);
        Assert.Equal(250, draft.Items[1].DiscountVal);
        Assert.Equal((750L, 0L, 750L), draft.Totals());
    }

    [Theory]
    [InlineData("{{SERIES:INV}}{{DELIMITER:-}}{{SEQUENCE:6}}", "INV-000042")]
    [InlineData("{{SERIES:EST}}{{DELIMITER:/}}{{DATE_FORMAT:Y}}{{DELIMITER:/}}{{SEQUENCE:3}}", "EST/2026/042")]
    [InlineData("{{CUSTOMER_SERIES}}-{{CUSTOMER_SEQUENCE:4}}", "ACME0007")]
    [InlineData("{{UNKNOWN:X}}{{SEQUENCE}}", "000042")]
    public void Serial_numbers_render_like_serial_number_service(string format, string expected)
    {
        // Text outside placeholders is dropped, as upstream does.
        Assert.Equal(expected, SerialNumbers.Generate(format, 42, 7, "ACME", new DateTime(2026, 5, 1)));
    }

    [Fact]
    public void Cashflow_period_and_axis_follow_reporting_period()
    {
        // ReportingPeriod::fiscalYear : un mois d'ouverture encore à venir appartient à l'exercice précédent.
        Assert.Equal(new DateOnly(2026, 1, 1), Cashflow.FiscalYearStart("1-12", new DateOnly(2026, 10, 23)));
        Assert.Equal(new DateOnly(2025, 11, 1), Cashflow.FiscalYearStart("11-10", new DateOnly(2026, 10, 23)));
        Assert.Equal(new DateOnly(2026, 1, 1), Cashflow.FiscalYearStart("calendar_year", new DateOnly(2026, 10, 23)));

        Assert.Equal([0, 25000, 50000, 75000, 100000], Cashflow.Ticks(0, 0)); // période vide : 0 → 1 000, comme suggestedMax
        Assert.Equal([0, 250000, 500000, 750000, 1000000], Cashflow.Ticks(0, 912345));
        Assert.Equal([-50000, 0, 50000, 100000, 150000], Cashflow.Ticks(-20000, 140000));
        Assert.Equal("12,3 k", Cashflow.Compact(1234567));
    }
}
