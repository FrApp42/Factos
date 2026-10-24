using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `items` table. Column names are part of the v1 API contract.
[Table("items")]
public class Item
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public long Price { get; set; }

    public int? CompanyId { get; set; }

    public int? UnitId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatorId { get; set; }

    public int? CurrencyId { get; set; }

    public bool TaxPerItem { get; set; } = false;
}
