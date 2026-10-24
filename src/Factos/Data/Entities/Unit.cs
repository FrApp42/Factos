using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `units` table. Column names are part of the v1 API contract.
[Table("units")]
public class Unit
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
