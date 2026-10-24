namespace Factos.Domain;

/// A tax line on a document or on one of its items.
public class DraftTax
{
    public int TaxTypeId { get; set; }
    public string Name { get; set; } = "";
    public decimal? Percent { get; set; }
    public string CalculationType { get; set; } = "percentage";
    public long? FixedAmount { get; set; }
    public bool Compound { get; set; }
    public long Amount { get; set; }
}

public class DraftItem
{
    public int? ItemId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; } = 1;
    /// Unit price in cents.
    public long Price { get; set; }
    public decimal Discount { get; set; }
    public string DiscountType { get; set; } = "fixed";
    public long DiscountVal { get; set; }
    public List<DraftTax> Taxes { get; set; } = [];

    public long Subtotal => Money.JsRound(Price * Quantity);
    public long Total => Subtotal - DiscountVal;
    public long Tax => Taxes.Sum(t => t.Amount);
}

/// The invoice/estimate form, computed the way InvoiceShelf's document form does it
/// (resources/scripts/features/shared/document-form): every amount is recomputed from
/// prices, quantities, discounts and tax rates, then the stored totals come from the
/// server's DocumentTotals::compute().
public class DocumentDraft
{
    public List<DraftItem> Items { get; set; } = [];
    public List<DraftTax> Taxes { get; set; } = [];
    public decimal Discount { get; set; }
    public string DiscountType { get; set; } = "fixed";
    public long DiscountVal { get; set; }
    public bool TaxPerItem { get; set; }
    public bool DiscountPerItem { get; set; }
    public bool TaxIncluded { get; set; }

    public long SubTotal => Items.Sum(i => i.Total);
    public long SubtotalWithDiscount => SubTotal - DiscountVal;

    public void Recalculate()
    {
        foreach (var item in Items)
        {
            if (!DiscountPerItem)
            {
                item.Discount = 0;
                item.DiscountVal = 0;
            }
            else
            {
                // calcItemDiscountVal()
                var absolute = Math.Abs(item.Subtotal);
                item.DiscountVal = item.DiscountType == "percentage"
                    ? Money.JsRound(absolute * item.Discount / 100)
                    : Math.Min(Money.JsRound(item.Discount * 100), absolute);
            }
        }

        // DocumentTotals.vue setDiscount()
        DiscountVal = DiscountType == "percentage"
            ? Money.JsRound(SubTotal * Discount / 100)
            : Money.JsRound(Discount * 100);

        if (TaxPerItem)
        {
            Taxes.Clear();
            foreach (var item in Items)
                ApplyTaxes(item.Taxes, ItemTaxBase(item));
        }
        else
        {
            foreach (var item in Items)
                item.Taxes.Clear();
            ApplyTaxes(Taxes, SubtotalWithDiscount);
        }
    }

    /// Server-side DocumentTotals::compute(): what gets stored.
    public (long SubTotal, long Tax, long Total) Totals()
    {
        long subTotal = 0, simple = 0, compound = 0;
        foreach (var item in Items)
        {
            subTotal += Money.PhpRound(item.Price * item.Quantity) - (DiscountPerItem ? item.DiscountVal : 0);
            if (TaxPerItem)
            {
                simple += item.Taxes.Where(t => t.TaxTypeId != 0 && !t.Compound).Sum(t => t.Amount);
                compound += item.Taxes.Where(t => t.TaxTypeId != 0 && t.Compound).Sum(t => t.Amount);
            }
        }
        if (!TaxPerItem)
        {
            simple = Taxes.Where(t => !t.Compound).Sum(t => t.Amount);
            compound = Taxes.Where(t => t.Compound).Sum(t => t.Amount);
        }

        var withDiscount = subTotal - DiscountVal;
        var tax = simple + compound;
        return (subTotal, tax, TaxIncluded ? withDiscount + compound : withDiscount + tax);
    }

    /// Simple taxes on the base first, then compound taxes on base + simple taxes.
    private void ApplyTaxes(List<DraftTax> taxes, long taxBase)
    {
        long simpleTotal = 0;
        foreach (var tax in taxes.Where(t => !t.Compound))
        {
            tax.Amount = TaxAmount(taxBase, tax, TaxIncluded, 0);
            simpleTotal += tax.Amount;
        }
        foreach (var tax in taxes.Where(t => t.Compound))
            tax.Amount = TaxAmount(taxBase, tax, TaxIncluded, simpleTotal);
    }

    /// DocumentItemRowTax.vue effectiveBase: with a document-level discount, an item
    /// carries its proportional share of it.
    private long ItemTaxBase(DraftItem item)
    {
        if (DiscountPerItem || Discount <= 0)
            return item.Total;
        var itemsTotal = SubTotal;
        if (itemsTotal == 0)
            return item.Total;
        var proportion = Math.Round((decimal)item.Total / itemsTotal, 2, MidpointRounding.AwayFromZero);
        var discount = DiscountType == "fixed" ? Discount * 100 : itemsTotal * Discount / 100;
        return item.Total - Money.JsRound(discount * proportion);
    }

    /// calcTaxAmount() from use-document-calculations.ts.
    public static long TaxAmount(long total, DraftTax tax, bool taxIncluded, long simpleTaxTotal)
    {
        if (tax.CalculationType == "fixed" && tax.FixedAmount is { } fixedAmount)
            return fixedAmount;
        if (total == 0 || tax.Percent is not { } percent || percent == 0)
            return 0;
        if (tax.Compound)
            return taxIncluded
                ? Money.JsRound(total * percent / 100)
                : Money.JsRound((total + simpleTaxTotal) * percent / 100);
        if (taxIncluded)
            return Money.JsRound(total - total / (1 + percent / 100));
        return Money.JsRound(total * percent / 100);
    }
}
