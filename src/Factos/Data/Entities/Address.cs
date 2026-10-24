using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `addresses` table. Column names are part of the v1 API contract.
[Table("addresses")]
public class Address
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? AddressStreet1 { get; set; }

    public string? AddressStreet2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public int? CountryId { get; set; }

    public string? Zip { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Type { get; set; }

    public int? UserId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CompanyId { get; set; }

    public long? CustomerId { get; set; }
}
