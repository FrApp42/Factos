using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `expenses` table. Column names are part of the v1 API contract.
[Table("expenses")]
public class Expense
{
    public int Id { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public string? AttachmentReceipt { get; set; }

    public long Amount { get; set; }

    public string? Notes { get; set; }

    public int ExpenseCategoryId { get; set; }

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatorId { get; set; }

    public long? CustomerId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseAmount { get; set; }

    public int? CurrencyId { get; set; }

    public int? PaymentMethodId { get; set; }

    public string? ExpenseNumber { get; set; }

    public long? SupplierId { get; set; }
}
