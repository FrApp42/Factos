using Factos.Domain;

namespace Factos.Tests;

public class AppUpdateTests
{
    [Fact]
    public void Tags_parse_to_comparable_versions()
    {
        Assert.Equal(new Version(1, 2, 3), AppUpdates.Parse("v1.2.3"));
        Assert.Equal(new Version(1, 2, 3), AppUpdates.Parse("1.2.3-beta+abc"));
        Assert.Null(AppUpdates.Parse("main"));
        Assert.True(AppUpdates.Parse("v0.10.0") > AppUpdates.Parse("v0.9.1"));
    }

    [Fact]
    public void Apply_replaces_files_moves_old_ones_aside_and_keeps_local_settings()
    {
        var root = Directory.CreateTempSubdirectory("factos-update-test").FullName;
        try
        {
            var from = Directory.CreateDirectory(Path.Combine(root, "new")).FullName;
            var to = Directory.CreateDirectory(Path.Combine(root, "app")).FullName;
            File.WriteAllText(Path.Combine(from, "Factos.dll"), "new");
            File.WriteAllText(Path.Combine(from, "appsettings.local.json"), "shipped by mistake");
            Directory.CreateDirectory(Path.Combine(from, "wwwroot"));
            File.WriteAllText(Path.Combine(from, "wwwroot", "app.css"), "css");
            File.WriteAllText(Path.Combine(to, "Factos.dll"), "old");
            File.WriteAllText(Path.Combine(to, "appsettings.local.json"), "mine");

            AppUpdates.Apply(from, to);

            Assert.Equal("new", File.ReadAllText(Path.Combine(to, "Factos.dll")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(to, "Factos.dll.old-update")));
            Assert.Equal("css", File.ReadAllText(Path.Combine(to, "wwwroot", "app.css")));
            Assert.Equal("mine", File.ReadAllText(Path.Combine(to, "appsettings.local.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
