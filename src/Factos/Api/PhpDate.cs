using System.Globalization;
using System.Text;

namespace Factos.Api;

/// PHP's date() / Carbon::format(). InvoiceShelf stores display formats as PHP format
/// strings (company setting `carbon_date_format`, e.g. "d M Y"), so they are rendered
/// with PHP's token table, not .NET's.
public static class PhpDate
{
    public static string Format(DateTime value, string format)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            switch (c)
            {
                case 'd': sb.Append(value.Day.ToString("00", inv)); break;
                case 'D': sb.Append(value.ToString("ddd", inv)); break;
                case 'j': sb.Append(value.Day.ToString(inv)); break;
                case 'l': sb.Append(value.ToString("dddd", inv)); break;
                case 'N': sb.Append(value.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)value.DayOfWeek); break;
                case 'S': sb.Append(OrdinalSuffix(value.Day)); break;
                case 'w': sb.Append((int)value.DayOfWeek); break;
                case 'z': sb.Append(value.DayOfYear - 1); break;
                case 'W': sb.Append(ISOWeek.GetWeekOfYear(value).ToString("00", inv)); break;
                case 'F': sb.Append(value.ToString("MMMM", inv)); break;
                case 'm': sb.Append(value.Month.ToString("00", inv)); break;
                case 'M': sb.Append(value.ToString("MMM", inv)); break;
                case 'n': sb.Append(value.Month); break;
                case 't': sb.Append(DateTime.DaysInMonth(value.Year, value.Month)); break;
                case 'L': sb.Append(DateTime.IsLeapYear(value.Year) ? 1 : 0); break;
                case 'o': sb.Append(ISOWeek.GetYear(value)); break;
                case 'Y': sb.Append(value.Year.ToString("0000", inv)); break;
                case 'y': sb.Append((value.Year % 100).ToString("00", inv)); break;
                case 'a': sb.Append(value.Hour < 12 ? "am" : "pm"); break;
                case 'A': sb.Append(value.Hour < 12 ? "AM" : "PM"); break;
                case 'g': sb.Append(Hour12(value)); break;
                case 'G': sb.Append(value.Hour); break;
                case 'h': sb.Append(Hour12(value).ToString("00", inv)); break;
                case 'H': sb.Append(value.Hour.ToString("00", inv)); break;
                case 'i': sb.Append(value.Minute.ToString("00", inv)); break;
                case 's': sb.Append(value.Second.ToString("00", inv)); break;
                case 'u': sb.Append((value.Ticks % TimeSpan.TicksPerSecond / 10).ToString("000000", inv)); break;
                case 'v': sb.Append(value.Millisecond.ToString("000", inv)); break;
                // App timezone is UTC (config/app.php), so every stored timestamp is UTC.
                case 'e': sb.Append("UTC"); break;
                case 'T': sb.Append("UTC"); break;
                case 'P': sb.Append("+00:00"); break;
                case 'p': sb.Append('Z'); break;
                case 'O': sb.Append("+0000"); break;
                case 'Z': sb.Append('0'); break;
                case 'I': sb.Append('0'); break;
                case 'U': sb.Append(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds()); break;
                case 'c': sb.Append(Format(value, @"Y-m-d\TH:i:sP")); break;
                case 'r': sb.Append(Format(value, "D, d M Y H:i:s O")); break;
                case '\\':
                    if (++i < format.Length) sb.Append(format[i]);
                    break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static int Hour12(DateTime value) => value.Hour % 12 == 0 ? 12 : value.Hour % 12;

    private static string OrdinalSuffix(int day) => day switch
    {
        1 or 21 or 31 => "st",
        2 or 22 => "nd",
        3 or 23 => "rd",
        _ => "th",
    };
}
