using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `companies` table. Column names are part of the v1 API contract.
[Table("companies")]
public class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Logo { get; set; }

    public string? UniqueHash { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Slug { get; set; }

    public int? OwnerId { get; set; }

    public string? VatId { get; set; }

    public string? TaxId { get; set; }
}
