using Factos.Components;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

public class DocumentForm
{
    public int? Id { get; set; }
    public long? CustomerId { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    /// Due date for an invoice, expiry date for an estimate.
    public DateOnly? DueDate { get; set; }
    public string Number { get; set; } = "";
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    /// A DocumentTemplate name; null keeps the document's current one.
    public string? TemplateName { get; set; }
    public DocumentDraft Draft { get; set; } = new();
}

public class PaymentForm
{
    public long? Id { get; set; }
    public long? CustomerId { get; set; }
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Number { get; set; } = "";
    /// Cents.
    public long Amount { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? Notes { get; set; }
    /// The part of the payment settling each invoice, in cents. Whatever is not allocated
    /// stays on the customer's account as credit.
    public List<Allocation> Allocations { get; set; } = [];
}

public class Allocation
{
    public int InvoiceId { get; set; }
    public long Amount { get; set; }
}

public class SalesException(string message) : Exception(message);

/// Invoices, estimates and payments, written the way InvoiceShelf's InvoiceService,
/// EstimateService, DocumentItemService, PaymentService and InvoiceBalanceService do.
public class Sales(AppDbFactory dbs)
{
    public const string Draft = "DRAFT", Sent = "SENT", Viewed = "VIEWED", Completed = "COMPLETED";
    public const string Unpaid = "UNPAID", PartiallyPaid = "PARTIALLY_PAID", Paid = "PAID";
    public const string Accepted = "ACCEPTED", Rejected = "REJECTED";

    // ---- numbering --------------------------------------------------------------

    /// The next number for "invoice", "estimate" or "payment" (`<kind>_number_format`).
    public async Task<string> NextNumberAsync(int companyId, string kind, long? customerId)
    {
        await using var db = dbs.Create();
        var (sequence, customerSequence) = await NextSequencesAsync(db, companyId, kind, customerId);
        var format = await Setting(db, companyId, kind + "_number_format") ?? "";
        var prefix = customerId is { } c ? await db.Customers.Where(x => x.Id == c).Select(x => x.Prefix).FirstOrDefaultAsync() : null;
        return SerialNumbers.Generate(format, sequence, customerSequence, prefix, DateTime.Now);
    }

