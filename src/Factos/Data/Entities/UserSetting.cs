using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `user_settings` table. Column names are part of the v1 API contract.
[Table("user_settings")]
public class UserSetting
{
    public long Id { get; set; }

    public string Key { get; set; } = "";

    public string Value { get; set; } = "";

    public int UserId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
