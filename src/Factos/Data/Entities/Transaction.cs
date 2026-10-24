using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `transactions` table. Column names are part of the v1 API contract.
[Table("transactions")]
public class Transaction
{
    public long Id { get; set; }

    public string? TransactionId { get; set; }

    public string? UniqueHash { get; set; }

    public string? Type { get; set; }

    public string Status { get; set; } = "";

    public DateTime TransactionDate { get; set; }

    public int? CompanyId { get; set; }

    public int InvoiceId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
