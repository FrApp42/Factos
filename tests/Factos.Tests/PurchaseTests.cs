using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Bills, supplier payments, supplier credits and refunds (PurchaseDocumentService +
/// SupplierSettlementService).
public class PurchaseTests : ApiTestBase
{
    private int _company, _currency, _category, _vat;
    private long _supplier;
    private Purchases Purchases => Service<Purchases>();

    private async Task SetUpAsync()
    {
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = await db.Currencies.FirstAsync(c => c.Code == "EUR");
            var company = await Companies.CreateAsync(db, owner, "Achats SA", eur.Id);
            var category = new ExpenseCategory { Name = "Fournitures", CompanyId = company.Id };
            var vat = new TaxType { Name = "TVA déductible", Percent = 20, CompanyId = company.Id, TransactionType = "purchases" };
            var supplier = new Supplier { Name = "Grossiste", CompanyId = company.Id, CurrencyId = eur.Id };
            db.AddRange(category, vat, supplier);
            await db.SaveChangesAsync();
            (_company, _currency, _category, _vat, _supplier) = (company.Id, eur.Id, category.Id, vat.Id, supplier.Id);
        });
    }

    private Task<long> BillAsync(long price, decimal quantity, string status = Purchases.Open)
        => Purchases.SaveBillAsync(_company, Jane, null, new PurchaseForm
        {
            SupplierId = _supplier, CurrencyId = _currency, Status = status, DueDate = DateOnly.FromDateTime(DateTime.Today),
            Items = [new PurchaseLine { Description = "Papier", ExpenseCategoryId = _category, Price = price, Quantity = quantity, TaxTypeIds = [_vat] }],
        });

    private SupplierMoneyForm Money(long amount, params BillShare[] allocations)
        => new() { SupplierId = _supplier, CurrencyId = _currency, Amount = amount, Allocations = [.. allocations] };

    private async Task<T> Find<T>(long id) where T : PurchaseRecord
    {
        T? row = null;
        await WithDb(async db => row = await db.Set<T>().AsNoTracking().SingleAsync(r => r.Id == id));
        return row!;
    }

    private async Task<long> AvailableAsync(PurchaseRecord source)
    {
        long available = 0;
        await WithDb(async db => available = await Purchases.AvailableAsync(db, source));
        return available;
    }

    [Fact]
    public async Task A_payment_advance_is_refunded_and_voiding_reopens_the_bill()
    {
        await SetUpAsync();
        var draftId = await BillAsync(10000, 1, Purchases.Draft);
        var draft = await Find<Bill>(draftId);
        Assert.Equal(("BILL-000001", 10000L, 2000L, 12000L, 12000L, 12000L), (draft.Number, draft.SubTotal, draft.Tax, draft.Total, draft.BaseTotal, draft.DueAmount));
        await Assert.ThrowsAsync<SalesException>(() => Purchases.RecordPaymentAsync(_company, Jane, Money(12000, new BillShare(draftId, 12000))));
        await Purchases.OpenBillAsync(_company, draftId);

        var paymentId = await Purchases.RecordPaymentAsync(_company, Jane, Money(15000, new BillShare(draftId, 12000)));
        var bill = await Find<Bill>(draftId);
        Assert.Equal((0L, "SETTLED"), (bill.DueAmount, Purchases.SettlementStatus(bill)));
        Assert.NotNull(bill.FinancialLockedAt);
        var payment = await Find<SupplierPayment>(paymentId);
        Assert.Equal(("SP-000001", 3000L), (payment.Number, await AvailableAsync(payment)));

        // A refund comes out of what the payment has left, never more.
        await Assert.ThrowsAsync<SalesException>(() => Purchases.RecordRefundAsync(_company, Jane, new SupplierMoneyForm { Amount = 3001, SupplierPaymentId = paymentId }));
        var refundId = await Purchases.RecordRefundAsync(_company, Jane, new SupplierMoneyForm { Amount = 3000, SupplierPaymentId = paymentId });
        var refund = await Find<SupplierRefund>(refundId);
        Assert.Equal(("SR-000001", _supplier, _currency, 3000L), (refund.Number, refund.SupplierId, refund.CurrencyId, refund.BaseAmount));
        Assert.Equal(0L, await AvailableAsync(payment));

        // Cash out: 150.00 paid, 30.00 given back.
        await WithDb(async db =>
        {
            var series = await Cashflow.FiscalYearAsync(db, _company, "1-12", DateOnly.FromDateTime(DateTime.Today));
            Assert.Equal(12000L, series.TotalExpenses);
        });

        await Assert.ThrowsAsync<SalesException>(() => Purchases.VoidPaymentAsync(_company, paymentId, "Erreur"));
        await Assert.ThrowsAsync<SalesException>(() => Purchases.VoidRefundAsync(_company, refundId, " "));
        await Purchases.VoidRefundAsync(_company, refundId, "Erreur");
        Assert.Equal(3000L, await AvailableAsync(payment));
        await Purchases.VoidPaymentAsync(_company, paymentId, "Erreur");
        Assert.Equal((12000L, Purchases.Void), ((await Find<Bill>(draftId)).DueAmount, (await Find<SupplierPayment>(paymentId)).Status));
    }

    [Fact]
    public async Task Partial_credits_telescope_to_the_bill_and_are_allocated_or_refunded()
    {
        await SetUpAsync();
        var billId = await BillAsync(3333, 3);              // 99.99 + 20.00 tax = 119.99
        var bill = await Find<Bill>(billId);
        Assert.Equal(11999L, bill.Total);
        long itemId = 0;
        await WithDb(async db => itemId = (await db.BillItems.SingleAsync(i => i.BillId == billId)).Id);

        PurchaseForm Credit(decimal quantity) => new()
        {
            SupplierId = _supplier, CurrencyId = _currency, SourceBillId = billId,
            Items = [new PurchaseLine { SourceBillItemId = itemId, Quantity = quantity }],
        };
        var firstId = await Purchases.CreateCreditAsync(_company, Jane, Credit(1));
        await Assert.ThrowsAsync<SalesException>(() => Purchases.CreateCreditAsync(_company, Jane, Credit(2.01m)));
        var secondId = await Purchases.CreateCreditAsync(_company, Jane, Credit(2));
        var (first, second) = (await Find<SupplierCredit>(firstId), await Find<SupplierCredit>(secondId));
        Assert.Equal(("SC-000001", Purchases.Open), (first.Number, first.Status));
        Assert.Equal((bill.Total, bill.Tax, bill.BaseTotal),
            (first.Total + second.Total, first.Tax + second.Tax, first.BaseTotal + second.BaseTotal));

        // The first credit settles part of the bill; the second goes back as cash.
        await Purchases.ReplaceAllocationsAsync(_company, "credit", firstId, [new BillShare(billId, first.Total)]);
        Assert.Equal(bill.Total - first.Total, (await Find<Bill>(billId)).DueAmount);
        await Assert.ThrowsAsync<SalesException>(() => Purchases.ReplaceAllocationsAsync(_company, "credit", secondId, [new BillShare(billId, second.Total + 1)]));
        var refundId = await Purchases.RecordRefundAsync(_company, Jane, new SupplierMoneyForm { Amount = second.Total, SupplierCreditId = secondId });
        Assert.Equal(0L, await AvailableAsync(second));
        await Assert.ThrowsAsync<SalesException>(() => Purchases.ReplaceAllocationsAsync(_company, "credit", secondId, [new BillShare(billId, 1)]));

        await Assert.ThrowsAsync<SalesException>(() => Purchases.VoidCreditAsync(_company, secondId, "Erreur"));  // refunded
        await Assert.ThrowsAsync<SalesException>(() => Purchases.VoidBillAsync(_company, billId, "Erreur"));     // credited
        await Purchases.VoidRefundAsync(_company, refundId, "Erreur");
        await Purchases.VoidCreditAsync(_company, secondId, "Erreur");

        // A settled bill keeps its financials; other details still change.
        var edit = new PurchaseForm
        {
            SupplierId = _supplier, CurrencyId = _currency, Status = Purchases.Open, DocumentDate = bill.DocumentDate,
            DueDate = bill.DocumentDate.AddDays(30), Reference = "BC-42",
            Items = [new PurchaseLine { Description = "Papier", ExpenseCategoryId = _category, Price = 3333, Quantity = 3, TaxTypeIds = [_vat] }],
        };
        await Purchases.SaveBillAsync(_company, Jane, billId, edit);
        Assert.Equal("BC-42", (await Find<Bill>(billId)).Reference);
        edit.Items[0].Price = 4000;
        await Assert.ThrowsAsync<SalesException>(() => Purchases.SaveBillAsync(_company, Jane, billId, edit));
    }

    [Fact]
    public void Proportional_slices_add_up_to_the_whole()
    {
        var parts = new[] { 0L, 33, 67, 100 };
        var slices = parts.Zip(parts.Skip(1), (a, b) => ProportionalAmount.Slice(1001, a, b, 100)).ToList();
        Assert.Equal(1001L, slices.Sum());
        Assert.Equal([330L, 340L, 331L], slices);
    }
}
