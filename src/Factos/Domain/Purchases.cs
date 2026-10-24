using Factos.Components;
using System.Text.Json;
using System.Text.Json.Nodes;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

public class PurchaseLine
{
    public long? SourceBillItemId { get; set; }
    public string Description { get; set; } = "";
    public int ExpenseCategoryId { get; set; }
    public decimal Quantity { get; set; } = 1;
    /// Unit price in cents.
    public long Price { get; set; }
    /// Always a percentage, as upstream forces it.
    public decimal Discount { get; set; }
    public List<int> TaxTypeIds { get; set; } = [];
}

/// A bill, or a supplier credit (no due date; `SourceBillId` credits lines of a bill).
public class PurchaseForm
{
    public long SupplierId { get; set; }
    public int CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public string? Reference { get; set; }
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = Purchases.Draft;
    public string? Notes { get; set; }
    public bool TaxIncluded { get; set; }
    public long? SourceBillId { get; set; }
    public List<PurchaseLine> Items { get; set; } = [];
}

/// A supplier payment (with its allocations), or a refund out of a payment or a credit.
public class SupplierMoneyForm
{
    public long SupplierId { get; set; }
    public int CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public long Amount { get; set; }
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? PaymentMethodId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public long? SupplierPaymentId { get; set; }
    public long? SupplierCreditId { get; set; }
    public List<BillShare> Allocations { get; set; } = [];
}

public record BillShare(long BillId, long Amount);

/// One tax snapshot of a purchase line, stored in the line's `taxes` JSON.
public record PurchaseTax(string Name, decimal? Percent, string CalculationType, long? FixedAmount, bool CompoundTax,
    int TaxTypeId, long Amount, long BaseAmount);

/// Port of upstream's PurchaseDocumentService + SupplierSettlementService: bills and
/// supplier credits (lines, taxes, totals computed here), payments to suppliers and how
/// payments and credits settle bills, refunds out of what a payment or credit has left,
/// and voiding.
public class Purchases(AppDbFactory dbs)
{
    public const string Draft = "DRAFT", Open = "OPEN", Void = "VOID";

    /// The most a line or a document may come to, in minor units.
    private const long MoneyLimit = 999999999999;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    // ---- suppliers ----------------------------------------------------------------

    public async Task<long> SaveSupplierAsync(int companyId, int userId, Supplier form)
    {
        Ensure(!string.IsNullOrWhiteSpace(form.Name), Loc.T("Le nom du fournisseur est obligatoire."));
        Ensure(form.PaymentTerms is >= 0 and <= 3650, Loc.T("Le délai de paiement doit être compris entre 0 et 3650 jours."));
        Ensure(string.IsNullOrWhiteSpace(form.Website) || Uri.TryCreate(form.Website, UriKind.Absolute, out _), Loc.T("Le site web doit être une URL."));
        await using var db = dbs.Create();
        Ensure(await db.Currencies.AnyAsync(c => c.Id == form.CurrencyId), "Choisissez une devise.");
        Ensure(form.ExpenseCategoryId is null || await db.ExpenseCategories.AnyAsync(c => c.Id == form.ExpenseCategoryId && c.CompanyId == companyId),
            Loc.T("Catégorie de dépense introuvable."));

        var now = Timestamp.Now();
        var supplier = form.Id == 0
            ? db.Suppliers.Add(new Supplier { CompanyId = companyId, CreatorId = userId, CreatedAt = now }).Entity
            : await db.Suppliers.SingleAsync(s => s.Id == form.Id && s.CompanyId == companyId);
        supplier.Name = form.Name.Trim();
        supplier.ContactName = form.ContactName;
        supplier.Email = form.Email;
        supplier.Phone = form.Phone;
        supplier.Website = form.Website;
        supplier.TaxId = form.TaxId;
        supplier.CurrencyId = form.CurrencyId;
        supplier.ExpenseCategoryId = form.ExpenseCategoryId;
        supplier.PaymentTerms = form.PaymentTerms;
        supplier.Notes = form.Notes;
        supplier.Enabled = form.Enabled;
        supplier.UpdatedAt = now;
        await db.SaveChangesAsync();
        return supplier.Id;
    }

    // ---- bills and credits --------------------------------------------------------

