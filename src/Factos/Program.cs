using System.Globalization;
using Factos.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AnthoDingo.Setup;
using AnthoDingo.Update;
using Microsoft.Extensions.Configuration.Json;
using Factos.Api;
using Factos.Api.Auth;
using Factos.Api.Resources;
using Factos.Components;
using Factos.Components.Account;
using Factos.Data;
using Factos.Domain;

// Taken out of args: the configuration would read the connection string's `key=value` pairs.
string[]? importArgs = args is ["import-invoiceshelf", var p, var c, ..] ? [p, c] : null;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr");
var builder = WebApplication.CreateBuilder(importArgs is null ? args : args[3..]);

// Written by the /setup wizard (AnthoDingo.Setup): Setup:IsComplete, Setup:Provider and
// ConnectionStrings:DefaultConnection. Machine-specific, never committed. Ranked right after
// appsettings.{Environment}.json: user secrets, environment variables, the command line and
// test hosts still override it. Setup:LocalConfigFile moves it (Docker: into the App_Data volume).
var localConfig = builder.Configuration["Setup:LocalConfigFile"] ?? "appsettings.local.json";
var sources = builder.Configuration.Sources;
sources.Insert(sources.ToList().FindLastIndex(s => s is JsonConfigurationSource { Path: { } p }
        && p.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)) + 1,
    new JsonConfigurationSource { Path = localConfig, Optional = true, FileProvider = builder.Environment.ContentRootFileProvider });

// Not installed yet: the app is only the wizard. It writes the file above then stops the
// process, which the host (service manager, container, `dotnet run` again) restarts installed.
if (!builder.Configuration.GetValue<bool>("Setup:IsComplete"))
{
    builder.Services.AddFileBasedSetup<Installer>(o =>
    {
        o.LocalConfigFileName = localConfig;
        DatabaseProviderExtensions.AddSetupDrivers(o);
    });
    builder.Services.AddSetupStep<CompanySetupStep>();
    var wizard = builder.Build();
    wizard.UseSetupMiddleware("Factos");
    wizard.Run();
    return;
}

// One-off migration from InvoiceShelf, into the installed (and still empty) database:
// dotnet Factos.dll import-invoiceshelf <Sqlite|Postgres|MySql> "<connection string>"
if (importArgs is [var sourceProvider, var sourceConnection])
{
    await using var target = DatabaseProviderExtensions.CreateContext(
        builder.Configuration.GetValue<DbProvider?>("Setup:Provider") ?? DbProvider.Sqlite,
        builder.Configuration.GetConnectionString("DefaultConnection")!);
    try
    {
        foreach (var line in await InvoiceShelfImport.RunAsync(target, Enum.Parse<DbProvider>(sourceProvider, ignoreCase: true), sourceConnection))
            Console.WriteLine(line);
    }
    catch (InvalidOperationException e)
    {
        Console.Error.WriteLine(e.Message);
        Environment.ExitCode = 1;
    }
    return;
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// InvoiceShelf's API is snake_case and returns Laravel's error envelope.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new LaravelDateTimeConverter());
    })
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory = ctx => LaravelResponse.ValidationError(ctx.ModelState));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// API clients authenticate with Sanctum-style bearer tokens; the Blazor UI uses the cookie.
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, SanctumTokenHandler>(SanctumTokenDefaults.Scheme, null);

builder.Services.AddFactosDatabase(builder.Configuration);
builder.Services.AddScoped<CompanyContext>();
builder.Services.AddScoped<CompanyScopeFilter>();
builder.Services.AddScoped<AccountResources>();
builder.Services.AddScoped<Factos.Api.Authorization.Bouncer>();
builder.Services.AddFactosRateLimits();
builder.Services.AddScoped<CurrentCompany>();
// Stateless over AppDbFactory: one instance serves the UI, the API and the worker.
builder.Services.AddSingleton<Sales>();
builder.Services.AddSingleton<CreditNotes>();
builder.Services.AddSingleton<Purchases>();
builder.Services.AddSingleton<Recurring>();
builder.Services.AddSingleton<Logos>();
builder.Services.AddSingleton<ExpenseFiles>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Mailer>();
builder.Services.AddSingleton<PasswordResets>();
// Reset links last 60 minutes, as upstream's password broker (`auth.passwords.*.expire`).
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromMinutes(60));
builder.Services.AddSingleton<DocumentMailer>();
builder.Services.AddSingleton<AppUpdates>();

// Keys survive restarts and redeployments: they protect the auth cookies and the stored
// SMTP passwords, which become unreadable if the keys are lost.
builder.Services.AddDataProtection()
    .SetApplicationName("Factos")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath,
        builder.Configuration["DataProtection:KeysPath"] ?? "Data/keys")));

// QuestPDF's Community license: free below 1M USD annual revenue (see questpdf.com/license).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
if (builder.Configuration.GetValue("Recurring:Enabled", true))
    builder.Services.AddHostedService<RecurringInvoiceWorker>();
// Migrations are applied by an admin on /update (AnthoDingo.Update), never at startup.
builder.Services.AddDatabaseUpdateWith<FactosMigrations>(o =>
{
    o.ProductName = "Factos";
    o.RequireAuthentication = true;
    o.AuthorizationPolicy = Installer.SuperAdmin;
    o.SignInPath = "/Account/Login";
    // The static sign-in form posts back to /Account/Login, sign-out to /Account/Logout.
    o.ExemptPathPrefixes.Add("/Account");
    o.ExemptPathPrefixes.Add("/language");
});
builder.Services.AddAuthorization(o => o.AddPolicy(Installer.SuperAdmin, p => p.RequireRole(Installer.SuperAdmin)));

// Before AddIdentityCore, which only adds its own hasher when none is registered.
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, LaravelPasswordHasher>();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        // InvoiceShelf's only password rule is `min:8`.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<FactosClaimsFactory>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();
AppUpdates.CleanUp();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseMigrationsGate();

// UI language only (fr default, en): number/date formatting keeps the server culture.
app.UseRequestLocalization(o =>
{
    o.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("fr"));
    o.SupportedCultures = [CultureInfo.CurrentCulture];
    o.SupportedUICultures = Loc.Languages.Select(CultureInfo.GetCultureInfo).ToList();
    o.RequestCultureProviders = [new Microsoft.AspNetCore.Localization.CustomRequestCultureProvider(ctx =>
        Task.FromResult<Microsoft.AspNetCore.Localization.ProviderCultureResult?>(
            ctx.Request.Cookies["factos_lang"] is { } l && Loc.Languages.Contains(l)
                ? new(CultureInfo.CurrentCulture.Name, l) : null))];
});
app.MapGet("/language/{lang}", (string lang, HttpContext ctx) =>
{
    if (Loc.Languages.Contains(lang))
        ctx.Response.Cookies.Append("factos_lang", lang, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
    var back = ctx.Request.Headers.Referer.ToString();
    return Results.Redirect(Uri.TryCreate(back, UriKind.Absolute, out var u) && u.Host == ctx.Request.Host.Host ? u.PathAndQuery : "/");
});

app.UseAntiforgery();
app.UseRateLimiter();

app.MapStaticAssets();
app.MapControllers();
app.MapDocumentEndpoints();
// Unknown API routes answer like Laravel, not with the Blazor not-found page.
app.MapFallback("/api/{**path}", ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    return ctx.Response.WriteAsJsonAsync(new { message = $"The route {ctx.Request.Path.Value!.TrimStart('/')} could not be found." });
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(UpdateRoutes).Assembly);
app.MapAdditionalIdentityEndpoints();

await DatabaseInitializer.SeedIfCurrentAsync(app.Services);

app.Run();
