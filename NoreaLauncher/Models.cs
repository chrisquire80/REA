using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Win32;

namespace NoreaLauncher;

public sealed class AnalysisRow
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class AnalysisSection
{
    public string Title { get; set; } = "";
    public List<AnalysisRow> Rows { get; set; } = [];
}

public sealed class UsageExample
{
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class UsageGuide
{
    public string Title { get; set; } = "Come si usa";
    public string Introduction { get; set; } = "";
    public List<UsageExample> Examples { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class AppEntry
{
    public string Id { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "REA";
    public string AuthorBadge { get; set; } = "BY NOREA";
    public string Version { get; set; } = "v2.6.1";
    public string Runtime { get; set; } = "Scintilla/Lexilla · .NET 10";
    public string Tagline { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconType { get; set; } = "notepad";
    public string AccentHex { get; set; } = "#60A5FA";
    public string? ProjectPath { get; set; }
    public string? BinaryPath { get; set; }
    public string? AnalysisPath { get; set; }
    public string? InstallScript { get; set; }
    public string? UninstallScript { get; set; }
    public double EstimatedSizeMb { get; set; } = 53.7;
    public string LastUpdatedText { get; set; } = "10 nov 2026";
    public bool IsPlanned { get; set; }
    public AnalysisSection? Analysis { get; set; }
    public UsageGuide? UsageGuide { get; set; }

    [JsonIgnore] public string DisplayName => Prefix + Suffix;
    [JsonIgnore] public string? InstalledExePath { get; set; }
    [JsonIgnore] public string? RollbackExePath => InstalledExePath is { } p ? p + ".prev" : null;
    [JsonIgnore] public bool HasRollback => RollbackExePath is { } r && File.Exists(r);
    [JsonIgnore] public string? DistExePath { get; set; }
    [JsonIgnore] public string? ResolvedProjectDir { get; set; }
    [JsonIgnore] public string? ResolvedAnalysisFile { get; set; }
    [JsonIgnore] public string? ResolvedInstallScript { get; set; }
    [JsonIgnore] public string? ResolvedUninstallScript { get; set; }
    [JsonIgnore] public DateTime LastUpdatedUtc { get; set; }

    [JsonIgnore] public bool IsInstalled => !string.IsNullOrEmpty(InstalledExePath) && File.Exists(InstalledExePath);
    [JsonIgnore] public bool HasDistBuild => !string.IsNullOrEmpty(DistExePath) && File.Exists(DistExePath);
    [JsonIgnore] public bool HasProject => !string.IsNullOrEmpty(ResolvedProjectDir) && Directory.Exists(ResolvedProjectDir);
    [JsonIgnore] public bool CanBuildOrInstall => HasDistBuild || HasProject;
    [JsonIgnore] public bool IsCanary => Version.Contains('-') || Version.StartsWith("v0.", StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool HasUpdate =>
        IsInstalled && HasDistBuild &&
        CatalogPolicy.HasDistUpdate(
            Version,
            File.GetLastWriteTimeUtc(DistExePath!),
            File.GetLastWriteTimeUtc(InstalledExePath!));

    [JsonIgnore] public bool IsRunning { get; set; }
    [JsonIgnore] public int? RunningPid { get; set; }
    [JsonIgnore] public string? Sha256Short { get; set; }
    [JsonIgnore] public bool IsOperating { get; set; }

    public string GetConfigAndBackupDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Id);
}

public sealed class LauncherSettings
{
    public string Language { get; set; } = "Auto";
    public string CheckUpdatesMode { get; set; } = "Automatically";
    public bool AutoInstallUpdates { get; set; }
    public bool AskPermissionOnce { get; set; }
    public bool NotifyUpdates { get; set; } = true;
    public bool IncludePreReleases { get; set; } = true;
    public const string DefaultGitHubRepo = "chrisquire80/REA";

    /// <summary>
    /// Il primo client aggiornamenti salvava questo nome, che non è l'account GitHub.
    /// api.github.com risponde 404.
    /// </summary>
    public const string RetiredGitHubRepo = "ChristianRobecchi/REA";

    public string GitHubRepo { get; set; } = DefaultGitHubRepo;
    public bool CheckCloudUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// RAM, uptime e thread delle app avviate, mostrati solo in questa sessione.
    /// Resta attivo: la scheda c'è già e i numeri non lasciano il PC.
    /// </summary>
    public bool CollectProcessTelemetry { get; set; } = true;
    public DateTime? LastCloudCheckUtc { get; set; }
    public string Appearance { get; set; } = "Dark";
    public bool HidePlannedApps { get; set; } = true;
    public string InstallFolder { get; set; } = DefaultInstallFolder;
    public string InstallFormat { get; set; } = "Portable (.exe) + Associazioni";
    public bool KeepInTray { get; set; } = true;
    public bool StartOnLogin { get; set; }
    public string? WorkspaceDir { get; set; }
    public string SortMode { get; set; } = "Name (A-Z)";
    public bool MasterDetailView { get; set; } = true;
    public Rectangle? Bounds { get; set; }
    public bool Maximized { get; set; }

    public static string DefaultInstallFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NOREA");

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NOREA");

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static LauncherSettings Load(string? settingsFilePath = null)
    {
        string path = settingsFilePath ?? Path.Combine(AppDataDir, "launcher_settings.json");
        LauncherSettings s = new();
        try
        {
            if (File.Exists(path))
                s = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path))
                    ?? throw new JsonException("Il file delle impostazioni è vuoto o non contiene un oggetto valido.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Impossibile caricare le impostazioni da {path}; verranno usati i valori predefiniti.", ex);
            PreserveCorruptFile(path);
        }

        if (string.IsNullOrWhiteSpace(s.InstallFolder))
            s.InstallFolder = DefaultInstallFolder;

        if (string.IsNullOrWhiteSpace(s.WorkspaceDir) || !Directory.Exists(s.WorkspaceDir))
            s.WorkspaceDir = DetectWorkspaceDir();

        string repo = NormalizeGitHubRepo(s.GitHubRepo);
        if (!string.Equals(repo, s.GitHubRepo, StringComparison.Ordinal))
        {
            s.GitHubRepo = repo;
            if (File.Exists(path))
                s.Save(path);
        }

        return s;
    }

    public static bool IsRetiredGitHubRepo(string? repo) =>
        string.Equals(repo?.Trim(), RetiredGitHubRepo, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeGitHubRepo(string? repo)
    {
        if (string.IsNullOrWhiteSpace(repo) || IsRetiredGitHubRepo(repo))
            return DefaultGitHubRepo;
        return repo.Trim();
    }

    public void Save(string? settingsFilePath = null)
    {
        string path = settingsFilePath ?? Path.Combine(AppDataDir, "launcher_settings.json");
        string? temp = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            temp = path + $".{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOpts));
            ReplaceFile(temp, path);
        }
        catch (Exception ex) { Log.Warn($"Impossibile salvare le impostazioni in {path}.", ex); }
        finally { DeleteTemporaryFile(temp); }
    }

    public void ApplyStartupRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;
            if (StartOnLogin)
                key.SetValue("NoreaLauncher", $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue("NoreaLauncher", throwOnMissingValue: false);
        }
        catch { }
    }

    public static string DetectWorkspaceDir()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "norea_catalog.json")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "NotepadREA")))
                    return dir.FullName;
            }
        }
        return AppContext.BaseDirectory;
    }

    internal static void PreserveCorruptFile(string path)
    {
        if (!File.Exists(path)) return;
        string backup = path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        try { File.Copy(path, backup); }
        catch (Exception ex) { Log.Warn($"Impossibile preservare il file non valido {path}.", ex); }
    }

    internal static void ReplaceFile(string temp, string destination)
    {
        if (!File.Exists(destination))
        {
            File.Move(temp, destination);
            return;
        }

        try { File.Replace(temp, destination, null); }
        catch (PlatformNotSupportedException) { File.Move(temp, destination, overwrite: true); }
    }

    internal static void DeleteTemporaryFile(string? path)
    {
        if (path is null || !File.Exists(path)) return;
        try { File.Delete(path); }
        catch (Exception ex) { Log.Warn($"Impossibile eliminare il file temporaneo {path}.", ex); }
    }
}
