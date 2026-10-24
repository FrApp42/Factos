using Factos.Components;
using Cronos;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

public class RecurringForm
{
    public long? Id { get; set; }
    public long? CustomerId { get; set; }
    public DateOnly StartsAt { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    /// Cron expression, as upstream stores it.
    public string Frequency { get; set; } = "0 0 1 * *";
    /// NONE, COUNT or DATE.
    public string LimitBy { get; set; } = "NONE";
    public int? LimitCount { get; set; }
    public DateOnly? LimitDate { get; set; }
    public bool SendAutomatically { get; set; }
    public bool NotifyCreator { get; set; }
    public string Status { get; set; } = Recurring.Active;
    public string? Notes { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public DocumentDraft Draft { get; set; } = new();
}

/// Support/Recurrence/Cadence.php: cron schedules evaluated in the company's time zone,
/// stored in UTC.
public static class Cadence
{
    public static bool IsValid(string? frequency)
    {
        try
        {
            CronExpression.Parse(frequency ?? "");
            return true;
        }
        catch (CronFormatException)
        {
            return false;
        }
    }

    /// The first run strictly after `afterUtc`, skipping a run in the same minute.
    public static DateTime Next(string frequency, DateTime afterUtc, TimeZoneInfo zone)
    {
        var cron = CronExpression.Parse(frequency);
        var next = cron.GetNextOccurrence(Utc(afterUtc), zone)
            ?? throw new InvalidOperationException($"The schedule '{frequency}' never runs again.");
        if (Minute(next) == Minute(afterUtc))
            next = cron.GetNextOccurrence(next, zone)!.Value;
        return next;
    }

    /// The latest run at or before `nowUtc`, searched from `dueUtc` (the stored next run).
    // ponytail: walks every occurrence since the stored due date; a minutely schedule left
    // idle for months iterates a lot. Fine at a one-minute polling interval.
    public static DateTime LatestAtOrBefore(string frequency, DateTime dueUtc, DateTime nowUtc, TimeZoneInfo zone)
        => CronExpression.Parse(frequency)
            .GetOccurrences(Utc(dueUtc), Utc(nowUtc), zone, fromInclusive: true, toInclusive: true)
            .DefaultIfEmpty(Utc(dueUtc))
            .Last();

    /// The schedule's local calendar day for a UTC instant.
    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), zone));

    /// Midnight of a calendar day in the schedule's zone, as UTC.
    public static DateTime StartOfDay(DateOnly day, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);

    /// CompanySetting::timeZone(): the company's `time_zone`, UTC when unknown.
    public static TimeZoneInfo Zone(string? id)
    {
        try { return string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    /// IANA ids, as upstream stores them (Windows reports its own ids, converted here).
    public static readonly string[] TimeZones = TimeZoneInfo.GetSystemTimeZones()
        .Select(z => z.HasIanaId ? z.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(z.Id, out var iana) ? iana : null)
        .OfType<string>().Append("UTC").Distinct().Order().ToArray();

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime Minute(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);
}

/// Recurring invoice templates and their generation, as InvoiceShelf's
/// RecurringInvoiceService and RecurrenceRunner do it (without catch-up: a schedule that
/// missed several runs produces one invoice, for the latest due run).
public class Recurring(AppDbFactory dbs, Sales sales, DocumentMailer mailer, IConfiguration config)
{
    public const string Active = "ACTIVE", OnHold = "ON_HOLD", Completed = "COMPLETED";
    public const string ScheduleType = "recurring_invoice", RecordType = "invoice";

    public async Task<long> SaveAsync(int companyId, int userId, RecurringForm form)
    {
        if (!Cadence.IsValid(form.Frequency))
            throw new SalesException(Loc.T("La fréquence n'est pas une expression cron valide."));
        if (form.LimitBy == "COUNT" && form.LimitCount is not > 0)
            throw new SalesException(Loc.T("Indiquez un nombre de factures."));
        if (form.LimitBy == "DATE" && form.LimitDate is null)
            throw new SalesException(Loc.T("Indiquez une date de fin."));

        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (form.CustomerId is not { } customerId || await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId) is not { } customer)
            throw new SalesException("Choisissez un client.");
        if (form.Draft.Items.Count == 0 || form.Draft.Items.Any(i => string.IsNullOrWhiteSpace(i.Name)))
            throw new SalesException("Ajoutez au moins une ligne, chacune avec un nom.");

        var companyCurrency = await Setting(db, companyId, "currency");
        var rate = customer.CurrencyId is { } cur && cur.ToString() != companyCurrency ? form.ExchangeRate : 1;
        if (rate <= 0)
            throw new SalesException(Loc.T("Indiquez le taux de change."));

        var now = Data.Timestamp.Now();
        var schedule = form.Id is { } id
            ? await db.RecurringInvoices.SingleAsync(r => r.Id == id && r.CompanyId == companyId)
            : new RecurringInvoice { CompanyId = companyId, CreatorId = userId, CreatedAt = now, TemplateName = "invoice1" };

        var startsAt = form.StartsAt.ToDateTime(TimeOnly.MinValue);
        var cadenceChanged = schedule.Id == 0 || schedule.Frequency != form.Frequency || schedule.StartsAt != startsAt;

        var draft = form.Draft;
        draft.Recalculate();
        var (subTotal, tax, total) = draft.Totals();

        schedule.CustomerId = customer.Id;
        schedule.StartsAt = startsAt;
        schedule.Frequency = form.Frequency.Trim();
        schedule.LimitBy = form.LimitBy;
        schedule.LimitCount = form.LimitBy == "COUNT" ? form.LimitCount : null;
        schedule.LimitDate = form.LimitBy == "DATE" ? form.LimitDate : null;
        schedule.SendAutomatically = form.SendAutomatically;
        schedule.NotifyCreator = form.NotifyCreator;
        schedule.Status = form.Status;
        schedule.Notes = form.Notes;
        schedule.CurrencyId = customer.CurrencyId;
        schedule.ExchangeRate = rate;
        schedule.TaxPerItem = draft.TaxPerItem ? "YES" : "NO";
        schedule.DiscountPerItem = draft.DiscountPerItem ? "YES" : "NO";
        schedule.TaxIncluded = draft.TaxIncluded;
        schedule.Discount = draft.Discount;
        schedule.DiscountType = draft.DiscountType;
        schedule.DiscountVal = draft.DiscountVal;
        schedule.SubTotal = subTotal;
        schedule.Tax = tax;
        schedule.Total = total;
        schedule.DueAmount = total;
        schedule.UpdatedAt = now;

        if (cadenceChanged)
        {
            // nextRunCountsFrom(): a start date still ahead counts from that day's midnight.
            var zone = Cadence.Zone(await Setting(db, companyId, "time_zone"));
            var from = Cadence.StartOfDay(form.StartsAt, zone) > now ? Cadence.StartOfDay(form.StartsAt, zone) : now;
            // Strictly after: a monthly "0 0 1 * *" starting on the 1st first runs a month later.
            schedule.NextInvoiceAt = Cadence.Next(schedule.Frequency, from, zone);
        }

        if (schedule.Id == 0)
            db.RecurringInvoices.Add(schedule);
        await db.SaveChangesAsync();

        // Template lines live in invoice_items / taxes keyed by recurring_invoice_id.
        await db.Taxes.Where(t => t.RecurringInvoiceId == schedule.Id ||
            db.InvoiceItems.Any(ii => ii.RecurringInvoiceId == schedule.Id && ii.InvoiceId == null && ii.Id == t.InvoiceItemId)).ExecuteDeleteAsync();
        await db.InvoiceItems.Where(ii => ii.RecurringInvoiceId == schedule.Id && ii.InvoiceId == null).ExecuteDeleteAsync();

        foreach (var line in draft.Items)
        {
            var item = new InvoiceItem
            {
                RecurringInvoiceId = schedule.Id, ItemId = line.ItemId, CompanyId = companyId,
                Name = line.Name, Description = line.Description, UnitName = line.UnitName,
                Quantity = line.Quantity, Price = line.Price, DiscountType = line.DiscountType,
                Discount = line.Discount, DiscountVal = line.DiscountVal, Tax = line.Tax,
                Total = Money.PhpRound(line.Price * line.Quantity) - (draft.DiscountPerItem ? line.DiscountVal : 0),
                ExchangeRate = rate, CreatedAt = now, UpdatedAt = now,
            };
            db.InvoiceItems.Add(item);
            await db.SaveChangesAsync();
            foreach (var t in line.Taxes.Where(t => t.TaxTypeId != 0))
                db.Taxes.Add(Sales.TaxRow(t, companyId, schedule.CurrencyId, rate, now, invoiceItemId: item.Id));
        }
        foreach (var t in draft.Taxes)
        {
            var row = Sales.TaxRow(t, companyId, schedule.CurrencyId, rate, now);
            row.RecurringInvoiceId = schedule.Id;
            db.Taxes.Add(row);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return schedule.Id;
    }

    public async Task<RecurringForm> LoadAsync(int companyId, long id)
    {
        await using var db = dbs.Create();
        var r = await db.RecurringInvoices.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
        var items = await db.InvoiceItems.Where(x => x.RecurringInvoiceId == id && x.InvoiceId == null).OrderBy(x => x.Id).ToListAsync();
        var itemIds = items.Select(x => (int?)x.Id).ToList();
        var taxes = await db.Taxes.Where(t => t.RecurringInvoiceId == id || itemIds.Contains(t.InvoiceItemId)).ToListAsync();
        return new RecurringForm
        {
            Id = r.Id, CustomerId = r.CustomerId, StartsAt = DateOnly.FromDateTime(r.StartsAt), Frequency = r.Frequency,
            LimitBy = r.LimitBy, LimitCount = r.LimitCount, LimitDate = r.LimitDate, SendAutomatically = r.SendAutomatically,
            NotifyCreator = r.NotifyCreator, Status = r.Status, Notes = r.Notes, ExchangeRate = r.ExchangeRate ?? 1,
            Draft = new DocumentDraft
            {
                TaxPerItem = r.TaxPerItem == "YES", DiscountPerItem = r.DiscountPerItem == "YES", TaxIncluded = r.TaxIncluded,
                Discount = r.Discount ?? 0, DiscountType = r.DiscountType ?? "fixed", DiscountVal = r.DiscountVal ?? 0,
                Taxes = taxes.Where(t => t.RecurringInvoiceId == id && t.InvoiceItemId == null).Select(Sales.ToDraft).ToList(),
                Items = items.Select(x => new DraftItem
                {
                    ItemId = x.ItemId, Name = x.Name, Description = x.Description, UnitName = x.UnitName,
                    Quantity = x.Quantity, Price = x.Price, Discount = x.Discount ?? 0, DiscountType = x.DiscountType,
                    DiscountVal = x.DiscountVal, Taxes = taxes.Where(t => t.InvoiceItemId == x.Id).Select(Sales.ToDraft).ToList(),
                }).ToList(),
            },
        };
    }

    public async Task SetStatusAsync(int companyId, long id, string status)
    {
        await using var db = dbs.Create();
        var r = await db.RecurringInvoices.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
        r.Status = status;
        r.UpdatedAt = Data.Timestamp.Now();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int companyId, long id)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await db.RecurringInvoices.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
        await db.Taxes.Where(t => t.RecurringInvoiceId == id ||
            db.InvoiceItems.Any(ii => ii.RecurringInvoiceId == id && ii.InvoiceId == null && ii.Id == t.InvoiceItemId)).ExecuteDeleteAsync();
        await db.InvoiceItems.Where(ii => ii.RecurringInvoiceId == id && ii.InvoiceId == null).ExecuteDeleteAsync();
        // Generated invoices stay; they only lose the link to their schedule.
        await db.Invoices.Where(i => i.RecurringInvoiceId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.RecurringInvoiceId, (long?)null));
        await db.RecurrenceOccurrences.Where(o => o.ScheduleType == ScheduleType && o.ScheduleId == id).ExecuteDeleteAsync();
        db.RecurringInvoices.Remove(r);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<int> GeneratedCountAsync(long id)
    {
        await using var db = dbs.Create();
        return await GeneratedCountAsync(db, id);
    }

    // ---- generation (RecurrenceRunner) -----------------------------------------------

    /// Runs every due ACTIVE schedule once. Returns the number of invoices created.
    public async Task<int> GenerateDueAsync(DateTime nowUtc, Action<long, Exception>? onFailure = null, Func<long, bool>? skip = null)
    {
        List<long> due;
        await using (var db = dbs.Create())
            due = await db.RecurringInvoices
                .Where(r => r.Status == Active && r.NextInvoiceAt != null && r.NextInvoiceAt <= nowUtc)
                .OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();

        var generated = 0;
        foreach (var id in due)
        {
            if (skip?.Invoke(id) == true)
                continue;
            try
            {
                generated += await StepAsync(id, nowUtc);
            }
            catch (Exception error)
            {
                await RecordFailureAsync(id, error is SalesException ? error.Message : "recurring_invoice_failed");
                onFailure?.Invoke(id, error);
            }
        }
        return generated;
    }

    private async Task<int> StepAsync(long id, DateTime nowUtc)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var row = await db.RecurringInvoices.SingleOrDefaultAsync(r => r.Id == id);
        if (row is null || row.Status != Active || row.NextInvoiceAt is not { } nextAt || nextAt > nowUtc)
            return 0;

        var zone = Cadence.Zone(await Setting(db, row.CompanyId!.Value, "time_zone"));
        var at = Cadence.LatestAtOrBefore(row.Frequency, nextAt, nowUtc, zone);
        var date = Cadence.LocalDate(at, zone);

        if (await LimitReachedAsync(db, row, date))
        {
            row.Status = Completed;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return 0;
        }

        var invoiceId = await CreateInvoiceAsync(db, row, date, at);
        var made = invoiceId is null ? 0 : 1;
        var next = Cadence.Next(row.Frequency, at, zone);
        row.NextInvoiceAt = next;
        row.LastError = null;
        if (await LimitReachedAsync(db, row, Cadence.LocalDate(next, zone)))
            row.Status = Completed;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // Sent once the invoice is committed, so a rolled-back run mails nothing.
        if (invoiceId is { } created && row.SendAutomatically)
            await SendToCustomerAsync(row.Id, created);
        return made;
    }

    private static async Task<bool> LimitReachedAsync(ApplicationDbContext db, RecurringInvoice row, DateOnly date) => row.LimitBy switch
    {
        "COUNT" => await GeneratedCountAsync(db, row.Id) >= (row.LimitCount ?? 0),
        "DATE" => row.LimitDate is { } limit && date > limit,
        _ => false,
    };

    /// generatedCount(): logged occurrences plus older invoices generated before logging.
    private static async Task<int> GeneratedCountAsync(ApplicationDbContext db, long id)
    {
        var logged = db.RecurrenceOccurrences.Where(o => o.ScheduleType == ScheduleType && o.ScheduleId == id && o.RecordType == RecordType).Select(o => o.RecordId);
        return await db.RecurrenceOccurrences.CountAsync(o => o.ScheduleType == ScheduleType && o.ScheduleId == id)
            + await db.Invoices.CountAsync(i => i.RecurringInvoiceId == id && !logged.Contains(i.Id));
    }

    /// createInvoiceFromRecurring(): a draft invoice copied from the template.
    private static async Task<int?> CreateInvoiceAsync(ApplicationDbContext db, RecurringInvoice r, DateOnly date, DateTime at)
    {
        if (await db.RecurrenceOccurrences.AnyAsync(o => o.ScheduleType == ScheduleType && o.ScheduleId == r.Id && o.ScheduledAt == at))
            return null;

        var companyId = r.CompanyId!.Value;
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == r.CustomerId && c.CompanyId == companyId)
            ?? throw new SalesException("recurring_invoice_customer_missing");
        if (r.CurrencyId is not null && customer.CurrencyId != r.CurrencyId)
            throw new SalesException("recurring_invoice_currency_changed");

        var days = int.TryParse(await Setting(db, companyId, "invoice_due_date_days"), out var d) && d != 0 ? d : 7;
        var (sequence, customerSequence) = await Sales.NextSequencesAsync(db, companyId, "invoice", customer.Id);
        var format = await Setting(db, companyId, "invoice_number_format") ?? "";
        var rate = r.ExchangeRate;
        var now = Data.Timestamp.Now();

        var invoice = new Invoice
        {
            CompanyId = companyId, CreatorId = r.CreatorId, CustomerId = customer.Id, CurrencyId = customer.CurrencyId,
            RecurringInvoiceId = r.Id, Type = "INVOICE", Status = Sales.Draft, PaidStatus = Sales.Unpaid,
            InvoiceDate = date.ToDateTime(TimeOnly.MinValue), DueDate = date.AddDays(days),
            InvoiceNumber = SerialNumbers.Generate(format, sequence, customerSequence, customer.Prefix, DateTime.Now),
            SequenceNumber = sequence, CustomerSequenceNumber = customerSequence,
            SubTotal = r.SubTotal, Tax = r.Tax, Total = r.Total, DueAmount = r.Total,
            TaxPerItem = r.TaxPerItem, DiscountPerItem = r.DiscountPerItem, TaxIncluded = r.TaxIncluded,
            Discount = r.Discount, DiscountType = r.DiscountType, DiscountVal = r.DiscountVal,
            Notes = r.Notes, TemplateName = r.TemplateName, ExchangeRate = rate,
            SalesTaxType = r.SalesTaxType, SalesTaxAddressType = r.SalesTaxAddressType,
            BaseDueAmount = Money.ToBaseMinor(r.Total, rate), BaseDiscountVal = Money.ToBaseMinor(r.DiscountVal, rate),
            BaseSubTotal = Money.ToBaseMinor(r.SubTotal, rate), BaseTax = Money.ToBaseMinor(r.Tax, rate),
            BaseTotal = Money.ToBaseMinor(r.Total, rate),
            UniqueHash = Companies.PublicToken(), CreatedAt = now, UpdatedAt = now,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var items = await db.InvoiceItems.AsNoTracking().Where(x => x.RecurringInvoiceId == r.Id && x.InvoiceId == null).OrderBy(x => x.Id).ToListAsync();
        foreach (var t in items)
        {
            var copy = new InvoiceItem
            {
                InvoiceId = invoice.Id, ItemId = t.ItemId, CompanyId = companyId, Name = t.Name, Description = t.Description,
                UnitName = t.UnitName, Quantity = t.Quantity, Price = t.Price, DiscountType = t.DiscountType,
                Discount = t.Discount, DiscountVal = t.DiscountVal, Tax = t.Tax,
                Total = Money.PhpRound(t.Price * t.Quantity) - (r.DiscountPerItem == "YES" ? t.DiscountVal : 0),
                ExchangeRate = rate, CreatedAt = now, UpdatedAt = now,
            };
            copy.BasePrice = Money.ToBaseMinor(copy.Price, rate);
            copy.BaseDiscountVal = Money.ToBaseMinor(copy.DiscountVal, rate);
            copy.BaseTax = Money.ToBaseMinor(copy.Tax, rate);
            copy.BaseTotal = Money.ToBaseMinor(copy.Total, rate);
            db.InvoiceItems.Add(copy);
            await db.SaveChangesAsync();
            foreach (var tax in await db.Taxes.AsNoTracking().Where(x => x.InvoiceItemId == t.Id).ToListAsync())
                db.Taxes.Add(CopyTax(tax, rate, now, invoiceItemId: copy.Id));
        }
        foreach (var tax in await db.Taxes.AsNoTracking().Where(x => x.RecurringInvoiceId == r.Id && x.InvoiceItemId == null).ToListAsync())
            db.Taxes.Add(CopyTax(tax, rate, now, invoiceId: invoice.Id));

        db.RecurrenceOccurrences.Add(new RecurrenceOccurrence
        {
            CompanyId = companyId, ScheduleType = ScheduleType, ScheduleId = r.Id, ScheduledFor = date,
            ScheduledAt = at, RecordType = RecordType, RecordId = invoice.Id, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        // ponytail: `notify_creator` (mail to the schedule's creator) is not sent yet.
        return invoice.Id;
    }

    /// sendToCustomer(): the company's default subject and body. A failure is recorded on the
    /// schedule and does not undo the invoice. Without `App:Url` (no request to read the site
    /// address from) the PDF is attached instead of linked.
    private async Task SendToCustomerAsync(long scheduleId, int invoiceId)
    {
        try
        {
            var data = await DocumentData.LoadAsync(dbs, sales, DocumentData.Invoice, invoiceId)
                ?? throw new SalesException("recurring_invoice_send_failed");
            if (!DocumentMailer.Enabled(data))
                return;
            await mailer.SendAsync(data, DocumentMailer.Prepare(data), config["App:Url"]);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await RecordFailureAsync(scheduleId, "recurring_invoice_send_failed");
        }
    }

    private static Tax CopyTax(Tax t, decimal? rate, DateTime now, int? invoiceId = null, int? invoiceItemId = null) => new()
    {
        TaxTypeId = t.TaxTypeId, Name = t.Name, Amount = t.Amount, Percent = t.Percent, CompoundTax = t.CompoundTax,
        CalculationType = t.CalculationType, FixedAmount = t.FixedAmount, CompanyId = t.CompanyId, CurrencyId = t.CurrencyId,
        ExchangeRate = rate, BaseAmount = Money.ToBaseMinor(t.Amount, rate),
        InvoiceId = invoiceId, InvoiceItemId = invoiceItemId, CreatedAt = now, UpdatedAt = now,
    };

    private async Task RecordFailureAsync(long id, string reason)
    {
        await using var db = dbs.Create();
        await db.RecurringInvoices.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.LastError, reason));
    }

    private static Task<string?> Setting(ApplicationDbContext db, int companyId, string option)
        => db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == option).Select(s => s.Value).FirstOrDefaultAsync();
}

/// Generates due recurring invoices every minute, like upstream's scheduler. A schedule
/// that fails is retried after an hour (RecurrenceRunner::RETRY_AFTER_SECONDS).
public class RecurringInvoiceWorker(Recurring recurring, ILogger<RecurringInvoiceWorker> logger) : BackgroundService
{
    private readonly Dictionary<long, DateTime> _failedAt = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                var now = DateTime.UtcNow;
                var made = await recurring.GenerateDueAsync(now,
                    onFailure: (id, error) =>
                    {
                        _failedAt[id] = now;
                        logger.LogWarning(error, "Recurring invoice {Id} failed", id);
                    },
                    skip: id => _failedAt.TryGetValue(id, out var at) && now - at < TimeSpan.FromHours(1));
                if (made > 0)
                    logger.LogInformation("Generated {Count} recurring invoice(s)", made);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Recurring invoice run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
