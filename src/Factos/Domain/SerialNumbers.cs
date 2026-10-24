using System.Security.Cryptography;
using System.Text;

namespace Factos.Domain;

/// Port of InvoiceShelf's SerialNumberService: document numbers built from a format such
/// as "{{SERIES:INV}}{{DELIMITER:-}}{{SEQUENCE:6}}" (company setting
/// `<model>_number_format`) and the next free sequence numbers.
public static class SerialNumbers
{
    private static readonly string[] ValidPlaceholders =
        ["CUSTOMER_SERIES", "SEQUENCE", "DATE_FORMAT", "SERIES", "RANDOM_SEQUENCE", "DELIMITER", "CUSTOMER_SEQUENCE"];
    private const string NameBytes = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_";
    private const string ValueBytes = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";
    private const int ValueLimit = 6;

    public static string Generate(string format, int sequence, int customerSequence, string? customerPrefix, DateTime now)
    {
        var result = new StringBuilder();
        foreach (var (name, value) in Placeholders(format))
            result.Append(name switch
            {
                "SEQUENCE" => sequence.ToString().PadLeft(value == "" ? 6 : int.Parse(value), '0'),
                "CUSTOMER_SEQUENCE" => customerSequence.ToString().PadLeft(int.TryParse(value, out var w) ? w : 0, '0'),
                "DATE_FORMAT" => Api.PhpDate.Format(now, value == "" ? "Y" : value),
                "RANDOM_SEQUENCE" => RandomHex(value == "" ? 6 : int.Parse(value)),
                "CUSTOMER_SERIES" => customerPrefix ?? "CST",
                _ => value,
            });
        return result.ToString();
    }

    /// getPlaceholders(): scans for {{NAME}} / {{NAME:value}} tokens, keeping valid names only.
    public static List<(string Name, string Value)> Placeholders(string format)
    {
        var found = new List<(string, string)>();
        var cursor = 0;
        while (cursor < format.Length)
        {
            var token = ReadToken(format, cursor);
            if (token is null)
            {
                cursor++;
                continue;
            }
            var (name, value, next) = token.Value;
            cursor = next;
            if (ValidPlaceholders.Contains(name))
                found.Add((name, value));
        }
        return found;
    }

    private static (string, string, int)? ReadToken(string format, int start)
    {
        if (!At(format, start, "{{"))
            return null;
        var nameAt = start + 2;
        for (var width = RunLength(format, NameBytes, nameAt); width > 0; width--)
            foreach (var colon in new[] { true, false })
            {
                var valueAt = nameAt + width;
                if (colon)
                {
                    if (valueAt >= format.Length || format[valueAt] != ':')
                        continue;
                    valueAt++;
                }
                if (ReadValue(format, valueAt) is { } value)
                    return (format.Substring(nameAt, width), value.Item1, value.Item2);
            }
        return null;
    }

    private static (string, int)? ReadValue(string format, int at)
    {
        var run = RunLength(format, ValueBytes, at);
        if (run > 0 && run <= ValueLimit && At(format, at + run, "}}"))
            return (format.Substring(at, run), at + run + 2);
        if (at < format.Length && format[at] != '\n' && At(format, at + 1, "}}"))
            return (format[at].ToString(), at + 3);
        if (At(format, at, "}}"))
            return ("", at + 2);
        return null;
    }

    private static int RunLength(string format, string bytes, int at)
    {
        var length = 0;
        while (at + length < format.Length && bytes.Contains(format[at + length]))
            length++;
        return length;
    }

    private static bool At(string s, int index, string value)
        => index >= 0 && index + value.Length <= s.Length && string.CompareOrdinal(s, index, value, 0, value.Length) == 0;

    private static string RandomHex(int length)
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(length))[..length];
}
