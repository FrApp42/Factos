using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `exchange_rate_logs` table. Column names are part of the v1 API contract.
[Table("exchange_rate_logs")]
public class ExchangeRateLog
{
    public long Id { get; set; }

    public int? CompanyId { get; set; }

    public int? BaseCurrencyId { get; set; }

    public int? CurrencyId { get; set; }

    [Precision(19, 6)]
    public decimal? ExchangeRate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