    internal static async Task<(int, int)> NextSequencesAsync(ApplicationDbContext db, int companyId, string kind, long? customerId)
    {
        // Upstream counts customer sequences against customer 1 when there is no customer.
        var customer = customerId ?? 1;
        return kind switch
        {
            "invoice" => (
                (await db.Invoices.Where(i => i.CompanyId == companyId && i.Type == "INVOICE").MaxAsync(i => i.SequenceNumber) ?? 0) + 1,
                (await db.Invoices.Where(i => i.CompanyId == companyId && i.Type == "INVOICE" && i.CustomerId == customer).MaxAsync(i => i.CustomerSequenceNumber) ?? 0) + 1),
            "estimate" => (
                (await db.Estimates.Where(i => i.CompanyId == companyId).MaxAsync(i => i.SequenceNumber) ?? 0) + 1,
                (await db.Estimates.Where(i => i.CompanyId == companyId && i.CustomerId == customer).MaxAsync(i => i.CustomerSequenceNumber) ?? 0) + 1),
            "payment" => (
                (await db.Payments.Where(i => i.CompanyId == companyId).MaxAsync(i => i.SequenceNumber) ?? 0) + 1,
                (await db.Payments.Where(i => i.CompanyId == companyId && i.CustomerId == customer).MaxAsync(i => i.CustomerSequenceNumber) ?? 0) + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    // ---- invoices ---------------------------------------------------------------

    public async Task<int> SaveInvoiceAsync(int companyId, int userId, DocumentForm form)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (customer, rate) = await ValidateAsync(db, companyId, form);
        if (await db.Invoices.AnyAsync(i => i.CompanyId == companyId && i.InvoiceNumber == form.Number && i.Id != form.Id))
            throw new SalesException(Loc.T("Ce numéro de facture est déjà utilisé."));

        var now = Data.Timestamp.Now();
        if (form.Id is { } editedId && await db.Invoices.AnyAsync(i => i.Id == editedId && i.Type == "CREDIT_NOTE"))
            throw new SalesException(Loc.T("Une note de crédit ne se modifie pas : supprimez-la et créez-en une nouvelle."));
        var invoice = form.Id is { } id
            ? await db.Invoices.SingleAsync(i => i.Id == id && i.CompanyId == companyId)
            : new Invoice
            {
                CompanyId = companyId, CreatorId = userId, CreatedAt = now, Status = Draft, PaidStatus = Unpaid,
                Type = "INVOICE", UniqueHash = Companies.PublicToken(), TemplateName = "invoice1",
            };

        var draft = form.Draft;
        draft.Recalculate();
        var (subTotal, tax, total) = draft.Totals();

        invoice.CustomerId = customer.Id;
        invoice.InvoiceDate = form.Date.ToDateTime(TimeOnly.MinValue);
        invoice.DueDate = form.DueDate;
        invoice.InvoiceNumber = form.Number;
        invoice.ReferenceNumber = form.ReferenceNumber;
        invoice.Notes = form.Notes;
        invoice.TemplateName = string.IsNullOrEmpty(form.TemplateName) ? invoice.TemplateName : form.TemplateName;
        invoice.TaxPerItem = draft.TaxPerItem ? "YES" : "NO";
        invoice.DiscountPerItem = draft.DiscountPerItem ? "YES" : "NO";
        invoice.TaxIncluded = draft.TaxIncluded;
        invoice.Discount = draft.Discount;
        invoice.DiscountType = draft.DiscountType;
        invoice.DiscountVal = draft.DiscountVal;
        invoice.SubTotal = subTotal;
        invoice.Tax = tax;
        invoice.Total = total;
        invoice.CurrencyId = customer.CurrencyId;
        invoice.ExchangeRate = rate;
        invoice.BaseDiscountVal = Money.ToBaseMinor(draft.DiscountVal, rate);
        invoice.BaseSubTotal = Money.ToBaseMinor(subTotal, rate);
        invoice.BaseTax = Money.ToBaseMinor(tax, rate);
        invoice.BaseTotal = Money.ToBaseMinor(total, rate);
        invoice.UpdatedAt = now;

        if (invoice.Id == 0)
        {
            var (sequence, customerSequence) = await NextSequencesAsync(db, companyId, "invoice", customer.Id);
            invoice.SequenceNumber = sequence;
            invoice.CustomerSequenceNumber = customerSequence;
            invoice.DueAmount = total;
            invoice.BaseDueAmount = Money.ToBaseMinor(total, rate);
            db.Invoices.Add(invoice);
        }
        await db.SaveChangesAsync();

        // Upstream replaces every line and tax on update.
        await db.Taxes.Where(t => t.InvoiceId == invoice.Id || db.InvoiceItems.Any(ii => ii.InvoiceId == invoice.Id && ii.Id == t.InvoiceItemId)).ExecuteDeleteAsync();
        await db.InvoiceItems.Where(ii => ii.InvoiceId == invoice.Id).ExecuteDeleteAsync();

        foreach (var line in draft.Items)
        {
            var item = new InvoiceItem
            {
                InvoiceId = invoice.Id, ItemId = line.ItemId, CompanyId = companyId,
                Name = line.Name, Description = line.Description, UnitName = line.UnitName,
                Quantity = line.Quantity, Price = line.Price,
                DiscountType = line.DiscountType, Discount = line.Discount, DiscountVal = line.DiscountVal,
                Tax = line.Tax,
                Total = Money.PhpRound(line.Price * line.Quantity) - (draft.DiscountPerItem ? line.DiscountVal : 0),
                ExchangeRate = rate, CreatedAt = now, UpdatedAt = now,
            };
            item.BasePrice = Money.ToBaseMinor(item.Price, rate);
            item.BaseDiscountVal = Money.ToBaseMinor(item.DiscountVal, rate);
            item.BaseTax = Money.ToBaseMinor(item.Tax, rate);
            item.BaseTotal = Money.ToBaseMinor(item.Total, rate);
            db.InvoiceItems.Add(item);
            await db.SaveChangesAsync();
            foreach (var t in line.Taxes.Where(t => t.TaxTypeId != 0))
                db.Taxes.Add(TaxRow(t, companyId, invoice.CurrencyId, rate, now, invoiceItemId: item.Id));
        }
        foreach (var t in draft.Taxes)
            db.Taxes.Add(TaxRow(t, companyId, invoice.CurrencyId, rate, now, invoiceId: invoice.Id));
        await db.SaveChangesAsync();

        await RecalculateBalanceAsync(db, invoice);
        await tx.CommitAsync();
        return invoice.Id;
    }

    public async Task DeleteInvoiceAsync(int companyId, int id)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == id && i.CompanyId == companyId);
        if (await db.PaymentAllocations.AnyAsync(a => a.InvoiceId == id))
            throw new SalesException(Loc.T("Cette facture a des paiements : supprimez-les d'abord."));
        if (await db.Invoices.AnyAsync(i => i.RelatedInvoiceId == id && i.Type == "CREDIT_NOTE"))
            throw new SalesException(Loc.T("Cette facture a des notes de crédit : supprimez-les d'abord."));
        await db.Taxes.Where(t => t.InvoiceId == id || db.InvoiceItems.Any(ii => ii.InvoiceId == id && ii.Id == t.InvoiceItemId)).ExecuteDeleteAsync();
        await db.InvoiceItems.Where(ii => ii.InvoiceId == id).ExecuteDeleteAsync();
        await db.Transactions.Where(t => t.InvoiceId == id).ExecuteDeleteAsync();
        await CustomFields.DeleteAsync(db, "Invoice", id);
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync();
        // Removing a credit note gives its amount back to the invoice it credited.
        if (invoice.Type == "CREDIT_NOTE" && invoice.RelatedInvoiceId is { } relatedId
            && await db.Invoices.SingleOrDefaultAsync(i => i.Id == relatedId) is { } related)
            await RecalculateBalanceAsync(db, related);
        await tx.CommitAsync();
    }

