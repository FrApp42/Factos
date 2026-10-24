using Factos.Components;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// Port of upstream's CreditNoteAmounts: what a credit note carries is the difference
/// between the cumulative credit after it and before it, each computed from the original
/// invoice by quantity ratio. Successive partial credits therefore telescope to exactly the
/// original amounts, to the cent.
public static class CreditNoteAmounts
{
    public sealed record TaxSnap(long Amount, long BaseAmount);

    public sealed record ItemSnap(long Price, decimal Quantity, long DiscountVal, long Tax, long Total,
        long BasePrice, long BaseDiscountVal, long BaseTax, long BaseTotal, Dictionary<int, TaxSnap> Taxes);

    public sealed record Snapshot(long SubTotal, long DiscountVal, long Tax, long Total,
        long BaseSubTotal, long BaseDiscountVal, long BaseTax, long BaseTotal,
        bool DiscountPerItem, bool TaxPerItem, bool TaxIncluded,
        Dictionary<int, ItemSnap> Items, Dictionary<int, TaxSnap> Taxes);

    public sealed record ItemAmounts(long Price, long BasePrice, decimal Quantity, long DiscountVal, long Tax, long Total,
        long BaseDiscountVal, long BaseTax, long BaseTotal, Dictionary<int, TaxSnap> Taxes);

    public sealed record Amounts(long SubTotal, long DiscountVal, long Tax, long Total,
        long BaseSubTotal, long BaseDiscountVal, long BaseTax, long BaseTotal,
        Dictionary<int, ItemAmounts> Items, Dictionary<int, TaxSnap> Taxes);

    public static int ToHundredths(decimal quantity) => (int)Money.PhpRound(quantity * 100);

    private static decimal FromHundredths(int hundredths) => hundredths / 100m;

    public static Amounts Cumulative(Snapshot s, Dictionary<int, int> credited)
    {
        var items = new Dictionary<int, ItemAmounts>();
        long subTotal = 0, itemTaxTotal = 0;
        foreach (var (itemId, item) in s.Items)
        {
            var original = ToHundredths(item.Quantity);
            var done = credited.GetValueOrDefault(itemId);
            var ratio = original == 0 ? 0m : (decimal)done / original;
            var amount = Money.PhpRound(item.Price * (decimal)done / 100);
            var discount = s.DiscountPerItem ? Money.PhpRound(item.DiscountVal * ratio) : 0;

            var taxes = new Dictionary<int, TaxSnap>();
            long itemTax = 0;
            foreach (var (taxId, tax) in item.Taxes)
            {
                var taxAmount = Money.PhpRound(tax.Amount * ratio);
                taxes[taxId] = new TaxSnap(taxAmount, Scale(tax.BaseAmount, taxAmount, tax.Amount));
                itemTax += taxAmount;
            }
            var total = amount - discount;
            items[itemId] = new ItemAmounts(item.Price, item.BasePrice, FromHundredths(done), discount, itemTax, total,
                Scale(item.BaseDiscountVal, discount, item.DiscountVal), Scale(item.BaseTax, itemTax, item.Tax),
                Scale(item.BaseTotal, total, item.Total), taxes);
            subTotal += total;
            itemTaxTotal += itemTax;
        }

        var docRatio = s.SubTotal == 0 ? 0m : (decimal)subTotal / s.SubTotal;
        var discountVal = Money.PhpRound(s.DiscountVal * docRatio);
        var documentTaxes = new Dictionary<int, TaxSnap>();
        long documentTaxTotal = 0;
        foreach (var (taxId, tax) in s.Taxes)
        {
            var taxAmount = Money.PhpRound(tax.Amount * docRatio);
            documentTaxes[taxId] = new TaxSnap(taxAmount, Scale(tax.BaseAmount, taxAmount, tax.Amount));
            documentTaxTotal += taxAmount;
        }
        var allTax = s.TaxPerItem ? itemTaxTotal : documentTaxTotal;
        var grand = s.TaxIncluded ? subTotal - discountVal : subTotal - discountVal + allTax;

        return new Amounts(subTotal, discountVal, allTax, grand,
            Scale(s.BaseSubTotal, subTotal, s.SubTotal), Scale(s.BaseDiscountVal, discountVal, s.DiscountVal),
            Scale(s.BaseTax, allTax, s.Tax), Scale(s.BaseTotal, grand, s.Total), items, documentTaxes);
    }

