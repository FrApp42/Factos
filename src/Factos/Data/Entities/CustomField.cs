using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `custom_fields` table. Column names are part of the v1 API contract.
[Table("custom_fields")]
public class CustomField
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string Slug { get; set; } = "";

    public string Label { get; set; } = "";

    public string ModelType { get; set; } = "";

    public string Type { get; set; } = "";

    public string? Placeholder { get; set; }

    public string? Options { get; set; }

    public bool? BooleanAnswer { get; set; }

    public DateOnly? DateAnswer { get; set; }

    public TimeOnly? TimeAnswer { get; set; }

    public string? StringAnswer { get; set; }

    public long? NumberAnswer { get; set; }

    public DateTime? DateTimeAnswer { get; set; }

    public bool IsRequired { get; set; } = false;

    public long Order { get; set; } = 1;

    public int CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(20)]
    public string Placement { get; set; } = "internal";

    public string? Validation { get; set; }
}
