using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `payments` table. Column names are part of the v1 API contract.
[Table("payments")]
public class Payment
{
    public long Id { get; set; }

    public string PaymentNumber { get; set; } = "";

    public DateOnly PaymentDate { get; set; }

    public string? Notes { get; set; }

    public long Amount { get; set; }

    public string? UniqueHash { get; set; }

    public int? CompanyId { get; set; }

    public int? PaymentMethodId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatorId { get; set; }

    public long? CustomerId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseAmount { get; set; }

    public int? CurrencyId { get; set; }

    public int? SequenceNumber { get; set; }

    public int? CustomerSequenceNumber { get; set; }

    public long? TransactionId { get; set; }
}
