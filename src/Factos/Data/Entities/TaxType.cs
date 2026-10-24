using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `tax_types` table. Column names are part of the v1 API contract.
[Table("tax_types")]
public class TaxType
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    [Precision(5, 2)]
    public decimal? Percent { get; set; }

    public byte CompoundTax { get; set; } = 0;

    public byte CollectiveTax { get; set; } = 0;

    public string? Description { get; set; }

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(255)]
    public string Type { get; set; } = "GENERAL";

    [MaxLength(255)]
    public string CalculationType { get; set; } = "percentage";

    public int? FixedAmount { get; set; }

    [MaxLength(20)]
    public string TransactionType { get; set; } = "sales";
}
