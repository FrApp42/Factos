using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `assigned_roles` table. Column names are part of the v1 API contract.
[Table("assigned_roles")]
public class AssignedRole
{
    public long Id { get; set; }

    public long RoleId { get; set; }

    public long EntityId { get; set; }

    public string EntityType { get; set; } = "";

    public long? RestrictedToId { get; set; }

    public string? RestrictedToType { get; set; }

    public int? Scope { get; set; }
}