    /// forCredit(): the amounts of one credit note taking the credited quantities from `before` to `after`.
    public static Amounts ForCredit(Snapshot s, Dictionary<int, int> before, Dictionary<int, int> after)
    {
        var from = Cumulative(s, before);
        var to = Cumulative(s, after);
        var items = new Dictionary<int, ItemAmounts>();
        foreach (var (itemId, item) in to.Items)
        {
            var previous = from.Items[itemId];
            var hundredths = ToHundredths(item.Quantity) - ToHundredths(previous.Quantity);
            if (hundredths == 0)
                continue;
            items[itemId] = new ItemAmounts(item.Price, item.BasePrice, FromHundredths(hundredths),
                item.DiscountVal - previous.DiscountVal, item.Tax - previous.Tax, item.Total - previous.Total,
                item.BaseDiscountVal - previous.BaseDiscountVal, item.BaseTax - previous.BaseTax, item.BaseTotal - previous.BaseTotal,
                item.Taxes.ToDictionary(t => t.Key, t => new TaxSnap(t.Value.Amount - previous.Taxes[t.Key].Amount, t.Value.BaseAmount - previous.Taxes[t.Key].BaseAmount)));
        }
        return new Amounts(to.SubTotal - from.SubTotal, to.DiscountVal - from.DiscountVal, to.Tax - from.Tax, to.Total - from.Total,
            to.BaseSubTotal - from.BaseSubTotal, to.BaseDiscountVal - from.BaseDiscountVal, to.BaseTax - from.BaseTax, to.BaseTotal - from.BaseTotal,
            items, to.Taxes.ToDictionary(t => t.Key, t => new TaxSnap(t.Value.Amount - from.Taxes[t.Key].Amount, t.Value.BaseAmount - from.Taxes[t.Key].BaseAmount)));
    }

    private static long Scale(long value, long part, long whole) => whole == 0 ? 0 : Money.PhpRound((decimal)value * part / whole);
}

/// Credit notes (CreditNoteService): a reversal of an issued invoice, whole or line by
/// line, never beyond what is still unpaid on it.
public class CreditNotes(AppDbFactory dbs)
{
    public const string Type = "CREDIT_NOTE";

    /// What each invoice line can still be credited, in quantity.
    public async Task<Dictionary<int, decimal>> RemainingQuantitiesAsync(int invoiceId)
    {
        await using var db = dbs.Create();
        var (invoiced, credited) = await QuantitiesAsync(db, invoiceId);
        return invoiced.ToDictionary(i => i.Key, i => Math.Max(0, i.Value - credited.GetValueOrDefault(i.Key)) / 100m);
    }

    /// `lines`: invoice item id → quantity to credit; null credits everything left.
    public async Task<int> CreateAsync(int companyId, int userId, int invoiceId, Dictionary<int, decimal>? lines, string? reason)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var original = await db.Invoices.SingleAsync(i => i.Id == invoiceId && i.CompanyId == companyId);
        if (original.Type == Type)
            throw new SalesException(Loc.T("On ne peut pas créditer une note de crédit."));
        if (original.Status == Sales.Draft)
            throw new SalesException(Loc.T("Une facture en brouillon n'a jamais été émise : modifiez-la ou supprimez-la."));

        var items = await db.InvoiceItems.Where(i => i.InvoiceId == invoiceId).OrderBy(i => i.Id).ToListAsync();
        var itemIds = items.Select(i => (int?)i.Id).ToList();
        var taxes = await db.Taxes.Where(t => t.InvoiceId == invoiceId || itemIds.Contains(t.InvoiceItemId)).ToListAsync();
        var snapshot = new CreditNoteAmounts.Snapshot(
            original.SubTotal, original.DiscountVal ?? 0, original.Tax, original.Total,
            original.BaseSubTotal ?? 0, original.BaseDiscountVal ?? 0, original.BaseTax ?? 0, original.BaseTotal ?? 0,
            original.DiscountPerItem == "YES", original.TaxPerItem == "YES", original.TaxIncluded,
            items.ToDictionary(i => i.Id, i => new CreditNoteAmounts.ItemSnap(i.Price, i.Quantity, i.DiscountVal, i.Tax, i.Total,
                i.BasePrice ?? 0, i.BaseDiscountVal ?? 0, i.BaseTax ?? 0, i.BaseTotal ?? 0,
                taxes.Where(t => t.InvoiceItemId == i.Id).ToDictionary(t => t.Id, t => new CreditNoteAmounts.TaxSnap(t.Amount, t.BaseAmount ?? 0)))),
            taxes.Where(t => t.InvoiceId == invoiceId).ToDictionary(t => t.Id, t => new CreditNoteAmounts.TaxSnap(t.Amount, t.BaseAmount ?? 0)));