    /// "Mark as sent": the document's status moves to SENT unless it is already further.
    public async Task MarkInvoiceSentAsync(int companyId, int id)
    {
        await using var db = dbs.Create();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == id && i.CompanyId == companyId);
        invoice.Sent = true;
        if (invoice.Status == Draft)
            invoice.Status = Sent;
        invoice.UpdatedAt = Data.Timestamp.Now();
        await db.SaveChangesAsync();
    }

    /// InvoiceBalanceService::recalculate().
    internal static async Task RecalculateBalanceAsync(ApplicationDbContext db, Invoice invoice)
    {
        var allocated = await db.PaymentAllocations.Where(a => a.InvoiceId == invoice.Id).SumAsync(a => (long?)a.Amount) ?? 0;
        var credited = -(await db.Invoices.Where(i => i.RelatedInvoiceId == invoice.Id && i.Type == "CREDIT_NOTE").SumAsync(i => (long?)i.Total) ?? 0);
        var due = Math.Max(0, invoice.Total - allocated - credited);

        invoice.DueAmount = due;
        invoice.BaseDueAmount = Money.ToBaseMinor(due, invoice.ExchangeRate);
        if (due == 0)
        {
            invoice.Status = Completed;
            invoice.PaidStatus = Paid;
            invoice.Overdue = false;
        }
        else
        {
            invoice.Status = invoice.Viewed ? Viewed : invoice.Sent ? Sent : Draft;
            invoice.PaidStatus = allocated > 0 ? PartiallyPaid : Unpaid;
        }
        await db.SaveChangesAsync();
    }

    // ---- estimates --------------------------------------------------------------

    public async Task<int> SaveEstimateAsync(int companyId, int userId, DocumentForm form)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (customer, rate) = await ValidateAsync(db, companyId, form);
        if (await db.Estimates.AnyAsync(e => e.CompanyId == companyId && e.EstimateNumber == form.Number && e.Id != form.Id))
            throw new SalesException(Loc.T("Ce numéro de devis est déjà utilisé."));

        var now = Data.Timestamp.Now();
        var estimate = form.Id is { } id
            ? await db.Estimates.SingleAsync(e => e.Id == id && e.CompanyId == companyId)
            : new Estimate
            {
                CompanyId = companyId, CreatorId = userId, CreatedAt = now, Status = Draft,
                UniqueHash = Companies.PublicToken(), TemplateName = "estimate1",
            };

        var draft = form.Draft;
        draft.Recalculate();
        var (subTotal, tax, total) = draft.Totals();

