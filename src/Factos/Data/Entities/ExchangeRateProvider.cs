using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `exchange_rate_providers` table. Column names are part of the v1 API contract.
[Table("exchange_rate_providers")]
public class ExchangeRateProvider
{
    public long Id { get; set; }

    public string Driver { get; set; } = "";

    public string Key { get; set; } = "";

    public string? Currencies { get; set; }

    public string? DriverConfig { get; set; }

    public bool Active { get; set; } = true;

    public int? CompanyId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
