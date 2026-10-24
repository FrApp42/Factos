using System.Globalization;
using Factos.Data;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// <summary>
/// Port de <c>CashflowQuery::series</c> sur la période par défaut du tableau de bord
/// (<c>ReportingPeriod::fiscalYear</c>) : facturé, encaissé et dépensé, mois par mois.
/// Le facturé prend toutes les factures datées dans la période, notes de crédit comprises
/// (leur total négatif annule la vente), comme upstream.
/// </summary>
public static class Cashflow
{
    public record Series(DateOnly From, string[] Labels, long[] Sales, long[] Receipts, long[] Expenses)
    {
        public long TotalSales => Sales.Sum();
        public long TotalReceipts => Receipts.Sum();
        public long TotalExpenses => Expenses.Sum();
        public long NetIncome => TotalReceipts - TotalExpenses;
        public bool IsEmpty => Sales.Concat(Receipts).Concat(Expenses).All(v => v == 0);
    }

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Premier jour de l'exercice en cours ; <paramref name="setting"/> = réglage <c>fiscal_year</c> (« 4-3 » = avril → mars).</summary>
    public static DateOnly FiscalYearStart(string setting, DateOnly today)
    {
        var month = int.TryParse(setting.Split('-')[0], out var m) && m is >= 1 and <= 12 ? m : 1;
        return new DateOnly(month > today.Month ? today.Year - 1 : today.Year, month, 1);
    }

    public static async Task<Series> FiscalYearAsync(ApplicationDbContext db, int companyId, string fiscalSetting, DateOnly today)
    {
        var from = FiscalYearStart(fiscalSetting, today);
        var end = from.AddMonths(12);
        var (start, stop) = (from.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));

        var invoices = await db.Invoices.Where(i => i.CompanyId == companyId && i.InvoiceDate >= start && i.InvoiceDate < stop)
            .Select(i => new { i.InvoiceDate, Amount = i.BaseTotal ?? i.Total }).ToListAsync();
        var payments = await db.Payments.Where(p => p.CompanyId == companyId && p.PaymentDate >= from && p.PaymentDate < end)
            .Select(p => new { p.PaymentDate, Amount = p.BaseAmount ?? p.Amount }).ToListAsync();
        var expenses = await db.Expenses.Where(e => e.CompanyId == companyId && e.ExpenseDate >= from && e.ExpenseDate < end)
            .Select(e => new { e.ExpenseDate, Amount = e.BaseAmount ?? e.Amount }).ToListAsync();
        // Upstream counts open supplier payments as spent and open supplier refunds as given back.
        var supplierPaid = await db.SupplierPayments.Where(p => p.CompanyId == companyId && p.Status == Purchases.Open && p.PaymentDate >= from && p.PaymentDate < end)
            .Select(p => new { p.PaymentDate, p.BaseAmount }).ToListAsync();
        var supplierRefunded = await db.SupplierRefunds.Where(r => r.CompanyId == companyId && r.Status == Purchases.Open && r.PaymentDate >= from && r.PaymentDate < end)
            .Select(r => new { r.PaymentDate, r.BaseAmount }).ToListAsync();

        long[] Bucket(IEnumerable<(DateOnly Date, long Amount)> rows)
        {
            var sums = new long[12];
            foreach (var (date, amount) in rows) sums[(date.Year - from.Year) * 12 + date.Month - from.Month] += amount;
            return sums;
        }

        return new Series(
            from,
            Enumerable.Range(0, 12).Select(k => from.AddMonths(k).ToString("MMM", French)).ToArray(),
            Bucket(invoices.Select(i => (DateOnly.FromDateTime(i.InvoiceDate), i.Amount))),
            Bucket(payments.Select(p => (p.PaymentDate, p.Amount))),
            Bucket(expenses.Select(e => (e.ExpenseDate, e.Amount))
                .Concat(supplierPaid.Select(p => (p.PaymentDate, p.BaseAmount)))
                .Concat(supplierRefunded.Select(r => (r.PaymentDate, -r.BaseAmount)))));
    }

    /// <summary>Graduations de l'axe des montants : 0 inclus, pas « rond » (1, 2, 2,5, 5 × 10ⁿ), 4 intervalles au plus.</summary>
    public static long[] Ticks(long min, long max)
    {
        (min, max) = (Math.Min(min, 0), Math.Max(max, 0));
        if (min == max) max = 100000; // rien sur la période : 0 → 1 000
        var raw = (max - min) / 4.0;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (long)Math.Ceiling(new[] { 1, 2, 2.5, 5, 10 }.Select(f => f * magnitude).First(s => s >= raw));
        var first = (long)Math.Floor((double)min / step) * step;
        var ticks = new List<long> { first };
        while (ticks[^1] < max) ticks.Add(ticks[^1] + step);
        return [.. ticks];
    }

    /// <summary>Montant abrégé pour l'axe (centièmes → « 12 k », « 1,5 M »).</summary>
    public static string Compact(long cents)
    {
        var v = cents / 100m;
        var (div, suffix) = Math.Abs(v) switch { >= 1_000_000 => (1_000_000m, " M"), >= 1_000 => (1_000m, " k"), _ => (1m, "") };
        return (v / div).ToString("0.#", French) + suffix;
    }
}
