using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `taxes` table. Column names are part of the v1 API contract.
[Table("taxes")]
public class Tax
{
    public int Id { get; set; }

    public int TaxTypeId { get; set; }

    public int? InvoiceId { get; set; }

    public int? EstimateId { get; set; }

    public int? InvoiceItemId { get; set; }

    public int? EstimateItemId { get; set; }

    public int? ItemId { get; set; }

    public int? CompanyId { get; set; }

    public string Name { get; set; } = "";

    public long Amount { get; set; }

    [Precision(5, 2)]
    public decimal? Percent { get; set; }

    public byte CompoundTax { get; set; } = 0;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseAmount { get; set; }

    public int? CurrencyId { get; set; }

    public long? RecurringInvoiceId { get; set; }

    [MaxLength(255)]
    public string CalculationType { get; set; } = "percentage";

    public int? FixedAmount { get; set; }

    public int? ExpenseId { get; set; }
}
