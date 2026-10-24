using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Factos.Domain;

/// Application updates from the GitHub releases of `Updates:Repository` (owner/name): the
/// release workflow attaches `factos.zip` (portable `dotnet publish`) to each `v*` tag.
/// Installing overwrites the application folder, then stops the process for the host to
/// restart it (as the /setup wizard does); pending migrations then go through /update.
public sealed class AppUpdates(IConfiguration config, IHostApplicationLifetime lifetime, ILogger<AppUpdates> log)
{
    public const string Asset = "factos.zip";
    private const string OldSuffix = ".old-update";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Factos" }, { "Accept", "application/vnd.github+json" } },
        Timeout = TimeSpan.FromMinutes(5),
    };

    public static Version Current { get; } = Parse(typeof(AppUpdates).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion) ?? new Version(0, 0, 0);

    private string Repository => config["Updates:Repository"] ?? "AnthoDingo/Factos";

    public sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("published_at")] DateTime? PublishedAt,
        [property: JsonPropertyName("assets")] List<ReleaseAsset> Assets)
    {
        public Version? Version => Parse(TagName);
        public string? DownloadUrl => Assets.FirstOrDefault(a => a.Name == Asset)?.BrowserDownloadUrl;
    }

    public sealed record ReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);

    /// "v1.2.3", "1.2.3-beta+sha" → 1.2.3.
    public static Version? Parse(string? tag) =>
        Version.TryParse(tag?.TrimStart('v', 'V').Split('-', '+')[0], out var v) ? v : null;

    /// The latest published (non-draft, non-prerelease) release, or null when there is none.
    public async Task<Release?> LatestAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Release>(ct);
    }

    /// Downloads and unpacks the release over the application folder, then stops the process.
    public async Task InstallAsync(Release release, CancellationToken ct = default)
    {
        var url = release.DownloadUrl ?? throw new InvalidOperationException($"La version {release.TagName} ne contient pas {Asset}.");
        var staging = Path.Combine(Path.GetTempPath(), "factos-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var zip = staging + ".zip";
            await using (var source = await Http.GetStreamAsync(url, ct))
            await using (var file = File.Create(zip))
                await source.CopyToAsync(file, ct);
            ZipFile.ExtractToDirectory(zip, staging); // rejects entries escaping the folder
            File.Delete(zip);
            if (!File.Exists(Path.Combine(staging, "Factos.dll")))
                throw new InvalidOperationException($"{Asset} ne contient pas Factos.dll.");
            Apply(staging, AppContext.BaseDirectory);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
        log.LogWarning("Factos {Version} installé, arrêt du processus pour redémarrage.", release.TagName);
        lifetime.StopApplication();
    }

    /// Loaded assemblies are locked on Windows but can be renamed: each replaced file is moved
    /// aside, then the new one written in its place. Leftovers are removed by CleanUp next start.
    internal static void Apply(string from, string to)
    {
        foreach (var source in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, source);
            // Machine settings, written by /setup, never shipped but never overwritten either.
            if (relative.Equals("appsettings.local.json", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
                File.Move(target, target + OldSuffix, overwrite: true);
            File.Copy(source, target);
        }
    }

    /// At startup: files replaced by the previous update, no longer loaded.
    public static void CleanUp()
    {
        foreach (var old in Directory.EnumerateFiles(AppContext.BaseDirectory, "*" + OldSuffix, SearchOption.AllDirectories))
        {
            try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
