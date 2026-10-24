using Factos.Data;
using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// The UI's colour themes (wwwroot/app.css, `<html data-theme>`). Dark or light follows the
/// system preference in every theme. Not part of upstream: the keys are Factos' own.
public static class Themes
{
    public static readonly (string Key, string Label, string Swatch)[] All =
    [
        ("blue", "Bleu", "#2563eb"),
        ("teal", "Sarcelle", "#0f766e"),
        ("emerald", "Vert émeraude", "#047857"),
        ("graphite", "Graphite + bleu", "#27272a"),
    ];

    public const string Fallback = "blue";

    /// User setting; absent means the user follows the default theme.
    public const string UserKey = "factos_theme";

    /// Global `settings` options, set by a super admin.
    public const string DefaultOption = "factos_default_theme", LoginOption = "factos_login_theme";

    public static string Valid(string? key) => All.Any(t => t.Key == key) ? key! : Fallback;

    /// The theme for a page: the user's own, else the default; signed-out pages get the login theme.
    public static async Task<string> ForUserAsync(ApplicationDbContext db, int? userId)
    {
        if (userId is { } id
            && await db.UserSettings.Where(s => s.UserId == id && s.Key == UserKey).Select(s => s.Value).FirstOrDefaultAsync() is { } own)
            return Valid(own);
        return Valid(await GlobalAsync(db, userId is null ? LoginOption : DefaultOption));
    }

    public static Task<string?> GlobalAsync(ApplicationDbContext db, string option)
        => db.Settings.Where(s => s.Option == option).Select(s => s.Value).FirstOrDefaultAsync();

    public static async Task SetGlobalAsync(ApplicationDbContext db, string option, string theme)
    {
        var now = Timestamp.Now();
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Option == option);
        if (row is null)
            db.Settings.Add(row = new Setting { Option = option, CreatedAt = now });
        row.Value = Valid(theme);
        row.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    /// Saves the user's choice; null or empty goes back to following the default.
    public static async Task SetUserAsync(ApplicationDbContext db, int userId, string? theme)
    {
        var row = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == UserKey);
        if (string.IsNullOrEmpty(theme))
        {
            if (row is not null)
                db.UserSettings.Remove(row);
        }
        else
        {
            var now = Timestamp.Now();
            if (row is null)
                db.UserSettings.Add(row = new UserSetting { UserId = userId, Key = UserKey, CreatedAt = now });
            row.Value = Valid(theme);
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
    }
}
