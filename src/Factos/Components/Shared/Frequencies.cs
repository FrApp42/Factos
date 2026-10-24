namespace Factos.Components.Shared;

/// The frequency presets upstream's form offers (use-frequency-presets.ts), as cron
/// expressions; anything else is a custom expression.
public static class Frequencies
{
    public const string Custom = "CUSTOM";

    public static readonly (string Cron, string Label)[] Presets =
    [
        ("0 0 * * 0", "Chaque semaine"),
        ("0 0 */14 * *", "Toutes les 2 semaines"),
        ("0 0 1 * *", "Chaque mois"),
        ("0 0 1 */2 *", "Tous les 2 mois"),
        ("0 0 1 */3 *", "Chaque trimestre"),
        ("0 0 1 */6 *", "Tous les 6 mois"),
        ("0 0 1 1 *", "Chaque année"),
        ("0 0 * * *", "Chaque jour"),
        ("0 5 */15 * *", "Tous les 15 jours"),
        ("0 * * * *", "Chaque heure"),
        ("* * * * *", "Chaque minute"),
    ];

    public static string Label(string cron)
        => Presets.FirstOrDefault(p => p.Cron == cron.Trim()).Label is { } l ? Loc.T(l) : Loc.F("Personnalisée ({0})", cron);
}
