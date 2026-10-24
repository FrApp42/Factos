using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `invoices` table. Column names are part of the v1 API contract.
[Table("invoices")]
public class Invoice
{
    public int Id { get; set; }

    public DateTime InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string InvoiceNumber { get; set; } = "";

    public string? ReferenceNumber { get; set; }

    public string Status { get; set; } = "";

    public string PaidStatus { get; set; } = "";

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

    public long DueAmount { get; set; }

    public bool Sent { get; set; } = false;

    public bool Viewed { get; set; } = false;

    public string? UniqueHash { get; set; }

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatorId { get; set; }

    public string? TemplateName { get; set; }

    public long? CustomerId { get; set; }

    public long? RecurringInvoiceId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public long? BaseDiscountVal { get; set; }

    public long? BaseSubTotal { get; set; }

    public long? BaseTotal { get; set; }

    public long? BaseTax { get; set; }

    public long? BaseDueAmount { get; set; }

    public int? CurrencyId { get; set; }

    public int? SequenceNumber { get; set; }

    public int? CustomerSequenceNumber { get; set; }

    public string? SalesTaxType { get; set; }

    public string? SalesTaxAddressType { get; set; }

    public bool Overdue { get; set; } = false;

    public bool TaxIncluded { get; set; } = false;

    public string Type { get; set; } = "INVOICE";

    public int? RelatedInvoiceId { get; set; }

    public string? CreditReason { get; set; }
}