    /// saveBill(): once a bill is settled or credited its financial details are locked,
    /// only the reference, due date and notes can change.
    public async Task<long> SaveBillAsync(int companyId, int userId, long? billId, PurchaseForm form)
    {
        Ensure(form.DueDate is { } due && due >= form.DocumentDate, Loc.T("L'échéance est obligatoire et ne peut précéder la date de la facture."));
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var supplier = await SupplierAsync(db, companyId, form.SupplierId);
        Ensure(supplier.Enabled, Loc.T("Ce fournisseur est désactivé."));
        var rate = await RateAsync(db, companyId, form.CurrencyId, form.ExchangeRate);
        var now = Timestamp.Now();

        Bill bill;
        if (billId is { } id)
        {
            bill = await db.Bills.SingleAsync(b => b.Id == id && b.CompanyId == companyId);
            Ensure(bill.Status != Void, Loc.T("Une facture annulée ne se modifie plus."));
            if (bill.FinancialLockedAt is not null)
            {
                var items = await db.BillItems.Where(i => i.BillId == id).OrderBy(i => i.Id).ToListAsync();
                Ensure(Unchanged(bill, items, form, rate), Loc.T("Cette facture est réglée ou créditée : seuls la référence, l'échéance et les notes changent."));
                bill.Reference = form.Reference;
                bill.DueDate = form.DueDate;
                bill.Notes = form.Notes;
                bill.UpdatedAt = now;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return bill.Id;
            }
        }
        else
        {
            bill = db.Bills.Add(new Bill { CompanyId = companyId, CreatorId = userId, CreatedAt = now }).Entity;
        }

        var lines = await ComposeAsync(db, companyId, form.Items, rate, form.TaxIncluded);
        bill.SupplierId = supplier.Id;
        bill.Reference = form.Reference;
        bill.DocumentDate = form.DocumentDate;
        bill.DueDate = form.DueDate;
        bill.Notes = form.Notes;
        bill.TaxIncluded = form.TaxIncluded;
        bill.CurrencyId = form.CurrencyId;
        bill.ExchangeRate = rate;
        SetTotals(bill, lines);
        bill.Status = billId is not null && bill.Status == Open ? Open : form.Status == Open ? Open : Draft;
        bill.SupplierSnapshot = Snapshot(supplier);
        bill.UpdatedAt = now;
        if (bill.Number is null)
            (bill.Number, bill.SequenceNumber) = await NextNumberAsync(db, db.Bills, companyId, "bill_number_format", "{{SERIES:BILL}}{{DELIMITER:-}}{{SEQUENCE:6}}");
        await db.SaveChangesAsync();

        await db.BillItems.Where(i => i.BillId == bill.Id).ExecuteDeleteAsync();
        db.BillItems.AddRange(lines.Select(l => Copy<BillItem>(l, now, i => i.BillId = bill.Id)));
        await db.SaveChangesAsync();
        await RecalculateAsync(db, bill);
        await tx.CommitAsync();
        return bill.Id;
    }

    /// createCredit(): against lines of an open bill (at the bill's prices, taxes and rate),
    /// or on its own lines. A credit is born OPEN; it settles nothing until allocated.
    public async Task<long> CreateCreditAsync(int companyId, int userId, PurchaseForm form)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var supplier = await SupplierAsync(db, companyId, form.SupplierId);
        var now = Timestamp.Now();
        int currencyId;
        decimal rate;
        bool taxIncluded;
        List<PurchaseItem> lines;

        if (form.SourceBillId is { } billId)
        {
            var bill = await db.Bills.SingleOrDefaultAsync(b => b.Id == billId && b.CompanyId == companyId)
                ?? throw new SalesException(Loc.T("Facture fournisseur introuvable."));
            Ensure(bill.SupplierId == supplier.Id && bill.CurrencyId == form.CurrencyId && bill.Status == Open,
                Loc.T("La facture d'origine doit être ouverte, de ce fournisseur et dans cette devise."));
            (currencyId, rate, taxIncluded) = (bill.CurrencyId, bill.ExchangeRate, bill.TaxIncluded);
            lines = await CreditBillItemsAsync(db, bill, form.Items);
            bill.FinancialLockedAt ??= now;
        }
        else
        {
            (currencyId, rate, taxIncluded) = (form.CurrencyId, await RateAsync(db, companyId, form.CurrencyId, form.ExchangeRate), form.TaxIncluded);
            lines = await ComposeAsync(db, companyId, form.Items, rate, taxIncluded);
        }