        estimate.CustomerId = customer.Id;
        estimate.EstimateDate = form.Date;
        estimate.ExpiryDate = form.DueDate;
        estimate.EstimateNumber = form.Number;
        estimate.ReferenceNumber = form.ReferenceNumber;
        estimate.Notes = form.Notes;
        estimate.TemplateName = string.IsNullOrEmpty(form.TemplateName) ? estimate.TemplateName : form.TemplateName;
        estimate.TaxPerItem = draft.TaxPerItem ? "YES" : "NO";
        estimate.DiscountPerItem = draft.DiscountPerItem ? "YES" : "NO";
        estimate.TaxIncluded = draft.TaxIncluded;
        estimate.Discount = draft.Discount;
        estimate.DiscountType = draft.DiscountType;
        estimate.DiscountVal = draft.DiscountVal;
        estimate.SubTotal = subTotal;
        estimate.Tax = tax;
        estimate.Total = total;
        estimate.CurrencyId = customer.CurrencyId;
        estimate.ExchangeRate = rate;
        estimate.BaseDiscountVal = Money.ToBaseMinor(draft.DiscountVal, rate);
        estimate.BaseSubTotal = Money.ToBaseMinor(subTotal, rate);
        estimate.BaseTax = Money.ToBaseMinor(tax, rate);
        estimate.BaseTotal = Money.ToBaseMinor(total, rate);
        estimate.UpdatedAt = now;

        if (estimate.Id == 0)
        {
            var (sequence, customerSequence) = await NextSequencesAsync(db, companyId, "estimate", customer.Id);
            estimate.SequenceNumber = sequence;
            estimate.CustomerSequenceNumber = customerSequence;
            db.Estimates.Add(estimate);
        }
        await db.SaveChangesAsync();

        await db.Taxes.Where(t => t.EstimateId == estimate.Id || db.EstimateItems.Any(ei => ei.EstimateId == estimate.Id && ei.Id == t.EstimateItemId)).ExecuteDeleteAsync();
        await db.EstimateItems.Where(ei => ei.EstimateId == estimate.Id).ExecuteDeleteAsync();

        foreach (var line in draft.Items)
        {
            var item = new EstimateItem
            {
                EstimateId = estimate.Id, ItemId = line.ItemId, CompanyId = companyId,
                Name = line.Name, Description = line.Description, UnitName = line.UnitName,
                Quantity = line.Quantity, Price = line.Price,
                DiscountType = line.DiscountType, Discount = line.Discount, DiscountVal = line.DiscountVal,
                Tax = line.Tax,
                Total = Money.PhpRound(line.Price * line.Quantity) - (draft.DiscountPerItem ? line.DiscountVal : 0),
                ExchangeRate = rate, CreatedAt = now, UpdatedAt = now,
            };
            item.BasePrice = Money.ToBaseMinor(item.Price, rate);
            item.BaseDiscountVal = Money.ToBaseMinor(item.DiscountVal, rate);
            item.BaseTax = Money.ToBaseMinor(item.Tax, rate);
            item.BaseTotal = Money.ToBaseMinor(item.Total, rate);
            db.EstimateItems.Add(item);
            await db.SaveChangesAsync();
            foreach (var t in line.Taxes.Where(t => t.TaxTypeId != 0))
                db.Taxes.Add(TaxRow(t, companyId, estimate.CurrencyId, rate, now, estimateItemId: item.Id));
        }
        foreach (var t in draft.Taxes)
            db.Taxes.Add(TaxRow(t, companyId, estimate.CurrencyId, rate, now, estimateId: estimate.Id));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return estimate.Id;
    }

    public async Task DeleteEstimateAsync(int companyId, int id)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var estimate = await db.Estimates.SingleAsync(e => e.Id == id && e.CompanyId == companyId);
        await db.Taxes.Where(t => t.EstimateId == id || db.EstimateItems.Any(ei => ei.EstimateId == id && ei.Id == t.EstimateItemId)).ExecuteDeleteAsync();
        await db.EstimateItems.Where(ei => ei.EstimateId == id).ExecuteDeleteAsync();
        await CustomFields.DeleteAsync(db, "Estimate", id);
        db.Estimates.Remove(estimate);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SetEstimateStatusAsync(int companyId, int id, string status)
    {
        await using var db = dbs.Create();
        var estimate = await db.Estimates.SingleAsync(e => e.Id == id && e.CompanyId == companyId);
        estimate.Status = status;
        estimate.UpdatedAt = Data.Timestamp.Now();
        await db.SaveChangesAsync();
    }

