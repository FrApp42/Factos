using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `roles` table. Column names are part of the v1 API contract.
[Table("roles")]
public class Role
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string? Title { get; set; }

    public int? Level { get; set; }

    public int? Scope { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
