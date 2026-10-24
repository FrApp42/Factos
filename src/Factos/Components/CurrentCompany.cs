using System.Security.Claims;
using Factos.Api;
using Factos.Data;
using Factos.Data.Entities;
using Factos.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Factos.Components;

/// The signed-in user and the company the UI acts on, for one Blazor circuit. The SPA
/// upstream keeps the selected company client side; here it is remembered in the
/// user's settings so it survives reloads.
public class CurrentCompany(AppDbFactory dbs, AuthenticationStateProvider auth)
{
    private const string SelectedKey = "factos_selected_company";

    public int UserId { get; private set; }
    public string UserName { get; private set; } = "";
    public Company Company { get; private set; } = null!;
    public Currency? Currency { get; private set; }
    public List<Company> Companies { get; private set; } = [];
    public Dictionary<string, string> Settings { get; private set; } = [];
    public bool Loaded { get; private set; }

    public int Id => Company.Id;
    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (Loaded)
            return;
        var state = await auth.GetAuthenticationStateAsync();
        UserId = int.Parse(state.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var db = dbs.Create();
        var user = await db.Users.SingleAsync(u => u.Id == UserId);
        UserName = user.Name;
        var ids = await db.UserCompanies.Where(uc => uc.UserId == UserId && uc.CompanyId != null)
            .OrderBy(uc => uc.Id).Select(uc => uc.CompanyId!.Value).ToListAsync();
        Companies = await db.Companies.Where(c => ids.Contains(c.Id)).OrderBy(c => c.Name).ToListAsync();
        if (Companies.Count == 0)
            throw new InvalidOperationException(Loc.T("Ce compte n'appartient à aucune société."));

        var saved = await db.UserSettings.Where(s => s.UserId == UserId && s.Key == SelectedKey).Select(s => s.Value).FirstOrDefaultAsync();
        var selected = Companies.FirstOrDefault(c => c.Id.ToString() == saved)
            ?? Companies.First(c => c.Id == ids[0]);
        await LoadCompanyAsync(db, selected.Id);
        Loaded = true;
    }

    public async Task SwitchAsync(int companyId)
    {
        if (Companies.All(c => c.Id != companyId))
            return;
        await RememberSelectionAsync(companyId);
        await using var db = dbs.Create();
        await LoadCompanyAsync(db, companyId);
        Changed?.Invoke();
    }

    /// Stores the company to open on the next load; membership is checked on load.
    public async Task RememberSelectionAsync(int companyId)
    {
        await using var db = dbs.Create();
        var row = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == UserId && s.Key == SelectedKey);
        if (row is null)
            db.UserSettings.Add(row = new UserSetting { UserId = UserId, Key = SelectedKey, CreatedAt = Data.Timestamp.Now() });
        row.Value = companyId.ToString();
        row.UpdatedAt = Data.Timestamp.Now();
        await db.SaveChangesAsync();
    }

    /// Re-reads the company and its settings after they were edited.
    public async Task RefreshAsync()
    {
        await using var db = dbs.Create();
        Companies = await db.Companies.Where(c => Companies.Select(x => x.Id).Contains(c.Id)).OrderBy(c => c.Name).ToListAsync();
        await LoadCompanyAsync(db, Company.Id);
        Changed?.Invoke();
    }

    private async Task LoadCompanyAsync(ApplicationDbContext db, int companyId)
    {
        Company = await db.Companies.SingleAsync(c => c.Id == companyId);
        Settings = await Domain.Companies.SettingsAsync(db, companyId);
        Currency = int.TryParse(Settings.GetValueOrDefault("currency"), out var currencyId)
            ? await db.Currencies.FindAsync(currencyId)
            : null;
    }

    public string Setting(string key, string fallback = "") => Settings.GetValueOrDefault(key, fallback);

    public string Money(long cents, Currency? currency = null) => Domain.Money.Format(cents, currency ?? Currency);

    public string Date(DateTime? value) => value is { } v ? PhpDate.Format(v, Setting("carbon_date_format", "Y/m/d")) : "";

    public string Date(DateOnly? value) => Date(value?.ToDateTime(TimeOnly.MinValue));
}
