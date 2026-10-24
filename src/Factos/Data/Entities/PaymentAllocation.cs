using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `payment_allocations` table. Column names are part of the v1 API contract.
[Table("payment_allocations")]
public class PaymentAllocation
{
    public long Id { get; set; }

    public long PaymentId { get; set; }

    public int InvoiceId { get; set; }

    public long Amount { get; set; }

    public long BaseAmount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
