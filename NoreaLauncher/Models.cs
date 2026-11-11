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

public sealed class AppEntry
{
    public string Id { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "REA";
    public string AuthorBadge { get; set; } = "BY NOREA";
    public string Version { get; set; } = "v2.0.0";
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
        (LauncherUtil.CompareVersions(Version, "v0.0.0") >= 0 &&
         File.GetLastWriteTimeUtc(DistExePath!) > File.GetLastWriteTimeUtc(InstalledExePath!).AddSeconds(5));

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

    static string SettingsFile => Path.Combine(AppDataDir, "launcher_settings.json");
    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static LauncherSettings Load()
    {
        LauncherSettings s = new();
        try
        {
            if (File.Exists(SettingsFile))
                s = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsFile)) ?? new();
        }
        catch { }

        if (string.IsNullOrWhiteSpace(s.InstallFolder))
            s.InstallFolder = DefaultInstallFolder;

        if (string.IsNullOrWhiteSpace(s.WorkspaceDir) || !Directory.Exists(s.WorkspaceDir))
            s.WorkspaceDir = DetectWorkspaceDir();

        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var tmp = SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
            File.Move(tmp, SettingsFile, overwrite: true);
        }
        catch { }
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
}

public static class CatalogService
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string GetCatalogPath(LauncherSettings settings)
    {
        var ws = settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
        var wsCatalog = Path.Combine(ws, "norea_catalog.json");
        if (File.Exists(wsCatalog) || Directory.Exists(ws)) return wsCatalog;
        Directory.CreateDirectory(LauncherSettings.AppDataDir);
        return Path.Combine(LauncherSettings.AppDataDir, "norea_catalog.json");
    }

    public static List<AppEntry> LoadApps(LauncherSettings settings)
    {
        var ws = settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
        var catalogPath = GetCatalogPath(settings);
        List<AppEntry> list = [];

        if (File.Exists(catalogPath))
        {
            try
            {
                list = JsonSerializer.Deserialize<List<AppEntry>>(File.ReadAllText(catalogPath), JsonOpts) ?? [];
            }
            catch { }
        }

        if (list.Count == 0)
        {
            list = GetDefaultCatalog();
            SaveCatalog(settings, list);
        }

        if (Directory.Exists(ws))
            DiscoverWorkspaceProjects(ws, list, settings);

        foreach (var app in list)
            ResolveAppState(app, ws, settings);

        list.RemoveAll(a => a.IsPlanned || (!a.IsInstalled && !a.HasDistBuild && !a.HasProject));
        RefreshRunningProcesses(list);

        return list;
    }

    public static bool RefreshRunningProcesses(IEnumerable<AppEntry> apps)
    {
        bool changed = false;
        foreach (var app in apps)
        {
            bool running = false;
            int? pid = null;
            try
            {
                var procs = Process.GetProcessesByName(app.Id);
                foreach (var p in procs)
                {
                    try
                    {
                        string? fn = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(fn) &&
                            (string.Equals(fn, app.InstalledExePath, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(fn, app.DistExePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            running = true;
                            pid = p.Id;
                            break;
                        }
                    }
                    catch
                    {
                        // Se non possiamo accedere a MainModule (permessi), consideriamo comunque il processo
                        running = true;
                        pid = p.Id;
                        break;
                    }
                }
                foreach (var p in procs) p.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warn($"Errore verifica stato processi per {app.Id}", ex);
            }

            if (app.IsRunning != running || app.RunningPid != pid)
            {
                app.IsRunning = running;
                app.RunningPid = pid;
                changed = true;
            }
        }
        return changed;
    }

    public static string ComputeFileSha256(string path) => LauncherUtil.ComputeFileSha256(path);

    public static void SaveCatalog(LauncherSettings settings, List<AppEntry> apps)
    {
        try
        {
            var path = GetCatalogPath(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(apps, JsonOpts));
        }
        catch { }
    }

    static void DiscoverWorkspaceProjects(string ws, List<AppEntry> list, LauncherSettings settings)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(ws))
            {
                var folderName = Path.GetFileName(dir);
                if (string.Equals(folderName, "NoreaLauncher", StringComparison.OrdinalIgnoreCase) ||
                    folderName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                    folderName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)) continue;
                var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
                if (csproj is null) continue;

                var id = Path.GetFileNameWithoutExtension(csproj);
                if (string.Equals(id, "NoreaLauncher", StringComparison.OrdinalIgnoreCase) ||
                    id.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                    id.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)) continue;

                var existing = list.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    var (prefix, suffix) = SplitReaName(id);
                    existing = new AppEntry
                    {
                        Id = id,
                        Prefix = prefix,
                        Suffix = suffix,
                        AuthorBadge = "BY NOREA",
                        Version = ReadCsprojVersion(csproj) ?? "V1.0.0",
                        Runtime = ".NET 10 WinForms",
                        Tagline = $"Applicazione {id} della suite NOREA",
                        Description = $"Progetto C#/.NET rilevato automaticamente nella cartella {folderName}.",
                        IconType = GuessIconType(id),
                        ProjectPath = folderName,
                        BinaryPath = Path.Combine("dist", id + ".exe"),
                        IsPlanned = false,
                    };
                    list.Insert(0, existing);
                }
                else
                {
                    existing.ProjectPath ??= folderName;
                    if (string.IsNullOrEmpty(existing.BinaryPath))
                        existing.BinaryPath = Path.Combine("dist", id + ".exe");
                    var ver = ReadCsprojVersion(csproj);
                    if (ver != null) existing.Version = ver;
                    existing.IsPlanned = false;
                }
            }

            var distDir = Path.Combine(ws, "dist");
            if (Directory.Exists(distDir))
            {
                foreach (var exe in Directory.GetFiles(distDir, "*.exe"))
                {
                    var id = Path.GetFileNameWithoutExtension(exe);
                    if (string.Equals(id, "NoreaLauncher", StringComparison.OrdinalIgnoreCase)) continue;
                    var existing = list.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (existing is null)
                    {
                        var (prefix, suffix) = SplitReaName(id);
                        list.Insert(0, new AppEntry
                        {
                            Id = id,
                            Prefix = prefix,
                            Suffix = suffix,
                            AuthorBadge = "BY NOREA",
                            Version = "V1.0.0",
                            Runtime = "Win64 Native / .NET",
                            Tagline = $"Eseguibile autonomo {id}",
                            Description = $"Binario rilevato in dist\\{Path.GetFileName(exe)}.",
                            IconType = GuessIconType(id),
                            BinaryPath = Path.Combine("dist", Path.GetFileName(exe)),
                            IsPlanned = false,
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Errore scansione cartelle workspace", ex);
        }

        // Le app scoperte dinamicamente restano nella sessione in memoria senza sovrascrivere norea_catalog.json
    }

    static void ResolveAppState(AppEntry app, string ws, LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(app.ProjectPath))
        {
            var p = Path.IsPathRooted(app.ProjectPath) ? app.ProjectPath : Path.Combine(ws, app.ProjectPath);
            if (Directory.Exists(p)) app.ResolvedProjectDir = p;
        }

        if (!string.IsNullOrWhiteSpace(app.AnalysisPath))
        {
            var a = Path.IsPathRooted(app.AnalysisPath) ? app.AnalysisPath : Path.Combine(ws, app.AnalysisPath);
            if (File.Exists(a)) app.ResolvedAnalysisFile = a;
        }

        if (!string.IsNullOrWhiteSpace(app.InstallScript))
        {
            var s = Path.IsPathRooted(app.InstallScript) ? app.InstallScript : Path.Combine(ws, app.InstallScript);
            if (File.Exists(s)) app.ResolvedInstallScript = s;
        }

        if (!string.IsNullOrWhiteSpace(app.UninstallScript))
        {
            var u = Path.IsPathRooted(app.UninstallScript) ? app.UninstallScript : Path.Combine(ws, app.UninstallScript);
            if (File.Exists(u)) app.ResolvedUninstallScript = u;
        }

        string? distExe = null;
        if (!string.IsNullOrWhiteSpace(app.BinaryPath))
        {
            var cand = Path.IsPathRooted(app.BinaryPath) ? app.BinaryPath : Path.Combine(ws, app.BinaryPath);
            if (File.Exists(cand)) distExe = cand;
        }
        if (distExe is null && app.ResolvedProjectDir != null)
        {
            var relDir = Path.Combine(app.ResolvedProjectDir, "bin", "Release");
            if (Directory.Exists(relDir))
                distExe = Directory.GetFiles(relDir, app.Id + ".exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        if (distExe is null)
        {
            var beside = Path.Combine(AppContext.BaseDirectory, app.Id + ".exe");
            if (File.Exists(beside)) distExe = beside;
        }
        app.DistExePath = distExe;

        var localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        string[] candidates =
        [
            Path.Combine(localPrograms, app.Id, app.Id + ".exe"),
            Path.Combine(settings.InstallFolder, app.Id, app.Id + ".exe"),
            Path.Combine(settings.InstallFolder, app.Id + ".exe"),
            Path.Combine(AppContext.BaseDirectory, app.Id + ".exe"),
            Path.Combine(AppContext.BaseDirectory, "dist", app.Id + ".exe"),
        ];
        app.InstalledExePath = candidates.FirstOrDefault(File.Exists);

        var refFile = app.InstalledExePath ?? app.DistExePath;
        if (refFile != null && File.Exists(refFile))
        {
            var fi = new FileInfo(refFile);
            app.EstimatedSizeMb = Math.Round(fi.Length / (1024.0 * 1024.0), 1);
            app.LastUpdatedUtc = fi.LastWriteTimeUtc;
            app.LastUpdatedText = fi.LastWriteTime.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
            app.IsPlanned = false;
        }
        else if (app.ResolvedProjectDir != null)
        {
            var di = new DirectoryInfo(app.ResolvedProjectDir);
            app.LastUpdatedUtc = di.LastWriteTimeUtc;
            app.LastUpdatedText = di.LastWriteTime.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
        }
    }

    public static (string Prefix, string Suffix) SplitReaName(string id)
    {
        if (id.EndsWith("REA", StringComparison.OrdinalIgnoreCase) && id.Length > 3)
            return (id[..^3], id[^3..].ToUpperInvariant());
        return (id, "");
    }

    static string? ReadCsprojVersion(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var v = doc.Descendants("Version").FirstOrDefault()?.Value;
            return string.IsNullOrWhiteSpace(v) ? null : "v" + v.TrimStart('v', 'V');
        }
        catch { return null; }
    }

    static string GuessIconType(string id) =>
        id.Contains("note", StringComparison.OrdinalIgnoreCase) ? "notepad" : "app";

    static List<AppEntry> GetDefaultCatalog() =>
    [
        new()
        {
            Id = "NotepadREA", Prefix = "Notepad", Suffix = "REA", AuthorBadge = "BY NOREA", Version = "v2.0.0",
            Runtime = "Scintilla/Lexilla · .NET 10",
            Tagline = "Editor di testo e codice Scintilla/Lexilla",
            Description = "Ricostruzione completa da zero basata sull'analisi statica REA di Notepad++ 8.9.8.1.",
            IconType = "notepad", AccentHex = "#60A5FA", ProjectPath = "NotepadREA", BinaryPath = @"dist\NotepadREA.exe",
            AnalysisPath = "ANALISI_REA.md", InstallScript = "installa_associazioni.ps1", UninstallScript = "rimuovi_associazioni.ps1",
            EstimatedSizeMb = 53.7, LastUpdatedText = "10 nov 2026", IsPlanned = false,
            Analysis = new AnalysisSection
            {
                Title = "Esito Analisi Statica (Notepad++ 8.9.8.1 Portable)",
                Rows =
                [
                    new AnalysisRow { Label = "Target analizzato:", Value = "notepad++.exe (PE32+, AMD64, 8.5 MB, Authenticode firmato)" },
                    new AnalysisRow { Label = "Motore individuato:", Value = "Scintilla + Lexilla linkati staticamente (nessuna DLL esterna)" },
                    new AnalysisRow { Label = "Hardening:", Value = "Control Flow Guard attivo, Security Cookie /GS presente" },
                    new AnalysisRow { Label = "Ricostruzione NOREA:", Value = "C# .NET 10 WinForms con Scintilla5.NET (zero codice sorgente Notepad++)" }
                ]
            }
        },
    ];
}