    /// Loads a saved invoice or estimate back into a form.
    public async Task<DocumentForm> LoadAsync(int companyId, string kind, int id)
    {
        await using var db = dbs.Create();

        if (kind == "invoice")
        {
            var i = await db.Invoices.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
            var items = await db.InvoiceItems.Where(x => x.InvoiceId == id).OrderBy(x => x.Id).ToListAsync();
            var itemIds = items.Select(x => (int?)x.Id).ToList();
            var taxes = await db.Taxes.Where(t => t.InvoiceId == id || itemIds.Contains(t.InvoiceItemId)).ToListAsync();
            return new DocumentForm
            {
                Id = i.Id, CustomerId = i.CustomerId, Date = DateOnly.FromDateTime(i.InvoiceDate), DueDate = i.DueDate,
                Number = i.InvoiceNumber, ReferenceNumber = i.ReferenceNumber, Notes = i.Notes, ExchangeRate = i.ExchangeRate ?? 1, TemplateName = i.TemplateName,
                Draft = new DocumentDraft
                {
                    TaxPerItem = i.TaxPerItem == "YES", DiscountPerItem = i.DiscountPerItem == "YES", TaxIncluded = i.TaxIncluded,
                    Discount = i.Discount ?? 0, DiscountType = i.DiscountType ?? "fixed", DiscountVal = i.DiscountVal ?? 0,
                    Taxes = taxes.Where(t => t.InvoiceId == id).Select(ToDraft).ToList(),
                    Items = items.Select(x => new DraftItem
                    {
                        ItemId = x.ItemId, Name = x.Name, Description = x.Description, UnitName = x.UnitName,
                        Quantity = x.Quantity, Price = x.Price, Discount = x.Discount ?? 0, DiscountType = x.DiscountType,
                        DiscountVal = x.DiscountVal, Taxes = taxes.Where(t => t.InvoiceItemId == x.Id).Select(ToDraft).ToList(),
                    }).ToList(),
                },
            };
        }

        var e = await db.Estimates.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
        var eItems = await db.EstimateItems.Where(x => x.EstimateId == id).OrderBy(x => x.Id).ToListAsync();
        var eItemIds = eItems.Select(x => (int?)x.Id).ToList();
        var eTaxes = await db.Taxes.Where(t => t.EstimateId == id || eItemIds.Contains(t.EstimateItemId)).ToListAsync();
        return new DocumentForm
        {
            Id = e.Id, CustomerId = e.CustomerId, Date = e.EstimateDate, DueDate = e.ExpiryDate,
            Number = e.EstimateNumber, ReferenceNumber = e.ReferenceNumber, Notes = e.Notes, ExchangeRate = e.ExchangeRate ?? 1, TemplateName = e.TemplateName,
            Draft = new DocumentDraft
            {
                TaxPerItem = e.TaxPerItem == "YES", DiscountPerItem = e.DiscountPerItem == "YES", TaxIncluded = e.TaxIncluded,
                Discount = e.Discount ?? 0, DiscountType = e.DiscountType ?? "fixed", DiscountVal = e.DiscountVal ?? 0,
                Taxes = eTaxes.Where(t => t.EstimateId == id).Select(ToDraft).ToList(),
                Items = eItems.Select(x => new DraftItem
                {
                    ItemId = x.ItemId, Name = x.Name, Description = x.Description, UnitName = x.UnitName,
                    Quantity = x.Quantity, Price = x.Price, Discount = x.Discount ?? 0, DiscountType = x.DiscountType,
                    DiscountVal = x.DiscountVal ?? 0, Taxes = eTaxes.Where(t => t.EstimateItemId == x.Id).Select(ToDraft).ToList(),
                }).ToList(),
            },
        };
    }

    /// Estimate -> new draft invoice with the same lines, numbered and dated today.
    public async Task<int> ConvertEstimateAsync(int companyId, int userId, int estimateId)
    {
        var form = await LoadAsync(companyId, "estimate", estimateId);
        await using (var db = dbs.Create())
        {
            var days = int.TryParse(await Setting(db, companyId, "invoice_due_date_days"), out var d) ? d : 7;
            var auto = await Setting(db, companyId, "invoice_set_due_date_automatically") == "YES";
            form.Id = null;
            form.TemplateName = null; // upstream converts with template_name "invoice1"
            form.Date = DateOnly.FromDateTime(DateTime.Today);
            form.DueDate = auto ? form.Date.AddDays(days) : null;
        }
        form.Number = await NextNumberAsync(companyId, "invoice", form.CustomerId);
        var invoiceId = await SaveInvoiceAsync(companyId, userId, form);
        await SetEstimateStatusAsync(companyId, estimateId, Accepted);
        return invoiceId;
    }

