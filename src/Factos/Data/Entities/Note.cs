using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `notes` table. Column names are part of the v1 API contract.
[Table("notes")]
public class Note
{
    public long Id { get; set; }

    public string Type { get; set; } = "";

    public string Name { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CompanyId { get; set; }

    public bool IsDefault { get; set; } = false;
}
