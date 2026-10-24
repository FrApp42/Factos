using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `custom_field_values` table. Column names are part of the v1 API contract.
[Table("custom_field_values")]
public class CustomFieldValue
{
    public long Id { get; set; }

    public string CustomFieldValuableType { get; set; } = "";

    public int CustomFieldValuableId { get; set; }

    public string Type { get; set; } = "";

    public bool? BooleanAnswer { get; set; }

    public DateOnly? DateAnswer { get; set; }

    public TimeOnly? TimeAnswer { get; set; }

    public string? StringAnswer { get; set; }

    public long? NumberAnswer { get; set; }

    public DateTime? DateTimeAnswer { get; set; }

    public long CustomFieldId { get; set; }

    public int CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