    // ---- payments ---------------------------------------------------------------

    /// Creates or updates a payment and replaces its allocations (PaymentService +
    /// PaymentAllocationService::replace()).
    public async Task<long> SavePaymentAsync(int companyId, int userId, PaymentForm form)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (form.CustomerId is not { } customerId || await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId) is not { } customer)
            throw new SalesException("Choisissez un client.");
        if (form.Amount < 1)
            throw new SalesException(Loc.T("Le montant doit être positif."));
        if (string.IsNullOrWhiteSpace(form.Number))
            throw new SalesException(Loc.T("Le numéro de paiement est obligatoire."));
        if (await db.Payments.AnyAsync(p => p.CompanyId == companyId && p.PaymentNumber == form.Number && p.Id != form.Id))
            throw new SalesException(Loc.T("Ce numéro de paiement est déjà utilisé."));

        var now = Data.Timestamp.Now();
        Payment payment;
        if (form.Id is { } id)
        {
            payment = await db.Payments.SingleAsync(p => p.Id == id && p.CompanyId == companyId);
            if (payment.CustomerId != customer.Id && await db.PaymentAllocations.AnyAsync(a => a.PaymentId == id) && form.Allocations.Count > 0)
                throw new SalesException(Loc.T("Ce paiement est affecté à des factures d'un autre client."));
        }
        else
        {
            var (sequence, customerSequence) = await NextSequencesAsync(db, companyId, "payment", customer.Id);
            payment = new Payment
            {
                CompanyId = companyId, CreatorId = userId, SequenceNumber = sequence, CustomerSequenceNumber = customerSequence,
                UniqueHash = Companies.PublicToken(), CreatedAt = now,
            };
            db.Payments.Add(payment);
        }

        // In the customer's currency; foreign currencies take the rate of the invoices settled.
        var firstInvoice = form.Allocations.Count == 0 ? null
            : await db.Invoices.Where(i => i.Id == form.Allocations[0].InvoiceId && i.CompanyId == companyId).FirstOrDefaultAsync();
        var rate = firstInvoice?.ExchangeRate ?? payment.ExchangeRate ?? 1;

        payment.CustomerId = customer.Id;
        payment.PaymentNumber = form.Number.Trim();
        payment.PaymentDate = form.PaymentDate;
        payment.Notes = form.Notes;
        payment.Amount = form.Amount;
        payment.PaymentMethodId = form.PaymentMethodId;
        payment.CurrencyId = customer.CurrencyId;
        payment.ExchangeRate = rate;
        payment.BaseAmount = Money.ToBaseMinor(form.Amount, rate);
        payment.UpdatedAt = now;
        await db.SaveChangesAsync();

        await ReplaceAllocationsAsync(db, payment, form.Allocations);
        await tx.CommitAsync();
        return payment.Id;
    }

    /// The customer's payments with an unallocated part (upstream's customer credit).
    public async Task<List<(Payment Payment, long Available)>> AvailableCreditsAsync(int companyId, long customerId)
    {
        await using var db = dbs.Create();
        var payments = await db.Payments.Where(p => p.CompanyId == companyId && p.CustomerId == customerId)
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id).ToListAsync();
        var ids = payments.Select(p => p.Id).ToList();
        var allocated = await db.PaymentAllocations.Where(a => ids.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId).Select(g => new { g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum);
        return payments.Select(p => (p, p.Amount - allocated.GetValueOrDefault(p.Id)))
            .Where(x => x.Item2 > 0).ToList();
    }

    /// applyCustomerCredits(): puts part of a payment's unallocated amount on an invoice,
    /// on top of what the payment already settles.
    public async Task ApplyCreditAsync(int companyId, long paymentId, int invoiceId, long amount)
    {
        if (amount < 1)
            throw new SalesException(Loc.T("Le montant doit être positif."));
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId && p.CompanyId == companyId)
            ?? throw new SalesException(Loc.T("Paiement introuvable."));
        var allocations = await db.PaymentAllocations.Where(a => a.PaymentId == paymentId)
            .Select(a => new Allocation { InvoiceId = a.InvoiceId, Amount = a.Amount }).ToListAsync();
        if (allocations.FirstOrDefault(a => a.InvoiceId == invoiceId) is { } existing)
            existing.Amount += amount;
        else
            allocations.Add(new Allocation { InvoiceId = invoiceId, Amount = amount });
        await ReplaceAllocationsAsync(db, payment, allocations);
        await tx.CommitAsync();
    }

    public async Task<PaymentForm> LoadPaymentAsync(int companyId, long id)
    {
        await using var db = dbs.Create();
        var p = await db.Payments.SingleAsync(x => x.Id == id && x.CompanyId == companyId);
        return new PaymentForm
        {
            Id = p.Id, CustomerId = p.CustomerId, PaymentDate = p.PaymentDate, Number = p.PaymentNumber,
            Amount = p.Amount, PaymentMethodId = p.PaymentMethodId, Notes = p.Notes,
            Allocations = await db.PaymentAllocations.Where(a => a.PaymentId == id).OrderBy(a => a.InvoiceId)
                .Select(a => new Allocation { InvoiceId = a.InvoiceId, Amount = a.Amount }).ToListAsync(),
        };
    }

    /// PaymentAllocationService::replaceLocked().
    private static async Task ReplaceAllocationsAsync(ApplicationDbContext db, Payment payment, List<Allocation> requested)
    {
        if (requested.Select(a => a.InvoiceId).Distinct().Count() != requested.Count)
            throw new SalesException(Loc.T("Une facture apparaît deux fois dans la répartition."));
        if (requested.Any(a => a.InvoiceId < 1 || a.Amount < 1))
            throw new SalesException(Loc.T("Chaque montant affecté doit être positif."));
        var allocations = requested.OrderBy(a => a.InvoiceId).ToList();
        if (allocations.Sum(a => a.Amount) > payment.Amount)
            throw new SalesException(Loc.T("La somme affectée dépasse le montant du paiement."));

        var oldIds = await db.PaymentAllocations.Where(a => a.PaymentId == payment.Id).Select(a => a.InvoiceId).ToListAsync();
        var newIds = allocations.Select(a => a.InvoiceId).ToList();
        var affected = await db.Invoices.Where(i => oldIds.Contains(i.Id) || newIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        if (newIds.Any(id => !affected.ContainsKey(id)))
            throw new SalesException(Loc.T("Facture introuvable."));

        foreach (var allocation in allocations)
        {
            var invoice = affected[allocation.InvoiceId];
            if (invoice.CompanyId != payment.CompanyId || invoice.CustomerId != payment.CustomerId || invoice.CurrencyId != payment.CurrencyId)
                throw new SalesException(Loc.F("La facture {0} n'appartient pas à ce client ou n'est pas dans sa devise.", invoice.InvoiceNumber));
            if (invoice.Type != "INVOICE" || invoice.Status == Draft)
                throw new SalesException(Loc.F("La facture {0} n'est pas payable (brouillon ou note de crédit).", invoice.InvoiceNumber));
            var byOthers = await db.PaymentAllocations.Where(a => a.InvoiceId == invoice.Id && a.PaymentId != payment.Id).SumAsync(a => (long?)a.Amount) ?? 0;
            var credited = -(await db.Invoices.Where(i => i.RelatedInvoiceId == invoice.Id && i.Type == "CREDIT_NOTE").SumAsync(i => (long?)i.Total) ?? 0);
            var available = Math.Max(0, invoice.Total - credited - byOthers);
            if (allocation.Amount > available)
                throw new SalesException(Loc.F("Le montant affecté à {0} dépasse son reste à payer.", invoice.InvoiceNumber));
        }

        await db.PaymentAllocations.Where(a => a.PaymentId == payment.Id).ExecuteDeleteAsync();
        var now = Data.Timestamp.Now();
        foreach (var (allocation, baseAmount) in BaseAmounts(payment, allocations))
            db.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.Id, InvoiceId = allocation.InvoiceId, Amount = allocation.Amount,
                BaseAmount = baseAmount, CreatedAt = now, UpdatedAt = now,
            });
        await db.SaveChangesAsync();

        foreach (var invoice in affected.Values)
            await RecalculateBalanceAsync(db, invoice);
    }

    /// baseAmounts(): each allocation gets its share of the payment's base amount, rounded
    /// down; when the payment is fully allocated, the last row takes the rounding remainder.
    internal static List<(Allocation, long)> BaseAmounts(Payment payment, List<Allocation> allocations)
    {
        var paymentBase = payment.BaseAmount ?? Money.ToBaseMinor(payment.Amount, payment.ExchangeRate is > 0 ? payment.ExchangeRate : 1);
        var allocated = allocations.Sum(a => a.Amount);
        long allocatedBase = 0;
        var result = new List<(Allocation, long)>();
        for (var i = 0; i < allocations.Count; i++)
        {
            var share = allocated == payment.Amount && i == allocations.Count - 1
                ? paymentBase - allocatedBase
                : (long)((Int128)paymentBase * allocations[i].Amount / payment.Amount);
            allocatedBase += share;
            result.Add((allocations[i], share));
        }
        return result;
    }

    public async Task DeletePaymentAsync(int companyId, long id)
    {
        await using var db = dbs.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var payment = await db.Payments.SingleAsync(p => p.Id == id && p.CompanyId == companyId);
        var invoiceIds = await db.PaymentAllocations.Where(a => a.PaymentId == id).Select(a => a.InvoiceId).ToListAsync();
        await db.PaymentAllocations.Where(a => a.PaymentId == id).ExecuteDeleteAsync();
        await CustomFields.DeleteAsync(db, "Payment", id);
        db.Payments.Remove(payment);
        await db.SaveChangesAsync();
        foreach (var invoice in await db.Invoices.Where(i => invoiceIds.Contains(i.Id)).ToListAsync())
            await RecalculateBalanceAsync(db, invoice);
        await tx.CommitAsync();
    }

    // ---- shared -----------------------------------------------------------------

    private static async Task<(Customer, decimal)> ValidateAsync(ApplicationDbContext db, int companyId, DocumentForm form)
    {
        if (form.CustomerId is not { } customerId || await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId) is not { } customer)
            throw new SalesException("Choisissez un client.");
        if (string.IsNullOrWhiteSpace(form.Number))
            throw new SalesException(Loc.T("Le numéro est obligatoire."));
        if (form.Draft.Items.Count == 0 || form.Draft.Items.Any(i => string.IsNullOrWhiteSpace(i.Name)))
            throw new SalesException("Ajoutez au moins une ligne, chacune avec un nom.");

        // InvoiceAttributes: rate 1 in the company currency, the submitted rate otherwise.
        var companyCurrency = await Setting(db, companyId, "currency");
        var foreign = customer.CurrencyId is { } cur && cur.ToString() != companyCurrency;
        if (foreign && form.ExchangeRate <= 0)
            throw new SalesException(Loc.T("Indiquez le taux de change."));
        return (customer, foreign ? form.ExchangeRate : 1);
    }

    internal static Tax TaxRow(DraftTax t, int companyId, int? currencyId, decimal rate, DateTime now,
        int? invoiceId = null, int? invoiceItemId = null, int? estimateId = null, int? estimateItemId = null) => new()
    {
        TaxTypeId = t.TaxTypeId, Name = t.Name, Amount = t.Amount, Percent = t.Percent,
        CalculationType = t.CalculationType, FixedAmount = (int?)t.FixedAmount, CompoundTax = (byte)(t.Compound ? 1 : 0),
        CompanyId = companyId, CurrencyId = currencyId, ExchangeRate = rate, BaseAmount = Money.ToBaseMinor(t.Amount, rate),
        InvoiceId = invoiceId, InvoiceItemId = invoiceItemId, EstimateId = estimateId, EstimateItemId = estimateItemId,
        CreatedAt = now, UpdatedAt = now,
    };

    internal static DraftTax ToDraft(Tax t) => new()
    {
        TaxTypeId = t.TaxTypeId, Name = t.Name, Percent = t.Percent, CalculationType = t.CalculationType,
        FixedAmount = t.FixedAmount, Compound = t.CompoundTax != 0, Amount = t.Amount,
    };

    private static Task<string?> Setting(ApplicationDbContext db, int companyId, string option)
        => db.CompanySettings.Where(s => s.CompanyId == companyId && s.Option == option).Select(s => s.Value).FirstOrDefaultAsync();
}
