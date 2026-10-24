using System.Globalization;
using System.Text.Json;

namespace Factos.Components;

/// <summary>
/// UI translations. The French text is the key; English comes from Locales/en.json,
/// a missing entry falls back to the key. The language is the request's UI culture
/// (cookie, see Program.cs), so this works in components and in domain code alike.
/// </summary>
public static class Loc
{
    public static readonly string[] Languages = ["fr", "en"];

    // French unless a request (cookie) says otherwise: background work, the API and tests
    // must not follow the machine's UI language.
    static Loc() => CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr");

    private static readonly Lazy<Dictionary<string, string>> En = new(() =>
    {
        using var s = typeof(Loc).Assembly.GetManifestResourceStream("Factos.Components.Locales.en.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(s)!;
    });

    public static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "fr";

    public static string T(string key) => Lang == "en" && En.Value.TryGetValue(key, out var v) ? v : key;

    /// <summary>Translated template with <c>{0}</c>-style placeholders.</summary>
    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}

// DataAnnotations messages must be attribute constants, so they are translated when shown.
public sealed class LocRequiredAttribute : System.ComponentModel.DataAnnotations.RequiredAttribute
{
    public override string FormatErrorMessage(string name) => Loc.T(ErrorMessage!);
}

public sealed class LocMinLengthAttribute(int length) : System.ComponentModel.DataAnnotations.MinLengthAttribute(length)
{
    public override string FormatErrorMessage(string name) => Loc.T(ErrorMessage!);
}

public sealed class LocEmailAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
{
    public override bool IsValid(object? value) => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
    public override string FormatErrorMessage(string name) => Loc.T(ErrorMessage!);
}

public sealed class LocCompareAttribute(string other) : System.ComponentModel.DataAnnotations.CompareAttribute(other)
{
    public override string FormatErrorMessage(string name) => Loc.T(ErrorMessage!);
}