        var credit = new SupplierCredit
        {
            CompanyId = companyId, CreatorId = userId, SupplierId = supplier.Id, Reference = form.Reference,
            DocumentDate = form.DocumentDate, Notes = form.Notes, TaxIncluded = taxIncluded, SourceBillId = form.SourceBillId,
            CurrencyId = currencyId, ExchangeRate = rate, Status = Open, SupplierSnapshot = Snapshot(supplier),
            CreatedAt = now, UpdatedAt = now,
        };
        SetTotals(credit, lines);
        (credit.Number, credit.SequenceNumber) = await NextNumberAsync(db, db.SupplierCredits, companyId,
            "supplier_credit_number_format", "{{SERIES:SC}}{{DELIMITER:-}}{{SEQUENCE:6}}");
        db.SupplierCredits.Add(credit);
        await db.SaveChangesAsync();
        db.SupplierCreditItems.AddRange(lines.Select(l => Copy<SupplierCreditItem>(l, now, i => i.SupplierCreditId = credit.Id)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return credit.Id;
    }

    public async Task OpenBillAsync(int companyId, long billId)
    {
        await using var db = dbs.Create();
        var bill = await db.Bills.SingleAsync(b => b.Id == billId && b.CompanyId == companyId);
        Ensure(bill.Status == Draft, Loc.T("Seule une facture brouillon peut être ouverte."));
        bill.Status = Open;
        bill.UpdatedAt = Timestamp.Now();
        await db.SaveChangesAsync();
    }

    /// act('void'): a document that still settles or is settled by something cannot be voided.
    public async Task VoidBillAsync(int companyId, long billId, string reason)
    {
        await using var db = dbs.Create();
        var bill = await db.Bills.SingleAsync(b => b.Id == billId && b.CompanyId == companyId);
        Ensure(!string.IsNullOrWhiteSpace(reason), Loc.T("Indiquez le motif de l'annulation."));
        if (bill.Status == Void)
            return;
        Ensure(!await db.SupplierPaymentAllocations.AnyAsync(a => a.BillId == billId)
               && !await db.SupplierCreditAllocations.AnyAsync(a => a.BillId == billId)
               && !await db.SupplierCredits.AnyAsync(c => c.SourceBillId == billId && c.Status != Void),
            Loc.T("Cette facture est réglée ou créditée : libérez d'abord ses règlements et avoirs."));
        MarkVoid(bill, reason);
        await db.SaveChangesAsync();
    }

    public async Task VoidCreditAsync(int companyId, long creditId, string reason)
    {
        await using var db = dbs.Create();
        var credit = await db.SupplierCredits.SingleAsync(c => c.Id == creditId && c.CompanyId == companyId);
        Ensure(!string.IsNullOrWhiteSpace(reason), Loc.T("Indiquez le motif de l'annulation."));
        if (credit.Status == Void)
            return;
        Ensure(!await db.SupplierCreditAllocations.AnyAsync(a => a.SupplierCreditId == creditId)
               && !await db.SupplierRefunds.AnyAsync(r => r.SupplierCreditId == creditId && r.Status == Open),
            Loc.T("Cet avoir est affecté ou remboursé : libérez d'abord ses affectations et remboursements."));
        MarkVoid(credit, reason);
        await db.SaveChangesAsync();
    }

    // ---- payments, allocations, refunds ---------------------------------------------

    /// recordPayment(): money paid to a supplier, allocated to any of its open bills.
    /// Whatever is not allocated stays available as an advance.
    public async Task<long> RecordPaymentAsync(int companyId, int userId, SupplierMoneyForm form)
    {
        CheckMoney(form.Amount);
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var supplier = await SupplierAsync(db, companyId, form.SupplierId);
        Ensure(supplier.Enabled, Loc.T("Ce fournisseur est désactivé."));
        await CheckMethodAsync(db, companyId, form.PaymentMethodId);
        var rate = await RateAsync(db, companyId, form.CurrencyId, form.ExchangeRate);
        var now = Timestamp.Now();
        var payment = new SupplierPayment
        {
            CompanyId = companyId, CreatorId = userId, SupplierId = supplier.Id, Amount = form.Amount,
            PaymentDate = form.PaymentDate, PaymentMethodId = form.PaymentMethodId, Reference = form.Reference, Notes = form.Notes,
            CurrencyId = form.CurrencyId, ExchangeRate = rate, BaseAmount = Money.ToBaseMinor(form.Amount, rate), Status = Open,
            CreatedAt = now, UpdatedAt = now,
        };
        (payment.Number, payment.SequenceNumber) = await NextNumberAsync(db, db.SupplierPayments, companyId,
            "supplier_payment_number_format", "{{SERIES:SP}}{{DELIMITER:-}}{{SEQUENCE:6}}");
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync();
        await ReplaceAllocationsAsync(db, payment, form.Allocations);
        await tx.CommitAsync();
        return payment.Id;
    }

    /// replaceAllocations(): which bills a payment or credit settles, and by how much. An
    /// empty list releases every allocation, reopening those bills.
    public async Task ReplaceAllocationsAsync(int companyId, string kind, long id, List<BillShare> rows)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        await ReplaceAllocationsAsync(db, await SourceAsync(db, companyId, kind, id), rows);
        await tx.CommitAsync();
    }

