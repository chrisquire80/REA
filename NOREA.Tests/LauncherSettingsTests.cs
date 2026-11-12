using NoreaLauncher;

namespace NOREA.Tests;

public class LauncherSettingsTests
{
    [Fact]
    public void Settings_SaveAndLoad_UsesExplicitPath()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-settings-test-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "launcher_settings.json");
        try
        {
            var settings = new LauncherSettings
            {
                Appearance = "Light",
                GitHubRepo = "owner/project",
                WorkspaceDir = root
            };
            settings.Save(path);

            var loaded = LauncherSettings.Load(path);

            Assert.Equal("Light", loaded.Appearance);
            Assert.Equal("owner/project", loaded.GitHubRepo);
            Assert.Equal(root, loaded.WorkspaceDir);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Settings_Load_PreservesMalformedFileBeforeUsingDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-settings-test-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "launcher_settings.json");
        const string invalidJson = "{ invalid";
        Directory.CreateDirectory(root);
        File.WriteAllText(path, invalidJson);
        try
        {
            var settings = LauncherSettings.Load(path);

            Assert.NotNull(settings);
            Assert.Equal(invalidJson, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(root, "launcher_settings.json.corrupt-*"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Catalog_Load_PreservesMalformedFileBeforeUsingDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-catalog-test-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "norea_catalog.json");
        const string invalidJson = "[ invalid";
        Directory.CreateDirectory(root);
        File.WriteAllText(path, invalidJson);
        try
        {
            var settings = new LauncherSettings { WorkspaceDir = root };

            var apps = CatalogService.LoadApps(settings);

            Assert.NotEmpty(apps);
            Assert.Equal(invalidJson, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(root, "norea_catalog.json.corrupt-*"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
