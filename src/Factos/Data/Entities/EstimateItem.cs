using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `estimate_items` table. Column names are part of the v1 API contract.
[Table("estimate_items")]
public class EstimateItem
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public string DiscountType { get; set; } = "";

    [Precision(15, 2)]
    public decimal Quantity { get; set; }

    [Precision(15, 2)]
    public decimal? Discount { get; set; }

    public long? DiscountVal { get; set; }

    public long Price { get; set; }

    public long Tax { get; set; }

    public long Total { get; set; }

    public int? ItemId { get; set; }

    public int EstimateId { get; set; }

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UnitName { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseDiscountVal { get; set; }

    public long? BasePrice { get; set; }

    public long? BaseTax { get; set; }

    public long? BaseTotal { get; set; }
}