    /// recordRefund(): money a supplier returned, out of the unallocated balance of a
    /// payment or a credit.
    public async Task<long> RecordRefundAsync(int companyId, int userId, SupplierMoneyForm form)
    {
        CheckMoney(form.Amount);
        Ensure((form.SupplierPaymentId is null) != (form.SupplierCreditId is null),Loc.T("Un remboursement porte sur un paiement ou sur un avoir."));
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        PurchaseRecord source = form.SupplierPaymentId is { } paymentId
            ? await SourceAsync(db, companyId, "payment", paymentId)
            : await SourceAsync(db, companyId, "credit", form.SupplierCreditId!.Value);
        Ensure(form.Amount <= await AvailableAsync(db, source), Loc.T("Le remboursement dépasse le solde disponible."));
        await CheckMethodAsync(db, companyId, form.PaymentMethodId);
        var rate = await RateAsync(db, companyId, source.CurrencyId, form.ExchangeRate);
        var now = Timestamp.Now();
        var refund = new SupplierRefund
        {
            CompanyId = companyId, CreatorId = userId, SupplierId = source.SupplierId,
            SupplierPaymentId = form.SupplierPaymentId, SupplierCreditId = form.SupplierCreditId,
            Amount = form.Amount, PaymentDate = form.PaymentDate, PaymentMethodId = form.PaymentMethodId,
            Reference = form.Reference, Notes = form.Notes, CurrencyId = source.CurrencyId, ExchangeRate = rate,
            BaseAmount = Money.ToBaseMinor(form.Amount, rate), Status = Open, CreatedAt = now, UpdatedAt = now,
        };
        (refund.Number, refund.SequenceNumber) = await NextNumberAsync(db, db.SupplierRefunds, companyId,
            "supplier_refund_number_format", "{{SERIES:SR}}{{DELIMITER:-}}{{SEQUENCE:6}}");
        db.SupplierRefunds.Add(refund);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return refund.Id;
    }

    /// void(): a payment releases its allocations first, and cannot be voided while it has
    /// open refunds.
    public async Task VoidPaymentAsync(int companyId, long paymentId, string reason)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var payment = await db.SupplierPayments.SingleAsync(p => p.Id == paymentId && p.CompanyId == companyId);
        if (payment.Status == Void)
            return;
        Ensure(!string.IsNullOrWhiteSpace(reason), Loc.T("Indiquez le motif de l'annulation."));
        Ensure(!await db.SupplierRefunds.AnyAsync(r => r.SupplierPaymentId == paymentId && r.Status == Open),
            Loc.T("Ce paiement a des remboursements : annulez-les d'abord."));
        await ReplaceAllocationsAsync(db, payment, []);
        MarkVoid(payment, reason);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task VoidRefundAsync(int companyId, long refundId, string reason)
    {
        await using var db = dbs.Create();
        var refund = await db.SupplierRefunds.SingleAsync(r => r.Id == refundId && r.CompanyId == companyId);
        if (refund.Status == Void)
            return;
        Ensure(!string.IsNullOrWhiteSpace(reason), Loc.T("Indiquez le motif de l'annulation."));
        MarkVoid(refund, reason);
        await db.SaveChangesAsync();
    }