        var (invoiced, before) = await QuantitiesAsync(db, invoiceId);
        var after = lines is null
            ? new Dictionary<int, int>(invoiced)
            : new Dictionary<int, int>(before);
        if (lines is not null)
            foreach (var (itemId, quantity) in lines.Where(l => l.Value > 0))
                after[itemId] = after.GetValueOrDefault(itemId) + CreditNoteAmounts.ToHundredths(quantity);

        var paid = await db.PaymentAllocations.Where(a => a.InvoiceId == invoiceId).SumAsync(a => (long?)a.Amount) ?? 0;
        var creditedBefore = -(await db.Invoices.Where(i => i.RelatedInvoiceId == invoiceId && i.Type == Type).SumAsync(i => (long?)i.Total) ?? 0);

        // guard()
        var remaining = invoiced.Sum(i => Math.Max(0, i.Value - before.GetValueOrDefault(i.Key)));
        if (remaining == 0 || original.Total - paid - creditedBefore <= 0)
            throw new SalesException(Loc.T("Cette facture est déjà entièrement créditée ou payée."));
        if (after.Any(a => a.Value > invoiced.GetValueOrDefault(a.Key)))
            throw new SalesException(Loc.T("La quantité créditée dépasse la quantité restante d'une ligne."));
        if (!after.Any(a => a.Value > before.GetValueOrDefault(a.Key)))
            throw new SalesException(Loc.T("Indiquez au moins une quantité à créditer."));

        var amounts = CreditNoteAmounts.ForCredit(snapshot, before, after);
        if (creditedBefore + amounts.Total > original.Total - paid)
            throw new SalesException(Loc.T("Le montant crédité dépasse le reste à payer de la facture."));

