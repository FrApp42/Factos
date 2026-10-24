using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Factos.Components;
using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// One record's answer to one definition, as the form edits it. `Value` is the input's own text:
/// Date `yyyy-MM-dd`, Time `HH:mm`, DateTime `yyyy-MM-ddTHH:mm`, Switch `1`/`0`, blank = no answer.
public sealed class CustomFieldAnswer
{
    public required CustomField Field { get; init; }
    public string? Value { get; set; }
    public bool Checked { get => Value == "1"; set => Value = value ? "1" : "0"; }
}

/// Port of upstream's Metadata domain (CustomFieldModelCatalog, CustomFieldService,
/// EloquentCustomFieldValueWriter, CustomFieldAnswerValidator, HasCustomFields).
public static class CustomFields
{
    public const string Internal = "internal", Document = "document", Today = "today";

    /// `custom_fields.model_type` keys this UI answers, with the morph alias written to
    /// `custom_field_values.custom_field_valuable_type` (upstream's ModelIdentityMap).
    public static readonly (string Key, string Label, string Morph)[] Models =
    [
        ("Customer", "Client", "customer"), ("Invoice", "Facture", "invoice"), ("Estimate", "Devis", "estimate"),
        ("Payment", "Paiement", "customer_payment"), ("Expense", "Dépense", "expense"), ("Item", "Article", "item"),
    ];

    public static readonly (string Key, string Label)[] Types =
    [
        ("Input", "Texte"), ("TextArea", "Zone de texte"), ("Phone", "Téléphone"), ("Url", "URL"), ("Number", "Nombre"),
        ("Dropdown", "Liste déroulante"), ("Switch", "Interrupteur"), ("Date", "Date"), ("Time", "Heure"), ("DateTime", "Date et heure"),
    ];

    public static string Morph(string model) => Models.First(m => m.Key == model).Morph;

    /// getCustomFieldValueKey().
    public static string ValueKey(string type) => type switch
    {
        "Number" or "Phone" => "number_answer",
        "Switch" => "boolean_answer",
        "Date" => "date_answer",
        "Time" => "time_answer",
        "DateTime" => "date_time_answer",
        _ => "string_answer",
    };

    public static string? Read(ICustomFieldAnswer a) => ValueKey(a.Type) switch
    {
        "number_answer" => a.NumberAnswer?.ToString(CultureInfo.InvariantCulture),
        "boolean_answer" => a.BooleanAnswer is { } b ? (b ? "1" : "0") : null,
        "date_answer" => a.DateAnswer?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "time_answer" => a.TimeAnswer?.ToString("HH:mm", CultureInfo.InvariantCulture),
        "date_time_answer" => a.DateTimeAnswer?.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
        _ => a.StringAnswer,
    };

    /// Writes the column the type maps to, and only that one, like upstream.
    public static void Write(ICustomFieldAnswer a, string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var inv = CultureInfo.InvariantCulture;
        switch (ValueKey(a.Type))
        {
            case "number_answer": a.NumberAnswer = value is null ? null : long.Parse(value, inv); break;
            case "boolean_answer": a.BooleanAnswer = value is null ? null : value == "1"; break;
            case "date_answer": a.DateAnswer = value is null ? null : DateOnly.Parse(value, inv); break;
            case "time_answer": a.TimeAnswer = value is null ? null : TimeOnly.Parse(value, inv); break;
            case "date_time_answer": a.DateTimeAnswer = value is null ? null : DateTime.Parse(value, inv); break;
            default: a.StringAnswer = value; break;
        }
    }

    /// The answer as getFieldsArray() substitutes it: PHP's string form of the stored value.
    public static string? Raw(ICustomFieldAnswer a) => ValueKey(a.Type) switch
    {
        "boolean_answer" => a.BooleanAnswer == true ? "1" : "",
        "time_answer" => a.TimeAnswer?.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        "date_time_answer" => a.DateTimeAnswer?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => Read(a),
    };

    /// formatted_answer: dates in the company's format, date-times as Y-m-d H:i.
    public static string? Formatted(ICustomFieldAnswer a, string dateFormat) => ValueKey(a.Type) switch
    {
        "date_answer" => a.DateAnswer is { } d ? Api.PhpDate.Format(d.ToDateTime(TimeOnly.MinValue), dateFormat) : null,
        "date_time_answer" => a.DateTimeAnswer?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        "boolean_answer" => a.BooleanAnswer is { } b ? (b ? Loc.T("Oui") : Loc.T("Non")) : null,
        _ => Raw(a),
    };

