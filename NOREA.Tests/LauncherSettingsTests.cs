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
            Assert.True(loaded.CollectProcessTelemetry);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Settings_Load_MissingTelemetryFlag_StaysEnabledAndExplicitFalseIsKept()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-settings-test-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "launcher_settings.json");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(path, """{"Appearance":"Dark"}""");
            Assert.True(LauncherSettings.Load(path).CollectProcessTelemetry);

            File.WriteAllText(path, """{"CollectProcessTelemetry":false}""");
            Assert.False(LauncherSettings.Load(path).CollectProcessTelemetry);
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

    [Fact]
    public void Catalog_Load_ExcludesBenchmarkAndToolProjects()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-benchmark-filter-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            string benchDir = Path.Combine(root, "NOREA.Benchmarks");
            Directory.CreateDirectory(benchDir);
            File.WriteAllText(Path.Combine(benchDir, "NOREA.Benchmarks.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");

            string testsDir = Path.Combine(root, "NOREA.Tests");
            Directory.CreateDirectory(testsDir);
            File.WriteAllText(Path.Combine(testsDir, "NOREA.Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");

            string coreDir = Path.Combine(root, "WinSCPREA.Core");
            Directory.CreateDirectory(coreDir);
            File.WriteAllText(Path.Combine(coreDir, "WinSCPREA.Core.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");

            string libDir = Path.Combine(root, "SharedHelpers");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "SharedHelpers.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");

            string appDir = Path.Combine(root, "NotepadREA");
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "NotepadREA.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><Version>2.2.0</Version></PropertyGroup></Project>");

            var settings = new LauncherSettings { WorkspaceDir = root };
            var apps = CatalogService.LoadApps(settings);

            Assert.DoesNotContain(apps, a => a.Id.Contains("Benchmark", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => a.Id.Contains("Test", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => a.Id.Contains(".Core", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(apps, a => string.Equals(a.Id, "SharedHelpers", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(apps, a => string.Equals(a.Id, "NotepadREA", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Catalog_Load_UsesDirectoryBuildPropsWhenCsprojHasNoVersion()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-suite-version-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <NoreaSuiteVersion>9.8.7</NoreaSuiteVersion>
                    <Version>$(NoreaSuiteVersion)</Version>
                  </PropertyGroup>
                </Project>
                """);

            string suiteDir = Path.Combine(root, "SampleREA");
            Directory.CreateDirectory(suiteDir);
            File.WriteAllText(Path.Combine(suiteDir, "SampleREA.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>WinExe</OutputType>
                  </PropertyGroup>
                </Project>
                """);

            string productDir = Path.Combine(root, "WidgetREA");
            Directory.CreateDirectory(productDir);
            File.WriteAllText(Path.Combine(productDir, "WidgetREA.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>WinExe</OutputType>
                    <Version>1.2.3</Version>
                  </PropertyGroup>
                </Project>
                """);

            var apps = CatalogService.LoadApps(new LauncherSettings { WorkspaceDir = root });

            Assert.Equal("v9.8.7", Assert.Single(apps, a => a.Id == "SampleREA").Version);
            Assert.Equal("v1.2.3", Assert.Single(apps, a => a.Id == "WidgetREA").Version);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
