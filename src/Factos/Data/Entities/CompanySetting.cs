using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `company_settings` table. Column names are part of the v1 API contract.
[Table("company_settings")]
public class CompanySetting
{
    public int Id { get; set; }

    public string Option { get; set; } = "";

    public string Value { get; set; } = "";

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