    /// Dropdown options, stored as a JSON array of strings (older rows: `{name: …}` objects).
    public static List<string> Options(CustomField field)
    {
        if (string.IsNullOrWhiteSpace(field.Options)) return [];
        try
        {
            using var doc = JsonDocument.Parse(field.Options);
            return doc.RootElement.EnumerateArray()
                .Select(o => o.ValueKind == JsonValueKind.Object ? (o.TryGetProperty("name", out var n) ? n.GetString() : null) : o.ToString())
                .Where(o => !string.IsNullOrEmpty(o)).ToList()!;
        }
        catch (JsonException) { return []; }
    }

    /// `validation` JSON: min_length, max_length, min, max, earliest, latest, pattern.
    public static Dictionary<string, string> Rules(CustomField field)
    {
        if (string.IsNullOrWhiteSpace(field.Validation)) return [];
        try
        {
            using var doc = JsonDocument.Parse(field.Validation);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            return doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind is not JsonValueKind.Null)
                .ToDictionary(p => p.Name, p => p.Value.ToString());
        }
        catch (JsonException) { return []; }
    }

    public static string? SerializeRules(Dictionary<string, string> rules)
    {
        var kept = rules.Where(r => !string.IsNullOrWhiteSpace(r.Value)).ToDictionary(r => r.Key, r => (object)(
            r.Key is "min_length" or "max_length" or "min" or "max" && decimal.TryParse(r.Value, CultureInfo.InvariantCulture, out var n) ? n : r.Value.Trim()));
        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept);
    }

    /// clean_slug(): CUSTOM_<MODEL>_<NAME>, then _1 … _10; null when all are taken.
    public static string? Slug(string model, string name, IReadOnlyCollection<string> taken)
    {
        var ascii = new string(name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        var slug = Regex.Replace(ascii, "[^a-z0-9]+", "_").Trim('_');
        var baseSlug = ("CUSTOM_" + model + "_" + slug).ToUpperInvariant();
        return new[] { baseSlug }.Concat(Enumerable.Range(1, 10).Select(n => $"{baseSlug}_{n}")).FirstOrDefault(s => !taken.Contains(s));
    }

    // ---- validation (CustomFieldRequest / CustomFieldAnswerValidator) --------------------

    /// Errors in a definition the settings form submits.
    public static string? ValidateDefinition(CustomField field)
    {
        if (string.IsNullOrWhiteSpace(field.Name) || string.IsNullOrWhiteSpace(field.Label))
            return Loc.T("Le nom et le libellé sont obligatoires.");
        var rules = Rules(field);
        foreach (var (low, high) in new[] { ("min_length", "max_length"), ("min", "max") })
            if (Number(rules, low) is { } from && Number(rules, high) is { } to && to < from)
                return Loc.T("Le maximum ne peut pas être inférieur au minimum.");
        DateTime? earliest = null, latest = null;
        foreach (var key in new[] { "earliest", "latest" })
        {
            if (!rules.TryGetValue(key, out var bound) || bound == Today) continue;
            if (field.Type == "Time")
            {
                if (TimeOfDay(bound) is null) return Loc.T("Cette borne n'est pas une heure lisible.");
                continue;
            }
            if (!DateTime.TryParse(bound, CultureInfo.InvariantCulture, out var d))
                return Loc.T("Cette borne n'est pas une date lisible.");
            if (key == "earliest") earliest = d; else latest = d;
        }
        if (earliest is not null && latest < earliest)
            return Loc.T("La borne la plus tardive précède la plus ancienne.");
        if (rules.TryGetValue("pattern", out var pattern))
        {
            try { _ = new Regex(pattern); }
            catch (ArgumentException) { return Loc.T("Ce motif n'est pas valide."); }
        }
        return null;
    }

    /// First error for one answer, or null (`is_required`, options, length, range, pattern, dates).
    public static string? Validate(CustomFieldAnswer answer, TimeZoneInfo zone)
    {
        var field = answer.Field;
        var value = answer.Value?.Trim();
        // A switch turned off is an answer; only nothing is not.
        if (string.IsNullOrEmpty(value))
            return field.IsRequired ? Loc.F("{0} est obligatoire.", field.Label) : null;
        return ValidateValue(field, value, zone);
    }

    private static string? ValidateValue(CustomField field, string value, TimeZoneInfo? zone = null)
    {
        var label = field.Label;
        var inv = CultureInfo.InvariantCulture;
        var parses = ValueKey(field.Type) switch
        {
            "number_answer" => long.TryParse(value, inv, out _),
            "date_answer" => DateOnly.TryParse(value, inv, out _),
            "time_answer" => TimeOnly.TryParse(value, inv, out _),
            "date_time_answer" => DateTime.TryParse(value, inv, out _),
            _ => true,
        };
        if (!parses)
            return field.Type is "Number" or "Phone" ? Loc.F("{0} doit être un nombre entier.", label) : Loc.F("{0} n'est pas une date valide.", label);

        var options = field.Type == "Dropdown" ? Options(field) : [];
        if (options.Count > 0 && !options.Contains(value))
            return Loc.F("{0} doit être l'une des options proposées.", label);

        var rules = Rules(field);
        if (rules.Count == 0) return null;

        if (Number(rules, "min_length") is { } minLength && value.Length < minLength)
            return Loc.F("{0} doit contenir au moins {1} caractères.", label, minLength);
        if (Number(rules, "max_length") is { } maxLength && value.Length > maxLength)
            return Loc.F("{0} ne doit pas dépasser {1} caractères.", label, maxLength);
        if (decimal.TryParse(value, NumberStyles.Number, inv, out var number))
        {
            if (Number(rules, "min") is { } min && number < min) return Loc.F("{0} ne peut pas être inférieur à {1}.", label, min);
            if (Number(rules, "max") is { } max && number > max) return Loc.F("{0} ne peut pas être supérieur à {1}.", label, max);
        }
        if (rules.TryGetValue("pattern", out var pattern) && pattern != "")
        {
            bool match;
            try { match = Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
            catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException) { match = false; }
            if (!match) return Loc.F("{0} n'est pas au format attendu.", label);
        }
        return DateRange(field, value, rules, zone ?? TimeZoneInfo.Utc);
    }

    private static string? DateRange(CustomField field, string value, Dictionary<string, string> rules, TimeZoneInfo zone)
    {
        rules.TryGetValue("earliest", out var earliest);
        rules.TryGetValue("latest", out var latest);
        if (earliest is null && latest is null) return null;
        var label = field.Label;

        if (field.Type == "Time")
        {
            // Zero-padded H:i orders correctly as text.
            if (TimeOfDay(value) is not { } answer) return null;
            if (earliest is not null && TimeOfDay(earliest) is { } from && string.CompareOrdinal(answer, from) < 0)
                return Loc.F("{0} ne peut pas être avant {1}.", label, from);
            if (latest is not null && TimeOfDay(latest) is { } to && string.CompareOrdinal(answer, to) > 0)
                return Loc.F("{0} ne peut pas être après {1}.", label, to);
            return null;
        }
        if (field.Type is not ("Date" or "DateTime") || !DateTime.TryParse(value, CultureInfo.InvariantCulture, out var moment))
            return null;

        // Both sides are wall-clock times in the company's zone; `today` is the company's today.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).ToDateTime(TimeOnly.MinValue);
        DateTime? Bound(string? b, bool upper) => b is null ? null
            : b == Today ? (upper ? today.AddDays(1).AddTicks(-1) : today)
            : DateTime.TryParse(b, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (Bound(earliest, false) is { } lo && moment < lo)
            return Loc.F("{0} ne peut pas être avant le {1}.", label, lo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (Bound(latest, true) is { } hi && moment > hi)
            return Loc.F("{0} ne peut pas être après le {1}.", label, hi.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return null;
    }

    private static decimal? Number(Dictionary<string, string> rules, string key)
        => rules.TryGetValue(key, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string? TimeOfDay(string value)
        => Regex.IsMatch(value, "^([01][0-9]|2[0-3]):[0-5][0-9]") ? value[..5] : null;

    // ---- storage ------------------------------------------------------------------------

    /// The company's definitions for a model, in form order, with the record's answers —
    /// or each definition's default answer for a new record (recordId null).
    public static async Task<List<CustomFieldAnswer>> LoadAsync(ApplicationDbContext db, int companyId, string model, long? recordId)
    {
        var fields = await db.CustomFields.AsNoTracking().Where(f => f.CompanyId == companyId && f.ModelType == model)
            .OrderBy(f => f.Order).ThenBy(f => f.Id).ToListAsync();
        var values = recordId is { } id ? await Values(db, model, id).ToDictionaryAsync(v => v.CustomFieldId) : [];
        return fields.Select(f => new CustomFieldAnswer
        {
            Field = f,
            // Read with the definition's current type: an answer written under another type is not shown.
            Value = recordId is null ? Read(f) : values.TryGetValue(f.Id, out var v) && v.Type == f.Type ? Read(v) : null,
        }).ToList();
    }

    /// First validation error among the answers.
    public static string? Validate(IEnumerable<CustomFieldAnswer> answers, string? timeZone)
    {
        var zone = Cadence.Zone(timeZone);
        return answers.Select(a => Validate(a, zone)).FirstOrDefault(e => e is not null);
    }

    /// EloquentCustomFieldValueWriter::update(): one row per definition, matched on the definition alone.
    public static async Task SaveAsync(ApplicationDbContext db, int companyId, string model, long recordId, IEnumerable<CustomFieldAnswer> answers)
    {
        var existing = await Values(db, model, recordId).ToDictionaryAsync(v => v.CustomFieldId);
        var now = Timestamp.Now();
        foreach (var answer in answers)
        {
            if (!existing.TryGetValue(answer.Field.Id, out var row))
            {
                row = new CustomFieldValue
                {
                    CustomFieldValuableType = Morph(model), CustomFieldValuableId = (int)recordId,
                    CustomFieldId = answer.Field.Id, CreatedAt = now,
                };
                db.CustomFieldValues.Add(row);
            }
            row.Type = answer.Field.Type;
            row.CompanyId = companyId;
            row.UpdatedAt = now;
            Write(row, answer.Value);
        }
        await db.SaveChangesAsync();
    }

    public static Task SaveAsync(AppDbFactory dbs, int companyId, string model, long recordId, IEnumerable<CustomFieldAnswer> answers)
        => Using(dbs, db => SaveAsync(db, companyId, model, recordId, answers));

    /// HasCustomFields::booted(): answers go with their record.
    public static Task DeleteAsync(ApplicationDbContext db, string model, long recordId) => Values(db, model, recordId).ExecuteDeleteAsync();

    /// The record's answers whose definition is printed (`placement = document`), as label + formatted answer.
    public static async Task<List<(string Label, string Value)>> PrintedAsync(ApplicationDbContext db, int companyId, string model, long recordId, string dateFormat)
    {
        var fields = await db.CustomFields.AsNoTracking()
            .Where(f => f.CompanyId == companyId && f.ModelType == model && f.Placement == Document)
            .OrderBy(f => f.Order).ThenBy(f => f.Id).ToListAsync();
        var values = await Values(db, model, recordId).AsNoTracking().ToDictionaryAsync(v => v.CustomFieldId);
        return fields.Select(f => (f.Label, Value: values.TryGetValue(f.Id, out var v) ? Formatted(v, dateFormat) : null))
            .Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => (x.Label, x.Value!)).ToList();
    }

    /// getFieldsArray(): `{SLUG}` → answer, for every answer the records hold.
    public static async Task<Dictionary<string, string?>> PlaceholdersAsync(ApplicationDbContext db, params (string Model, long Id)[] records)
    {
        var fields = new Dictionary<string, string?>();
        foreach (var (model, id) in records)
        {
            var rows = await Values(db, model, id).AsNoTracking()
                .Join(db.CustomFields, v => v.CustomFieldId, f => f.Id, (v, f) => new { v, f.Slug }).ToListAsync();
            foreach (var row in rows)
                fields["{" + row.Slug + "}"] = Raw(row.v);
        }
        return fields;
    }

    private static IQueryable<CustomFieldValue> Values(ApplicationDbContext db, string model, long recordId)
    {
        var morph = Morph(model);
        return db.CustomFieldValues.Where(v => v.CustomFieldValuableType == morph && v.CustomFieldValuableId == recordId);
    }

    private static async Task Using(AppDbFactory dbs, Func<ApplicationDbContext, Task> action)
    {
        await using var db = dbs.Create();
        await action(db);
    }
}
