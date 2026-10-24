using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `recurring_invoices` table. Column names are part of the v1 API contract.
[Table("recurring_invoices")]
public class RecurringInvoice
{
    public long Id { get; set; }

    public DateTime StartsAt { get; set; }

    public bool SendAutomatically { get; set; } = false;

    public long? CustomerId { get; set; }

    public int? CompanyId { get; set; }

    [MaxLength(255)]
    public string Status { get; set; } = "ACTIVE";

    public DateTime? NextInvoiceAt { get; set; }

    public int? CreatorId { get; set; }

    public string Frequency { get; set; } = "";

    [MaxLength(255)]
    public string LimitBy { get; set; } = "NONE";

    public int? LimitCount { get; set; }

    public DateOnly? LimitDate { get; set; }

    public int? CurrencyId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public string TaxPerItem { get; set; } = "";

    public string DiscountPerItem { get; set; } = "";

    public string? Notes { get; set; }

    public string? DiscountType { get; set; }

    [Precision(15, 2)]
    public decimal? Discount { get; set; }

    public long? DiscountVal { get; set; }

    public long SubTotal { get; set; }

    public long Total { get; set; }

    public long Tax { get; set; }

    public string? TemplateName { get; set; }

    public long DueAmount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? SalesTaxType { get; set; }

    public string? SalesTaxAddressType { get; set; }

    public bool TaxIncluded { get; set; } = false;

    public string? LastError { get; set; }

    public bool NotifyCreator { get; set; } = false;
}
