using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `customers` table. Column names are part of the v1 API contract.
[Table("customers")]
public class Customer
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Password { get; set; }

    [MaxLength(100)]
    public string? RememberToken { get; set; }

    public string? FacebookId { get; set; }

    public string? GoogleId { get; set; }

    public string? GithubId { get; set; }

    public string? ContactName { get; set; }

    public string? CompanyName { get; set; }

    public string? Website { get; set; }

    public bool EnablePortal { get; set; } = false;

    public int? CurrencyId { get; set; }

    public int? CompanyId { get; set; }

    public int? CreatorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Prefix { get; set; }

    public string? TaxId { get; set; }
}
