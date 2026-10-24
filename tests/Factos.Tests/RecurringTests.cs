using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Factos.Tests;

/// Cron cadence in the company's zone, and invoice generation from a recurring template.
public class RecurringTests : ApiTestBase
{
    private static readonly TimeZoneInfo Paris = Cadence.Zone("Europe/Paris");

    [Fact]
    public void Next_run_is_strictly_after_and_in_the_company_zone()
    {
        // Monthly on the 1st, counted from the start day's midnight in Paris: next month, not the same day.
        var startMidnight = Cadence.StartOfDay(new DateOnly(2026, 11, 1), Paris);
        Assert.Equal(new DateTime(2026, 11, 30, 23, 0, 0, DateTimeKind.Utc), Cadence.Next("0 0 1 * *", startMidnight, Paris));

        // Every week (Sunday 00:00 Paris), from a Wednesday afternoon.
        Assert.Equal(new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc),
            Cadence.Next("0 0 * * 0", new DateTime(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc), Paris));

        // No catch-up: of several missed daily runs, only the latest counts.
        Assert.Equal(new DateTime(2026, 10, 22, 22, 0, 0, DateTimeKind.Utc),
            Cadence.LatestAtOrBefore("0 0 * * *", new DateTime(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc), Paris));

        Assert.False(Cadence.IsValid("every month"));
    }

    [Fact]
    public async Task A_due_template_generates_one_draft_invoice_then_waits_for_its_next_run()
    {
        var (company, customer) = await SetUpAsync();
        var recurring = Service<Recurring>();
        var id = await recurring.SaveAsync(company, Jane, new RecurringForm
        {
            CustomerId = customer,
            StartsAt = new DateOnly(2026, 1, 1),
            Frequency = "0 0 1 * *",
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "Abonnement", Price = 4900, Quantity = 1 }] },
        });

        var next = await NextRunAsync(id);
        Assert.Equal(0, await recurring.GenerateDueAsync(next.AddMinutes(-1)));   // not due yet
        Assert.Equal(1, await recurring.GenerateDueAsync(next.AddHours(1)));
        Assert.Equal(0, await recurring.GenerateDueAsync(next.AddHours(2)));      // already advanced

        await WithDb(async db =>
        {
            var invoice = await db.Invoices.SingleAsync(i => i.RecurringInvoiceId == id);
            Assert.Equal(("INV-000001", Sales.Draft, 4900L, 4900L), (invoice.InvoiceNumber, invoice.Status, invoice.Total, invoice.DueAmount));
            Assert.Equal(Cadence.LocalDate(next, Paris), DateOnly.FromDateTime(invoice.InvoiceDate));
            Assert.Equal(Cadence.LocalDate(next, Paris).AddDays(7), invoice.DueDate);
            Assert.Equal("Abonnement", (await db.InvoiceItems.SingleAsync(i => i.InvoiceId == invoice.Id)).Name);
            Assert.Equal(1, await db.RecurrenceOccurrences.CountAsync(o => o.ScheduleId == id));
            Assert.True((await db.RecurringInvoices.SingleAsync(r => r.Id == id)).NextInvoiceAt > next);
        });
    }

    [Fact]
    public async Task A_count_limit_completes_the_schedule_and_on_hold_skips_it()
    {
        var (company, customer) = await SetUpAsync();
        var recurring = Service<Recurring>();
        var limited = await recurring.SaveAsync(company, Jane, new RecurringForm
        {
            CustomerId = customer, Frequency = "0 0 * * *", LimitBy = "COUNT", LimitCount = 1,
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "Une fois", Price = 100, Quantity = 1 }] },
        });
        var held = await recurring.SaveAsync(company, Jane, new RecurringForm
        {
            CustomerId = customer, Frequency = "0 0 * * *", Status = Recurring.OnHold,
            Draft = new DocumentDraft { Items = [new DraftItem { Name = "En pause", Price = 100, Quantity = 1 }] },
        });

        var later = (await NextRunAsync(limited)).AddHours(1);
        Assert.Equal(1, await recurring.GenerateDueAsync(later));

        await WithDb(async db =>
        {
            Assert.Equal(Recurring.Completed, (await db.RecurringInvoices.SingleAsync(r => r.Id == limited)).Status);
            Assert.False(await db.Invoices.AnyAsync(i => i.RecurringInvoiceId == held));
        });
    }

    private async Task<DateTime> NextRunAsync(long id)
    {
        DateTime next = default;
        await WithDb(async db => next = (await db.RecurringInvoices.SingleAsync(r => r.Id == id)).NextInvoiceAt!.Value);
        return DateTime.SpecifyKind(next, DateTimeKind.Utc);
    }

    private async Task<(int Company, long Customer)> SetUpAsync()
    {
        (int, long) ids = default;
        await WithDb(async db =>
        {
            var owner = await db.Users.SingleAsync(u => u.Id == Jane);
            var eur = new Currency { Name = "Euro", Code = "EUR", Symbol = "€", Precision = 2, ThousandSeparator = ".", DecimalSeparator = "," };
            db.Currencies.Add(eur);
            await db.SaveChangesAsync();
            var company = await Companies.CreateAsync(db, owner, "Abonnements SA", eur.Id);
            await db.CompanySettings.Where(s => s.CompanyId == company.Id && s.Option == "time_zone")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "Europe/Paris"));
            var customer = new Customer { Name = "Abonné", CompanyId = company.Id, CurrencyId = eur.Id };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            ids = (company.Id, customer.Id);
        });
        return ids;
    }
}
