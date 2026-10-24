using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `permissions` table. Column names are part of the v1 API contract.
[Table("permissions")]
public class Permission
{
    public long Id { get; set; }

    public long AbilityId { get; set; }

    public long? EntityId { get; set; }

    public string? EntityType { get; set; }

    public bool Forbidden { get; set; } = false;

    public int? Scope { get; set; }
}
