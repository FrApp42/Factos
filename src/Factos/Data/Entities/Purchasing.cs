using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

// Mirrors InvoiceShelf's purchasing settlement tables
// (2026_09_27_191119_create_purchasing_settlement_tables). The abstract bases are the
// migration's shared column sets (`record()`, the bill/credit document shape, the item
// and allocation tables); they are not mapped themselves.

/// The columns every supplier payment, refund, bill and credit carries.
public abstract class PurchaseRecord
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public int? CreatorId { get; set; }

    public long SupplierId { get; set; }

    public int CurrencyId { get; set; }

    [Precision(20, 6)]
    public decimal ExchangeRate { get; set; } = 1;

    public string? Number { get; set; }

    public int? SequenceNumber { get; set; }

    public string? Reference { get; set; }

    public string Status { get; set; } = "DRAFT";

    public string? Notes { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// Bills and supplier credits share one document shape.
public abstract class PurchaseDocument : PurchaseRecord
{
    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool TaxIncluded { get; set; }

    public DateTime? FinancialLockedAt { get; set; }

    public long SubTotal { get; set; }

    public long Tax { get; set; }

    public long Total { get; set; }

    public long BaseTotal { get; set; }

    public long DueAmount { get; set; }

    public long BaseDueAmount { get; set; }

    public long? SourceBillId { get; set; }

    public int? SourceExpenseId { get; set; }

    public string? SupplierSnapshot { get; set; }
}

[Table("bills")]
[Index(nameof(CompanyId), nameof(Number), IsUnique = true)]
public class Bill : PurchaseDocument;

[Table("supplier_credits")]
[Index(nameof(CompanyId), nameof(Number), IsUnique = true)]
public class SupplierCredit : PurchaseDocument;

public abstract class PurchaseItem
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public int ExpenseCategoryId { get; set; }

    public long? SourceBillItemId { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = "";

    [Precision(15, 2)]
    public decimal Quantity { get; set; }

    public long Price { get; set; }

    [Precision(15, 2)]
    public decimal Discount { get; set; }

    public string DiscountType { get; set; } = "percentage";

    public long SubTotal { get; set; }

    public long DiscountVal { get; set; }

    public long Tax { get; set; }

    public long Total { get; set; }

    public long BaseTotal { get; set; }

    /// JSON list of tax snapshots: name, percent, calculation_type, fixed_amount,
    /// compound_tax, tax_type_id, amount, base_amount.
    public string? Taxes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

[Table("bill_items")]
public class BillItem : PurchaseItem
{
    public long BillId { get; set; }
}

[Table("supplier_credit_items")]
public class SupplierCreditItem : PurchaseItem
{
    public long SupplierCreditId { get; set; }
}

public abstract class SupplierMoney : PurchaseRecord
{
    public DateOnly PaymentDate { get; set; }

    public long Amount { get; set; }

    public long BaseAmount { get; set; }

    public int? PaymentMethodId { get; set; }
}

[Table("supplier_payments")]
[Index(nameof(CompanyId), nameof(Number), IsUnique = true)]
public class SupplierPayment : SupplierMoney;

[Table("supplier_refunds")]
[Index(nameof(CompanyId), nameof(Number), IsUnique = true)]
public class SupplierRefund : SupplierMoney
{
    public long? SupplierPaymentId { get; set; }

    public long? SupplierCreditId { get; set; }
}

public abstract class BillAllocation
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public long BillId { get; set; }

    public long Amount { get; set; }

    public long BaseAmount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

[Table("supplier_payment_allocations")]
[Index(nameof(SupplierPaymentId), nameof(BillId), IsUnique = true, Name = "supplier_payment_bill_unique")]
public class SupplierPaymentAllocation : BillAllocation
{
    public long SupplierPaymentId { get; set; }
}

[Table("supplier_credit_allocations")]
[Index(nameof(SupplierCreditId), nameof(BillId), IsUnique = true, Name = "supplier_credit_bill_unique")]
public class SupplierCreditAllocation : BillAllocation
{
    public long SupplierCreditId { get; set; }
}
