using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `suppliers` table. Column names are part of the v1 API contract.
[Table("suppliers")]
public class Supplier
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public int? CreatorId { get; set; }

    public string Name { get; set; } = "";

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? TaxId { get; set; }

    public int CurrencyId { get; set; }

    public int? ExpenseCategoryId { get; set; }

    public int PaymentTerms { get; set; } = 30;

    public string? Addresses { get; set; }

    public string? Notes { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
