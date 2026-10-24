using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Credit notes (CreditNoteService) and customer credit (applyCustomerCredits).
public class CreditNoteTests : ApiTestBase
{
    private int _company, _vat;
    private long _customer;
    private Sales Sales => Service<Sales>();
    private CreditNotes Credits => Service<CreditNotes>();

    private async Task SetUpAsync()
    {
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = await db.Currencies.FirstAsync(c => c.Code == "EUR");
            var company = await Companies.CreateAsync(db, owner, "Avoirs SA", eur.Id);
            var customer = new Customer { Name = "Client", CompanyId = company.Id, CurrencyId = eur.Id };
            var vat = new TaxType { Name = "TVA", Percent = 20, CompanyId = company.Id };
            db.AddRange(customer, vat);
            await db.SaveChangesAsync();
            (_company, _customer, _vat) = (company.Id, customer.Id, vat.Id);
        });
    }

    private async Task<int> IssuedInvoiceAsync(long price, decimal quantity, decimal discountPercent = 0)
    {
        var id = await Sales.SaveInvoiceAsync(_company, Jane, new DocumentForm
        {
            CustomerId = _customer,
            Number = await Sales.NextNumberAsync(_company, "invoice", _customer),
            Draft = new DocumentDraft
            {
                Items = [new DraftItem { Name = "Prestation", Price = price, Quantity = quantity }],
                Taxes = [new DraftTax { TaxTypeId = _vat, Name = "TVA", Percent = 20 }],
                Discount = discountPercent, DiscountType = "percentage",
            },
        });
        await Sales.MarkInvoiceSentAsync(_company, id);
        return id;
    }

    private async Task<Invoice> InvoiceAsync(int id)
    {
        Invoice? invoice = null;
        await WithDb(async db => invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == id));
        return invoice!;
    }

    private async Task<int> ItemIdAsync(int invoiceId)
    {
        var id = 0;
        await WithDb(async db => id = (await db.InvoiceItems.FirstAsync(i => i.InvoiceId == invoiceId)).Id);
        return id;
    }

    [Fact]
    public async Task A_full_credit_note_reverses_the_invoice_and_closes_it()
    {
        await SetUpAsync();
        var invoiceId = await IssuedInvoiceAsync(10000, 1);   // 120.00

        var noteId = await Credits.CreateAsync(_company, Jane, invoiceId, null, "Erreur de facturation");

        var note = await InvoiceAsync(noteId);
        Assert.Equal(("CREDIT_NOTE", "CN-000001", -12000L, -2000L), (note.Type, note.InvoiceNumber, note.Total, note.Tax));
        Assert.Equal((Sales.Draft, Sales.Paid, 0L, invoiceId), (note.Status, note.PaidStatus, note.DueAmount, note.RelatedInvoiceId!.Value));
        var original = await InvoiceAsync(invoiceId);
        Assert.Equal((0L, Sales.Completed, Sales.Paid), (original.DueAmount, original.Status, original.PaidStatus));

        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, invoiceId, null, null)); // nothing left
        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, noteId, null, null));    // credit of a credit

        // Deleting the credit note gives the amount back to the invoice.
        await Sales.DeleteInvoiceAsync(_company, noteId);
        Assert.Equal((12000L, Sales.Unpaid), ((await InvoiceAsync(invoiceId)).DueAmount, (await InvoiceAsync(invoiceId)).PaidStatus));
    }

    [Fact]
    public async Task Partial_credits_telescope_to_the_original_to_the_cent()
    {
        await SetUpAsync();
        // 3 × 3.33 with 10 % off and 20 % VAT: every partial credit rounds.
        var invoiceId = await IssuedInvoiceAsync(333, 3, discountPercent: 10);
        var original = await InvoiceAsync(invoiceId);
        var item = await ItemIdAsync(invoiceId);

        var notes = new List<int>();
        foreach (var quantity in new[] { 1m, 1m, 1m })
            notes.Add(await Credits.CreateAsync(_company, Jane, invoiceId, new() { [item] = quantity }, null));

        var credited = (await Task.WhenAll(notes.Select(InvoiceAsync))).ToList();
        Assert.Equal(-original.Total, credited.Sum(n => n.Total));
        Assert.Equal(-original.Tax, credited.Sum(n => n.Tax));
        Assert.Equal(-original.SubTotal, credited.Sum(n => n.SubTotal));
        Assert.Equal(-(original.DiscountVal ?? 0), credited.Sum(n => n.DiscountVal ?? 0));
        Assert.Equal(0, (await InvoiceAsync(invoiceId)).DueAmount);

        // Nothing left to credit on the line.
        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, invoiceId, new() { [item] = 0.5m }, null));
    }

    [Fact]
    public async Task A_credit_never_exceeds_what_is_still_unpaid()
    {
        await SetUpAsync();
        var invoiceId = await IssuedInvoiceAsync(10000, 2);   // 240.00
        var item = await ItemIdAsync(invoiceId);
        await Sales.SavePaymentAsync(_company, Jane, new PaymentForm
        {
            CustomerId = _customer, Amount = 20000, Number = "PAY-1",
            Allocations = [new Allocation { InvoiceId = invoiceId, Amount = 20000 }],
        });

        // 40.00 still unpaid: crediting one unit (120.00) is too much.
        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, invoiceId, new() { [item] = 1 }, null));
        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, invoiceId, new() { [item] = 5 }, null));

        var draft = await Sales.SaveInvoiceAsync(_company, Jane, new DocumentForm
        {
            CustomerId = _customer, Number = "INV-DRAFT",
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "x", Price = 100, Quantity = 1 }] },
        });
        await Assert.ThrowsAsync<SalesException>(() => Credits.CreateAsync(_company, Jane, draft, null, null));
    }

    [Fact]
    public async Task Unallocated_payment_credit_settles_another_invoice()
    {
        await SetUpAsync();
        var first = await IssuedInvoiceAsync(10000, 1);   // 120.00
        var second = await IssuedInvoiceAsync(5000, 1);   //  60.00
        var paymentId = await Sales.SavePaymentAsync(_company, Jane, new PaymentForm
        {
            CustomerId = _customer, Amount = 20000, Number = "PAY-1",
            Allocations = [new Allocation { InvoiceId = first, Amount = 12000 }],
        });

        var credit = Assert.Single(await Sales.AvailableCreditsAsync(_company, _customer));
        Assert.Equal(8000, credit.Available);

        await Assert.ThrowsAsync<SalesException>(() => Sales.ApplyCreditAsync(_company, paymentId, second, 9000));  // > credit left
        await Sales.ApplyCreditAsync(_company, paymentId, second, 6000);

        Assert.Equal(Sales.Paid, (await InvoiceAsync(second)).PaidStatus);
        Assert.Equal(2000, Assert.Single(await Sales.AvailableCreditsAsync(_company, _customer)).Available);
    }
}
