using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// The invoice life cycle against a real database: numbering, stored totals, payments
/// and the balance/status rules of InvoiceBalanceService.
public class SalesTests : ApiTestBase
{
    private int _company;
    private long _customer;
    private int _vat;
    private Sales Sales => new(Service<AppDbFactory>());

    private async Task SetUpCompanyAsync()
    {
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = new Currency { Name = "Euro", Code = "EUR", Symbol = "€", Precision = 2, ThousandSeparator = ".", DecimalSeparator = "," };
            db.Currencies.Add(eur);
            await db.SaveChangesAsync();
            var company = await Companies.CreateAsync(db, owner, "Ventes SA", eur.Id);
            var customer = new Customer { Name = "Client", CompanyId = company.Id, CurrencyId = eur.Id };
            var vat = new TaxType { Name = "TVA", Percent = 20, CompanyId = company.Id };
            db.Customers.Add(customer);
            db.TaxTypes.Add(vat);
            await db.SaveChangesAsync();
            (_company, _customer, _vat) = (company.Id, customer.Id, vat.Id);
        });
    }

    private async Task<int> NewInvoiceAsync(long priceCents)
    {
        var form = new DocumentForm
        {
            CustomerId = _customer,
            Number = await Sales.NextNumberAsync(_company, "invoice", _customer),
            Draft = new DocumentDraft
            {
                Items = [new DraftItem { Name = "Prestation", Price = priceCents, Quantity = 1 }],
                Taxes = [new DraftTax { TaxTypeId = _vat, Name = "TVA", Percent = 20 }],
            },
        };
        return await Sales.SaveInvoiceAsync(_company, Jane, form);
    }

    private async Task<Invoice> InvoiceAsync(int id)
    {
        Invoice? invoice = null;
        await WithDb(async db => invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == id));
        return invoice!;
    }

    [Fact]
    public async Task Invoices_are_numbered_and_store_server_totals()
    {
        await SetUpCompanyAsync();
        var first = await NewInvoiceAsync(10000);
        var second = await NewInvoiceAsync(5000);

        var invoice = await InvoiceAsync(first);
        Assert.Equal("INV-000001", invoice.InvoiceNumber);
        Assert.Equal("INV-000002", (await InvoiceAsync(second)).InvoiceNumber);
        Assert.Equal((10000L, 2000L, 12000L, 12000L), (invoice.SubTotal, invoice.Tax, invoice.Total, invoice.DueAmount));
        Assert.Equal((Sales.Draft, Sales.Unpaid), (invoice.Status, invoice.PaidStatus));
        Assert.Equal(12000, invoice.BaseTotal); // company currency: rate 1
    }

    [Fact]
    public async Task Payments_move_the_balance_and_statuses_both_ways()
    {
        await SetUpCompanyAsync();
        var id = await NewInvoiceAsync(10000); // 120.00 with tax

        await Assert.ThrowsAsync<SalesException>(() => Sales.SavePaymentAsync(_company, Jane, new PaymentForm { CustomerId = _customer, Amount = 100, Number = "PAY-1", Allocations = [new() { InvoiceId = id, Amount = 100 }] })); // draft
        await Sales.MarkInvoiceSentAsync(_company, id);

        var first = await Sales.SavePaymentAsync(_company, Jane, new PaymentForm { CustomerId = _customer, Amount = 5000, Number = "PAY-1", Allocations = [new() { InvoiceId = id, Amount = 5000 }] });
        var invoice = await InvoiceAsync(id);
        Assert.Equal((7000L, Sales.Sent, Sales.PartiallyPaid), (invoice.DueAmount, invoice.Status, invoice.PaidStatus));

        await Assert.ThrowsAsync<SalesException>(() => Sales.SavePaymentAsync(_company, Jane, new PaymentForm { CustomerId = _customer, Amount = 7001, Number = "PAY-2", Allocations = [new() { InvoiceId = id, Amount = 7001 }] })); // overpayment

        await Sales.SavePaymentAsync(_company, Jane, new PaymentForm { CustomerId = _customer, Amount = 7000, Number = "PAY-2", Allocations = [new() { InvoiceId = id, Amount = 7000 }] });
        invoice = await InvoiceAsync(id);
        Assert.Equal((0L, Sales.Completed, Sales.Paid), (invoice.DueAmount, invoice.Status, invoice.PaidStatus));

        await Sales.DeletePaymentAsync(_company, first);
        invoice = await InvoiceAsync(id);
        Assert.Equal((5000L, Sales.Sent, Sales.PartiallyPaid), (invoice.DueAmount, invoice.Status, invoice.PaidStatus));

        await Assert.ThrowsAsync<SalesException>(() => Sales.DeleteInvoiceAsync(_company, id)); // still has a payment
    }

    [Fact]
    public async Task One_payment_settles_several_invoices_and_keeps_the_rest_as_credit()
    {
        await SetUpCompanyAsync();
        var first = await NewInvoiceAsync(10000);   // 120.00 with tax
        var second = await NewInvoiceAsync(5000);   //  60.00 with tax
        await Sales.MarkInvoiceSentAsync(_company, first);
        await Sales.MarkInvoiceSentAsync(_company, second);

        PaymentForm Form(params (int Invoice, long Amount)[] allocations) => new()
        {
            CustomerId = _customer, Amount = 20000, Number = "PAY-1",
            Allocations = allocations.Select(a => new Allocation { InvoiceId = a.Invoice, Amount = a.Amount }).ToList(),
        };

        // Refusals leave nothing behind.
        await Assert.ThrowsAsync<SalesException>(() => Sales.SavePaymentAsync(_company, Jane, Form((first, 12000), (second, 9000))));  // > payment
        await Assert.ThrowsAsync<SalesException>(() => Sales.SavePaymentAsync(_company, Jane, Form((second, 7000))));                 // > invoice due
        await Assert.ThrowsAsync<SalesException>(() => Sales.SavePaymentAsync(_company, Jane, Form((first, 100), (first, 100))));    // duplicate

        var paymentId = await Sales.SavePaymentAsync(_company, Jane, Form((first, 12000), (second, 6000)));
        Assert.Equal((Sales.Paid, 0L), ((await InvoiceAsync(first)).PaidStatus, (await InvoiceAsync(first)).DueAmount));
        Assert.Equal(Sales.Paid, (await InvoiceAsync(second)).PaidStatus);
        await WithDb(async db => Assert.Equal(18000, await db.PaymentAllocations.Where(a => a.PaymentId == paymentId).SumAsync(a => a.Amount)));

        // Editing the allocations reopens what is no longer paid.
        var edit = await Sales.LoadPaymentAsync(_company, paymentId);
        edit.Allocations = [new Allocation { InvoiceId = first, Amount = 12000 }];
        await Sales.SavePaymentAsync(_company, Jane, edit);
        var reopened = await InvoiceAsync(second);
        Assert.Equal((Sales.Unpaid, 6000L, Sales.Sent), (reopened.PaidStatus, reopened.DueAmount, reopened.Status));
    }

    [Fact]
    public void Base_amounts_are_shared_out_to_the_cent()
    {
        var payment = new Payment { Amount = 100, BaseAmount = 333 };
        List<Allocation> rows = [new() { InvoiceId = 1, Amount = 33 }, new() { InvoiceId = 2, Amount = 33 }, new() { InvoiceId = 3, Amount = 34 }];

        var shares = Sales.BaseAmounts(payment, rows).Select(x => x.Item2).ToList();
        Assert.Equal([109L, 109L, 115L], shares);   // floors, the fully-allocated last row takes the rest
        Assert.Equal(333, shares.Sum());
    }

    [Fact]
    public async Task An_estimate_converts_into_a_draft_invoice_and_is_accepted()
    {
        await SetUpCompanyAsync();
        var estimateId = await Sales.SaveEstimateAsync(_company, Jane, new DocumentForm
        {
            CustomerId = _customer,
            Number = await Sales.NextNumberAsync(_company, "estimate", _customer),
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "Audit", Price = 80000, Quantity = 1 }] },
        });

        var invoiceId = await Sales.ConvertEstimateAsync(_company, Jane, estimateId);

        var invoice = await InvoiceAsync(invoiceId);
        Assert.Equal((80000L, Sales.Draft, "INV-000001"), (invoice.Total, invoice.Status, invoice.InvoiceNumber));
        await WithDb(async db =>
        {
            var estimate = await db.Estimates.SingleAsync(e => e.Id == estimateId);
            Assert.Equal(("EST-000001", Sales.Accepted), (estimate.EstimateNumber, estimate.Status));
            Assert.Equal(1, await db.InvoiceItems.CountAsync(i => i.InvoiceId == invoiceId));
        });
    }
}
