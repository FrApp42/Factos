using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Factos.Api;

/// Response envelopes InvoiceShelf inherits from Laravel 13. Shapes and messages here
/// are part of the v1 API contract: changing a key or a sentence breaks existing clients.
public static class LaravelResponse
{
    /// 422, as produced by ValidationException: the message is the first error,
    /// followed by "(and N more error[s])" when there are others.
    public static IActionResult ValidationError(IReadOnlyDictionary<string, string[]> errors)
    {
        var all = errors.SelectMany(e => e.Value).ToList();
        var message = all.Count == 0 ? "The given data was invalid." : all[0];
        if (all.Count > 1)
            message += $" (and {all.Count - 1} more {(all.Count == 2 ? "error" : "errors")})";

        return new ObjectResult(new Dictionary<string, object?> { ["message"] = message, ["errors"] = errors })
        { StatusCode = StatusCodes.Status422UnprocessableEntity };
    }

    /// Body ASP.NET could not bind at all (malformed JSON): still answered in Laravel's 422 shape.
    public static IActionResult ValidationError(ModelStateDictionary modelState)
        => ValidationError(modelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => Naming.ToSnakeCase(e.Key.TrimStart('$', '.')),
                e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray()));

    public static IActionResult Message(int status, string message)
        => new ObjectResult(new Dictionary<string, object?> { ["message"] = message }) { StatusCode = status };

    /// ModelNotFoundException rendered as JSON: Laravel keeps the exception message for
    /// HTTP exceptions even outside debug mode, class name included. find() failures carry
    /// the id ("... [X] 5"); firstOrFail() on a query carries none ("... [X].").
    public static IActionResult ModelNotFound(string modelClass, object? id = null)
        => Message(StatusCodes.Status404NotFound,
            $"No query results for model [{modelClass}]" + (id is null ? "." : $" {id}"));

    public const string Unauthenticated = "Unauthenticated.";

    public static IActionResult Forbidden() => Message(StatusCodes.Status403Forbidden, "This action is unauthorized.");
    public const string TooManyAttempts = "Too Many Attempts.";

    /// Laravel's length-aware paginator wrapped by an API Resource collection.
    public static async Task<object> PaginateAsync<T>(IQueryable<T> query, HttpRequest request, int? perPage = null)
    {
        var page = Math.Max(1, ParseInt(request.Query["page"]) ?? 1);
        var size = Math.Clamp(perPage ?? ParseInt(request.Query["limit"]) ?? 10, 1, 500);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        var lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)size);
        var path = $"{request.Scheme}://{request.Host}{request.Path}";

        return new
        {
            data = items,
            links = new
            {
                first = $"{path}?page=1",
                last = $"{path}?page={lastPage}",
                prev = page > 1 ? $"{path}?page={page - 1}" : null,
                next = page < lastPage ? $"{path}?page={page + 1}" : null,
            },
            meta = new
            {
                current_page = page,
                from = items.Count == 0 ? (int?)null : (page - 1) * size + 1,
                last_page = lastPage,
                path,
                per_page = size,
                to = items.Count == 0 ? (int?)null : (page - 1) * size + items.Count,
                total,
            },
        };
    }

    private static int? ParseInt(string? value) => int.TryParse(value, out var i) ? i : null;
}

/// Carbon's JSON form: UTC, microseconds, trailing Z ("2026-10-22T19:27:06.000000Z").
/// Timestamps are stored in the app timezone, which is UTC.
public class LaravelDateTimeConverter : JsonConverter<DateTime>
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}

/// Laravel's `required` rule and its default English message.
public class LaravelValidator
{
    private readonly Dictionary<string, string[]> _errors = new();

    public bool Failed => _errors.Count > 0;

    public LaravelValidator Required(string field, object? value)
    {
        var missing = value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            System.Collections.ICollection c => c.Count == 0,
            _ => false,
        };
        if (missing)
            Add(field, $"The {field.Replace('_', ' ')} field is required.");
        return this;
    }

    /// `min:n` on a string: length in characters.
    public LaravelValidator Min(string field, string? value, int min)
    {
        if (!string.IsNullOrEmpty(value) && value.Length < min)
            Add(field, $"The {field.Replace('_', ' ')} field must be at least {min} characters.");
        return this;
    }

    /// InvoiceShelf's IdnEmail rule: IDN domain converted to ASCII, then PHP's
    /// FILTER_VALIDATE_EMAIL. Empty values are left to `required`.
    // ponytail: MailAddress approximates FILTER_VALIDATE_EMAIL; exotic quoted local parts may differ.
    public LaravelValidator IdnEmail(string field, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        var parts = value.Split('@');
        var valid = parts.Length == 2 && parts[0].Length > 0 && parts[1].Contains('.')
            && !value.Any(char.IsWhiteSpace) && TryAscii(parts[1], out var domain)
            && System.Net.Mail.MailAddress.TryCreate($"{parts[0]}@{domain}", out var parsed)
            && parsed.Address == $"{parts[0]}@{domain}";
        if (!valid)
            Add(field, $"The {field.Replace('_', ' ')} must be a valid email address.");
        return this;

        static bool TryAscii(string domain, out string ascii)
        {
            try { ascii = new System.Globalization.IdnMapping().GetAscii(domain); return true; }
            catch (ArgumentException) { ascii = ""; return false; }
        }
    }

    public LaravelValidator Unique(string field, bool taken)
    {
        if (taken)
            Add(field, $"The {field.Replace('_', ' ')} has already been taken.");
        return this;
    }

    public bool Has(string field) => _errors.ContainsKey(field);

    public LaravelValidator Add(string field, string message)
    {
        _errors[field] = _errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];
        return this;
    }

    public IActionResult Result() => LaravelResponse.ValidationError(_errors);
}
