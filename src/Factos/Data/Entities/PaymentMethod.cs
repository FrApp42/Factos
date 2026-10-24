using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `payment_methods` table. Column names are part of the v1 API contract.
[Table("payment_methods")]
public class PaymentMethod
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Driver { get; set; }

    [MaxLength(255)]
    public string Type { get; set; } = "GENERAL";

    public string? Settings { get; set; }

    public bool Active { get; set; } = false;

    public bool UseTestEnv { get; set; } = false;
}
