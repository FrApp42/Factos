using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `currencies` table. Column names are part of the v1 API contract.
[Table("currencies")]
public class Currency
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Code { get; set; } = "";

    public string? Symbol { get; set; }

    public int Precision { get; set; }

    public string ThousandSeparator { get; set; } = "";

    public string DecimalSeparator { get; set; } = "";

    public bool SwapCurrencySymbol { get; set; } = false;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