        var now = Data.Timestamp.Now();
        var (sequence, customerSequence) = await NextSequencesAsync(db, companyId, original.CustomerId);
        var format = await db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == "credit_note_number_format").Select(s => s.Value).FirstOrDefaultAsync() ?? "";
        var prefix = await db.Customers.Where(c => c.Id == original.CustomerId).Select(c => c.Prefix).FirstOrDefaultAsync();

        var note = new Invoice
        {
            CreatorId = userId, Type = Type, RelatedInvoiceId = original.Id, CreditReason = reason,
            InvoiceDate = DateTime.UtcNow.Date, DueDate = null,
            InvoiceNumber = SerialNumbers.Generate(format, sequence, customerSequence, prefix, DateTime.Now),
            SequenceNumber = sequence, CustomerSequenceNumber = customerSequence,
            ReferenceNumber = original.InvoiceNumber, CustomerId = original.CustomerId, CompanyId = companyId,
            TemplateName = original.TemplateName,
            // Born DRAFT so it can be sent, born PAID: nothing is ever owed on it.
            Status = Sales.Draft, PaidStatus = Sales.Paid,
            SubTotal = -amounts.SubTotal, DiscountVal = -amounts.DiscountVal, Total = -amounts.Total, DueAmount = 0, Tax = -amounts.Tax,
            TaxIncluded = original.TaxIncluded, Notes = original.Notes, ExchangeRate = original.ExchangeRate,
            BaseDiscountVal = -amounts.BaseDiscountVal, BaseSubTotal = -amounts.BaseSubTotal, BaseTotal = -amounts.BaseTotal,
            BaseTax = -amounts.BaseTax, BaseDueAmount = 0,
            Discount = original.Discount, DiscountType = original.DiscountType, TaxPerItem = original.TaxPerItem,
            DiscountPerItem = original.DiscountPerItem, CurrencyId = original.CurrencyId,
            SalesTaxType = original.SalesTaxType, SalesTaxAddressType = original.SalesTaxAddressType,
            UniqueHash = Companies.PublicToken(), CreatedAt = now, UpdatedAt = now,
        };
        db.Invoices.Add(note);
        await db.SaveChangesAsync();

        foreach (var (sourceId, line) in amounts.Items)
        {
            var source = items.Single(i => i.Id == sourceId);
            // The quantity stays positive: the negative price and totals make it a credit.
            var credit = new InvoiceItem
            {
                InvoiceId = note.Id, SourceInvoiceItemId = sourceId, ItemId = source.ItemId, CompanyId = companyId,
                Name = source.Name, Description = source.Description, UnitName = source.UnitName,
                DiscountType = source.DiscountType, Discount = source.Discount, Quantity = line.Quantity,
                Price = -line.Price, BasePrice = -line.BasePrice, DiscountVal = -line.DiscountVal, Tax = -line.Tax, Total = -line.Total,
                BaseDiscountVal = -line.BaseDiscountVal, BaseTax = -line.BaseTax, BaseTotal = -line.BaseTotal,
                ExchangeRate = original.ExchangeRate, CreatedAt = now, UpdatedAt = now,
            };
            db.InvoiceItems.Add(credit);
            await db.SaveChangesAsync();
            foreach (var (taxId, amount) in line.Taxes)
                db.Taxes.Add(CreditTax(taxes.Single(t => t.Id == taxId), amount, original, now, invoiceItemId: credit.Id));
        }
        if (original.TaxPerItem != "YES")
            foreach (var (taxId, amount) in amounts.Taxes)
                db.Taxes.Add(CreditTax(taxes.Single(t => t.Id == taxId), amount, original, now, invoiceId: note.Id));
        await db.SaveChangesAsync();

        await Sales.RecalculateBalanceAsync(db, original);
        await tx.CommitAsync();
        return note.Id;
    }

    private static Tax CreditTax(Tax source, CreditNoteAmounts.TaxSnap amount, Invoice original, DateTime now, int? invoiceId = null, int? invoiceItemId = null) => new()
    {
        TaxTypeId = source.TaxTypeId, ItemId = source.ItemId, Name = source.Name, Percent = source.Percent,
        CompoundTax = source.CompoundTax, CalculationType = source.CalculationType, FixedAmount = source.FixedAmount,
        Amount = -amount.Amount, BaseAmount = -amount.BaseAmount, CompanyId = original.CompanyId,
        ExchangeRate = original.ExchangeRate, CurrencyId = original.CurrencyId,
        InvoiceId = invoiceId, InvoiceItemId = invoiceItemId, CreatedAt = now, UpdatedAt = now,
    };

    /// Invoiced and already-credited quantities per invoice line, in hundredths.
    private static async Task<(Dictionary<int, int> Invoiced, Dictionary<int, int> Credited)> QuantitiesAsync(ApplicationDbContext db, int invoiceId)
    {
        var invoiced = await db.InvoiceItems.Where(i => i.InvoiceId == invoiceId)
            .Select(i => new { i.Id, i.Quantity }).ToListAsync();
        var credited = await db.InvoiceItems
            .Where(i => i.SourceInvoiceItemId != null && db.Invoices.Any(n => n.Id == i.InvoiceId && n.RelatedInvoiceId == invoiceId && n.Type == Type))
            .Select(i => new { i.SourceInvoiceItemId, i.Quantity }).ToListAsync();
        return (
            invoiced.ToDictionary(i => i.Id, i => CreditNoteAmounts.ToHundredths(i.Quantity)),
            credited.GroupBy(i => i.SourceInvoiceItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => CreditNoteAmounts.ToHundredths(i.Quantity))));
    }

    /// Credit notes are numbered in their own sequence (`type = CREDIT_NOTE`).
    private static async Task<(int, int)> NextSequencesAsync(ApplicationDbContext db, int companyId, long? customerId)
    {
        var customer = customerId ?? 1;
        return (
            (await db.Invoices.Where(i => i.CompanyId == companyId && i.Type == Type).MaxAsync(i => i.SequenceNumber) ?? 0) + 1,
            (await db.Invoices.Where(i => i.CompanyId == companyId && i.Type == Type && i.CustomerId == customer).MaxAsync(i => i.CustomerSequenceNumber) ?? 0) + 1);
    }
}
