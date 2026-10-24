using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `file_disks` table. Column names are part of the v1 API contract.
[Table("file_disks")]
public class FileDisk
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string Type { get; set; } = "REMOTE";

    public string Driver { get; set; } = "";

    public bool SetAsDefault { get; set; } = false;

    public string Credentials { get; set; } = "";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
