using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `abilities` table. Column names are part of the v1 API contract.
[Table("abilities")]
public class Ability
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string? Title { get; set; }

    public long? EntityId { get; set; }

    public string? EntityType { get; set; }

    public bool OnlyOwned { get; set; } = false;

    public string? Options { get; set; }

    public int? Scope { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