    /// available_amount: what an open payment or credit has left after its allocations and
    /// open refunds.
    public static async Task<long> AvailableAsync(ApplicationDbContext db, PurchaseRecord source)
    {
        if (source.Status != Open)
            return 0;
        var (total, allocated, refunded) = source switch
        {
            SupplierPayment p => (p.Amount,
                await db.SupplierPaymentAllocations.Where(a => a.SupplierPaymentId == p.Id).SumAsync(a => (long?)a.Amount) ?? 0,
                await db.SupplierRefunds.Where(r => r.SupplierPaymentId == p.Id && r.Status == Open).SumAsync(r => (long?)r.Amount) ?? 0),
            SupplierCredit c => (c.Total,
                await db.SupplierCreditAllocations.Where(a => a.SupplierCreditId == c.Id).SumAsync(a => (long?)a.Amount) ?? 0,
                await db.SupplierRefunds.Where(r => r.SupplierCreditId == c.Id && r.Status == Open).SumAsync(r => (long?)r.Amount) ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        return total - allocated - refunded;
    }

    private static async Task ReplaceAllocationsAsync(ApplicationDbContext db, PurchaseRecord source, List<BillShare> rows)
    {
        Ensure(source.Status == Open, Loc.T("Seul un paiement ou un avoir ouvert peut être affecté."));
        Ensure(rows.Count <= 200 && rows.All(r => r.Amount is >= 1 and <= MoneyLimit), Loc.T("Chaque montant affecté doit être positif."));
        Ensure(rows.Select(r => r.BillId).Distinct().Count() == rows.Count, Loc.T("Une facture apparaît deux fois dans la répartition."));

        var (total, baseTotal) = source switch
        {
            SupplierPayment p => (p.Amount, p.BaseAmount),
            SupplierCredit c => (c.Total, c.BaseTotal),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        var isPayment = source is SupplierPayment;
        List<BillAllocation> existing = isPayment
            ? [.. await db.SupplierPaymentAllocations.Where(a => a.SupplierPaymentId == source.Id).ToListAsync()]
            : [.. await db.SupplierCreditAllocations.Where(a => a.SupplierCreditId == source.Id).ToListAsync()];
        var refunded = await db.SupplierRefunds
            .Where(r => r.Status == Open && (isPayment ? r.SupplierPaymentId == source.Id : r.SupplierCreditId == source.Id))
            .SumAsync(r => (long?)r.Amount) ?? 0;
        Ensure(rows.Sum(r => r.Amount) <= total - refunded, Loc.T("La somme affectée dépasse le solde disponible."));

        var ids = existing.Select(a => a.BillId).Concat(rows.Select(r => r.BillId)).Distinct().ToList();
        var bills = await db.Bills.Where(b => b.CompanyId == source.CompanyId && ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
        foreach (var row in rows)
        {
            var bill = bills.GetValueOrDefault(row.BillId);
            Ensure(bill is not null && bill.SupplierId == source.SupplierId && bill.CurrencyId == source.CurrencyId && bill.Status == Open,
                Loc.T("Chaque facture doit être ouverte, de ce fournisseur et dans cette devise."));
            await RecalculateAsync(db, bill!);
            var own = existing.Where(a => a.BillId == bill!.Id).Sum(a => a.Amount);
            Ensure(row.Amount <= bill!.DueAmount + own, Loc.F("Le montant affecté à {0} dépasse son reste à payer.", bill.Number));
        }

        db.RemoveRange(existing);
        long used = 0;
        var now = Timestamp.Now();
        foreach (var row in rows.OrderBy(r => r.BillId))
        {
            var baseAmount = ProportionalAmount.Slice(baseTotal, used, used + row.Amount, total);
            used += row.Amount;
            BillAllocation allocation = isPayment
                ? new SupplierPaymentAllocation { SupplierPaymentId = source.Id }
                : new SupplierCreditAllocation { SupplierCreditId = source.Id };
            (allocation.CompanyId, allocation.BillId, allocation.Amount, allocation.BaseAmount) = (source.CompanyId, row.BillId, row.Amount, baseAmount);
            allocation.CreatedAt = allocation.UpdatedAt = now;
            db.Add((object)allocation);
            bills[row.BillId].FinancialLockedAt ??= now;
        }
        await db.SaveChangesAsync();

        foreach (var bill in bills.Values)
            await RecalculateAsync(db, bill);
    }

    /// recalculate(): a bill's balance worked out again from its allocations.
    private static async Task RecalculateAsync(ApplicationDbContext db, Bill bill)
    {
        var paid = await db.SupplierPaymentAllocations.Where(a => a.BillId == bill.Id).SumAsync(a => (long?)a.Amount) ?? 0;
        var credited = await db.SupplierCreditAllocations.Where(a => a.BillId == bill.Id).SumAsync(a => (long?)a.Amount) ?? 0;
        var due = bill.Total - paid - credited;
        Ensure(due >= 0, Loc.T("Les règlements dépassent le total de la facture."));
        bill.DueAmount = due;
        bill.BaseDueAmount = ProportionalAmount.Floor(bill.BaseTotal, due, bill.Total);
        await db.SaveChangesAsync();
    }

    /// Bill::settlement_status.
    public static string SettlementStatus(Bill bill)
        => bill.DueAmount == 0 ? "SETTLED" : bill.DueAmount < bill.Total ? "PARTIAL" : "UNPAID";

    // ---- lines ------------------------------------------------------------------

    /// composeItems(): the company's purchase taxes applied, amounts computed as the
    /// document form computes them (taxes and percentage discounts per line), and the
    /// base-currency total split across the lines and their taxes.
    private static async Task<List<PurchaseItem>> ComposeAsync(ApplicationDbContext db, int companyId, List<PurchaseLine> input, decimal rate, bool included)
    {
        await CheckLinesAsync(db, companyId, input);
        var taxIds = input.SelectMany(l => l.TaxTypeIds).Distinct().ToList();
        var types = await db.TaxTypes.Where(t => t.CompanyId == companyId && t.Type == "GENERAL" && t.TransactionType == "purchases" && taxIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        var draft = new DocumentDraft { TaxPerItem = true, DiscountPerItem = true, TaxIncluded = included };
        foreach (var line in input)
        {
            Ensure(line.Price * line.Quantity <= MoneyLimit, Loc.T("Une ligne dépasse le montant maximal."));
            Ensure(line.TaxTypeIds.Count <= 10 && line.TaxTypeIds.Distinct().Count() == line.TaxTypeIds.Count, Loc.T("Une taxe apparaît deux fois sur une ligne."));
            draft.Items.Add(new DraftItem
            {
                Name = line.Description, Quantity = line.Quantity, Price = line.Price, Discount = line.Discount, DiscountType = "percentage",
                Taxes = line.TaxTypeIds.Select(id => types.GetValueOrDefault(id) is { } t
                    ? new DraftTax
                    {
                        TaxTypeId = t.Id, Name = t.Name, Percent = t.Percent, CalculationType = t.CalculationType,
                        FixedAmount = t.FixedAmount, Compound = t.CompoundTax != 0,
                    }
                    : throw new SalesException("Taxe d'achat inconnue.")).ToList(),
            });
        }
        draft.Recalculate();

        var lineTotals = draft.Items.Select(i => i.Total + (included ? i.Taxes.Where(t => t.Compound).Sum(t => t.Amount) : i.Tax)).ToList();
        var documentTotal = lineTotals.Sum();
        Ensure(documentTotal is > 0 and <= MoneyLimit, Loc.T("Le total du document doit être positif et ne pas dépasser le montant maximal."));

        var documentBase = Money.ToBaseMinor(documentTotal, rate);
        long used = 0;
        var items = new List<PurchaseItem>();
        for (var k = 0; k < input.Count; k++)
        {
            var (line, amounts, total) = (input[k], draft.Items[k], lineTotals[k]);
            var lineBase = ProportionalAmount.Slice(documentBase, used, used + total, documentTotal);
            var position = total - amounts.Tax;
            var taxes = new List<PurchaseTax>();
            foreach (var tax in amounts.Taxes)
            {
                taxes.Add(new PurchaseTax(tax.Name, tax.Percent, tax.CalculationType, tax.FixedAmount, tax.Compound, tax.TaxTypeId,
                    tax.Amount, ProportionalAmount.Slice(lineBase, position, position + tax.Amount, total)));
                position += tax.Amount;
            }
            items.Add(new BillItem
            {
                CompanyId = companyId, Description = line.Description, ExpenseCategoryId = line.ExpenseCategoryId,
                Quantity = line.Quantity, Price = line.Price, Discount = line.Discount, DiscountType = "percentage",
                SubTotal = amounts.Subtotal, DiscountVal = amounts.DiscountVal, Tax = amounts.Tax, Total = total, BaseTotal = lineBase,
                Taxes = JsonSerializer.Serialize(taxes, Json),
            });
            used += total;
        }
        return items;
    }

    /// creditBillItems(): each credited quantity takes the matching slice of its source
    /// line's amounts and taxes, after what earlier credits already took, so partial
    /// credits add up exactly.
    private static async Task<List<PurchaseItem>> CreditBillItemsAsync(ApplicationDbContext db, Bill bill, List<PurchaseLine> input)
    {
        Ensure(input.Count is >= 1 and <= 200, Loc.T("Ajoutez au moins une ligne à créditer."));
        Ensure(input.Select(l => l.SourceBillItemId).Distinct().Count() == input.Count, Loc.T("Une ligne de la facture apparaît deux fois."));
        var sources = await db.BillItems.Where(i => i.BillId == bill.Id).ToDictionaryAsync(i => i.Id);
        var items = new List<PurchaseItem>();
        foreach (var line in input)
        {
            var source = line.SourceBillItemId is { } sid ? sources.GetValueOrDefault(sid) : null;
            Ensure(source is not null, "Ligne de facture inconnue.");
            Ensure(line.Quantity is > 0 and <= 1000000 && decimal.Round(line.Quantity, 2) == line.Quantity, Loc.T("Quantité invalide."));
            var previous = await db.SupplierCreditItems
                .Where(i => i.SourceBillItemId == source!.Id
                    && db.SupplierCredits.Any(c => c.Id == i.SupplierCreditId && c.Status != Void))
                .Select(i => i.Quantity).ToListAsync();
            long before = previous.Sum(CreditNoteAmounts.ToHundredths);
            var after = before + CreditNoteAmounts.ToHundredths(line.Quantity);
            long whole = CreditNoteAmounts.ToHundredths(source!.Quantity);
            Ensure(after <= whole, Loc.F("« {0} » : quantité créditée supérieure à la quantité facturée.", source.Description));

            long Slice(long amount) => ProportionalAmount.Slice(amount, before, after, whole);
            var taxes = (JsonSerializer.Deserialize<List<PurchaseTax>>(source.Taxes ?? "[]", Json) ?? [])
                .Select(t => t with { Amount = Slice(t.Amount), BaseAmount = Slice(t.BaseAmount) }).ToList();
            items.Add(new BillItem
            {
                CompanyId = source.CompanyId, Description = source.Description, ExpenseCategoryId = source.ExpenseCategoryId,
                Price = source.Price, Discount = source.Discount, DiscountType = source.DiscountType,
                Quantity = line.Quantity, SourceBillItemId = source.Id,
                SubTotal = Slice(source.SubTotal), DiscountVal = Slice(source.DiscountVal), Total = Slice(source.Total),
                BaseTotal = Slice(source.BaseTotal),
                // Derived from the individual snapshots, preserving every last minor unit.
                Tax = taxes.Sum(t => t.Amount),
                Taxes = JsonSerializer.Serialize(taxes, Json),
            });
        }
        return items;
    }

    private static async Task CheckLinesAsync(ApplicationDbContext db, int companyId, List<PurchaseLine> lines)
    {
        Ensure(lines.Count is >= 1 and <= 200, "Ajoutez au moins une ligne.");
        var categories = lines.Select(l => l.ExpenseCategoryId).Distinct().ToList();
        Ensure(await db.ExpenseCategories.CountAsync(c => c.CompanyId == companyId && categories.Contains(c.Id)) == categories.Count,
            Loc.T("Chaque ligne doit avoir une catégorie de dépense."));
        foreach (var line in lines)
        {
            Ensure(!string.IsNullOrWhiteSpace(line.Description) && line.Description.Length <= 1000, Loc.T("Chaque ligne doit avoir une description."));
            Ensure(line.Quantity is > 0 and <= 1000000 && decimal.Round(line.Quantity, 2) == line.Quantity, Loc.T("Quantité invalide."));
            Ensure(line.Price is >= 0 and <= MoneyLimit, "Prix invalide.");
            Ensure(line.Discount is >= 0 and <= 100, Loc.T("La remise est un pourcentage entre 0 et 100."));
        }
    }

    private static void SetTotals(PurchaseDocument document, List<PurchaseItem> items)
    {
        document.Total = items.Sum(i => i.Total);
        Ensure(document.Total > 0, Loc.T("Le document est vide."));
        document.SubTotal = items.Sum(i => i.SubTotal);
        document.Tax = items.Sum(i => i.Tax);
        document.BaseTotal = items.Sum(i => i.BaseTotal);
    }

    /// unchanged(): whether a submitted bill carries the stored financial details.
    private static bool Unchanged(Bill bill, List<BillItem> items, PurchaseForm form, decimal rate)
    {
        static string Line(string description, int category, decimal quantity, long price, decimal discount, IEnumerable<int> taxes)
            => $"{description}\u0001{category}\u0001{quantity:0.##}\u0001{price}\u0001{discount:0.##}\u0001{string.Join(",", taxes)}";
        return bill.SupplierId == form.SupplierId && bill.CurrencyId == form.CurrencyId && bill.ExchangeRate == rate
            && bill.DocumentDate == form.DocumentDate && bill.TaxIncluded == form.TaxIncluded
            && items.Select(i => Line(i.Description, i.ExpenseCategoryId, i.Quantity, i.Price, i.Discount, TaxIds(i)))
                .SequenceEqual(form.Items.Select(l => Line(l.Description, l.ExpenseCategoryId, l.Quantity, l.Price, l.Discount, l.TaxTypeIds)));
    }

    public static List<int> TaxIds(PurchaseItem item)
        => (JsonSerializer.Deserialize<List<PurchaseTax>>(item.Taxes ?? "[]", Json) ?? []).Select(t => t.TaxTypeId).ToList();

    private static T Copy<T>(PurchaseItem s, DateTime now, Action<T> parent) where T : PurchaseItem, new()
    {
        var item = new T
        {
            CompanyId = s.CompanyId, ExpenseCategoryId = s.ExpenseCategoryId, SourceBillItemId = s.SourceBillItemId,
            Description = s.Description, Quantity = s.Quantity, Price = s.Price, Discount = s.Discount, DiscountType = s.DiscountType,
            SubTotal = s.SubTotal, DiscountVal = s.DiscountVal, Tax = s.Tax, Total = s.Total, BaseTotal = s.BaseTotal, Taxes = s.Taxes,
            CreatedAt = now, UpdatedAt = now,
        };
        parent(item);
        return item;
    }

    // ---- shared -----------------------------------------------------------------

    /// What a document keeps of its supplier as it was when it was recorded.
    private static string Snapshot(Supplier s) => new JsonObject
    {
        ["name"] = s.Name, ["contact_name"] = s.ContactName, ["email"] = s.Email, ["tax_id"] = s.TaxId,
        ["addresses"] = s.Addresses is null ? null : JsonNode.Parse(s.Addresses),
    }.ToJsonString();

    private static async Task<Supplier> SupplierAsync(ApplicationDbContext db, int companyId, long supplierId)
        => await db.Suppliers.SingleOrDefaultAsync(s => s.Id == supplierId && s.CompanyId == companyId)
           ?? throw new SalesException("Choisissez un fournisseur.");

    private static async Task<PurchaseRecord> SourceAsync(ApplicationDbContext db, int companyId, string kind, long id) => kind switch
    {
        "payment" => (PurchaseRecord?)await db.SupplierPayments.SingleOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId),
        "credit" => await db.SupplierCredits.SingleOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId),
        _ => null,
    } ?? throw new SalesException(Loc.T("Paiement ou avoir introuvable."));

    /// PurchaseInputs::money(): the base currency always takes a rate of 1, any other a
    /// positive one.
    private static async Task<decimal> RateAsync(ApplicationDbContext db, int companyId, int currencyId, decimal rate)
    {
        Ensure(await db.Currencies.AnyAsync(c => c.Id == currencyId), "Choisissez une devise.");
        var companyCurrency = await db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == "currency").Select(s => s.Value).FirstOrDefaultAsync();
        if (companyCurrency == currencyId.ToString())
            return 1;
        Ensure(rate is > 0 and <= 1000000, Loc.T("Indiquez le taux de change."));
        return rate;
    }

    private static async Task CheckMethodAsync(ApplicationDbContext db, int companyId, int? methodId)
        => Ensure(methodId is null || await db.PaymentMethods.AnyAsync(m => m.Id == methodId && m.CompanyId == companyId), Loc.T("Moyen de paiement introuvable."));

    private static void CheckMoney(long amount) => Ensure(amount is >= 1 and <= MoneyLimit, Loc.T("Le montant doit être positif."));

    /// DocumentNumberAssigner: the company's `<kind>_number_format`, one sequence per kind.
    private static async Task<(string, int)> NextNumberAsync<T>(ApplicationDbContext db, IQueryable<T> set, int companyId, string setting, string fallback)
        where T : PurchaseRecord
    {
        var sequence = (await set.Where(r => r.CompanyId == companyId).MaxAsync(r => r.SequenceNumber) ?? 0) + 1;
        var format = await db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == setting).Select(s => s.Value).FirstOrDefaultAsync();
        return (SerialNumbers.Generate(string.IsNullOrEmpty(format) ? fallback : format, sequence, 0, null, DateTime.Now), sequence);
    }

    private static void MarkVoid(PurchaseRecord record, string reason)
    {
        record.Status = Void;
        record.VoidedAt = record.UpdatedAt = Timestamp.Now();
        record.VoidReason = reason;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new SalesException(message);
    }
}

/// Port of App\Support\ProportionalAmount: integer apportionment of an amount.
public static class ProportionalAmount
{
    public static long Floor(long amount, long part, long whole)
    {
        if (whole <= 0 || part <= 0)
            return 0;
        if (part >= whole)
            return amount;
        // intdiv(amount, whole) * part + floor((amount % whole) * part / whole), as upstream.
        return amount / whole * part + (long)((Int128)(amount % whole) * part / whole);
    }

    public static long Slice(long amount, long before, long after, long whole) => Floor(amount, after, whole) - Floor(amount, before, whole);
}
