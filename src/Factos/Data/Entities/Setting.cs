using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `settings` table. Column names are part of the v1 API contract.
[Table("settings")]
public class Setting
{
    public int Id { get; set; }

    public string Option { get; set; } = "";

    public string? Value { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
