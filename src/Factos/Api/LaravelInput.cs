using System.Globalization;
using System.Text.Json;

namespace Factos.Api;

/// How Laravel reads request input, where it differs from ASP.NET's binders.
public static class LaravelInput
{
    /// A query-string array: `key[]=a&key[]=b`, `key[0]=a`, or a bare `key=a` (which
    /// `(array)` turns into a one-element list). Empty strings become null, as
    /// ConvertEmptyStringsToNull does. Null when the key is absent.
    public static List<string?>? QueryArray(HttpRequest request, string key)
    {
        var values = request.Query
            .Where(q => q.Key == key || q.Key == key + "[]" ||
                        (q.Key.StartsWith(key + "[") && q.Key.EndsWith(']')))
            .SelectMany(q => q.Value)
            .Select(v => string.IsNullOrEmpty(v) ? null : v)
            .ToList();
        return values.Count == 0 && !request.Query.Keys.Any(k => k == key || k.StartsWith(key + "["))
            ? null
            : values;
    }

    /// The string Eloquent writes to a text column for a decoded JSON value.
    public static string PhpString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.True => "1",
        JsonValueKind.False => "",
        JsonValueKind.Number => value.TryGetInt64(out var i)
            ? i.ToString(CultureInfo.InvariantCulture)
            : value.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        // Upstream fails on these (null into a NOT NULL column, "Array to string
        // conversion"); stored as empty / raw JSON instead of answering 500.
        JsonValueKind.Null => "",
        _ => value.GetRawText(),
    };

    /// The keys PHP's array_keys() yields for a decoded JSON object or list.
    public static IEnumerable<(string Key, JsonElement Value)> Entries(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Select(p => (p.Name, p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select((v, i) => (i.ToString(CultureInfo.InvariantCulture), v)),
        _ => [],
    };
}
