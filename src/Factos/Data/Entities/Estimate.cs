using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `estimates` table. Column names are part of the v1 API contract.
[Table("estimates")]
public class Estimate
{
    public int Id { get; set; }

    public DateOnly EstimateDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string EstimateNumber { get; set; } = "";

    public string Status { get; set; } = "";

    public string? ReferenceNumber { get; set; }

    public string TaxPerItem { get; set; } = "";

    public string DiscountPerItem { get; set; } = "";

    public string? Notes { get; set; }

    [Precision(15, 2)]
    public decimal? Discount { get; set; }

    public string? DiscountType { get; set; }

    public long? DiscountVal { get; set; }

    public long SubTotal { get; set; }

    public long Total { get; set; }

    public long Tax { get; set; }

    public string? UniqueHash { get; set; }

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatorId { get; set; }

    public string? TemplateName { get; set; }

    public long? CustomerId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseDiscountVal { get; set; }

    public long? BaseSubTotal { get; set; }

    public long? BaseTotal { get; set; }

    public long? BaseTax { get; set; }

    public int? CurrencyId { get; set; }

    public int? SequenceNumber { get; set; }

    public int? CustomerSequenceNumber { get; set; }

    public string? SalesTaxType { get; set; }

    public string? SalesTaxAddressType { get; set; }

    public bool TaxIncluded { get; set; } = false;
}
