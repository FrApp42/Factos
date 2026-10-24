using System.Globalization;
using System.Text;
using Factos.Data.Entities;

namespace Factos.Domain;

/// Amounts are stored as integers in hundredths ("cents"), whatever the currency's
/// precision — InvoiceShelf multiplies by 100 everywhere.
public static class Money
{
    /// JavaScript's Math.round: halves go towards +infinity (-2.5 -> -2). The upstream
    /// form computes line and tax amounts with it before posting them.
    public static long JsRound(decimal value) => (long)Math.Floor(value + 0.5m);

    /// PHP's round(): halves go away from zero. The upstream server rounds with it.
    public static long PhpRound(decimal value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

    /// Major units as typed in a form ("12.50") to stored cents.
    public static long ToCents(decimal major) => JsRound(major * 100);

    public static decimal ToMajor(long cents) => cents / 100m;

    /// MoneyConversion::toBaseMinor(): amount × rate, rounded to 6 decimals then half away from zero.
    public static long ToBaseMinor(long? amount, decimal? rate)
        => PhpRound(Math.Round((amount ?? 0) * (rate ?? 0), 6, MidpointRounding.AwayFromZero));

    /// utils/format-money.ts: "$ 1,234.50", or "1.234,50 €" when the symbol is swapped.
    public static string Format(long cents, Currency? currency)
    {
        currency ??= DefaultCurrency;
        var precision = Math.Abs(currency.Precision);
        var amount = Math.Abs(cents / 100m);
        var rounded = Math.Round(amount, precision, MidpointRounding.AwayFromZero);

        var integer = Math.Truncate(rounded).ToString(CultureInfo.InvariantCulture);
        var grouped = new StringBuilder();
        for (var i = 0; i < integer.Length; i++)
        {
            if (i > 0 && (integer.Length - i) % 3 == 0)
                grouped.Append(currency.ThousandSeparator);
            grouped.Append(integer[i]);
        }

        var text = (cents < 0 ? "-" : "") + grouped;
        if (precision > 0)
            text += currency.DecimalSeparator +
                    (rounded - Math.Truncate(rounded)).ToString("F" + precision, CultureInfo.InvariantCulture)[2..];

        return currency.SwapCurrencySymbol ? $"{text} {currency.Symbol}" : $"{currency.Symbol} {text}";
    }

    private static readonly Currency DefaultCurrency = new()
    {
        Name = "US Dollar", Code = "USD", Symbol = "$", Precision = 2,
        ThousandSeparator = ",", DecimalSeparator = ".",
    };
}
