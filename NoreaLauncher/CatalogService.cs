using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Win32;

namespace NoreaLauncher;

public static partial class CatalogService
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
        bool catalogExists = File.Exists(catalogPath);

        if (catalogExists)
        {
            try
            {
                list = JsonSerializer.Deserialize<List<AppEntry>>(File.ReadAllText(catalogPath), JsonOpts)
                    ?? throw new JsonException("Il catalogo è vuoto o non contiene un array valido.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Catalogo non valido in {catalogPath}; il file originale verrà preservato.", ex);
                LauncherSettings.PreserveCorruptFile(catalogPath);
                list = [];
            }
        }

        if (list.Count == 0)
        {
            list = GetDefaultCatalog();
            if (!catalogExists) SaveCatalog(settings, list);
        }

        if (Directory.Exists(ws))
            DiscoverWorkspaceProjects(ws, list, settings);

        foreach (var app in list)
            ResolveAppState(app, ws, settings);

        list.RemoveAll(a => IsExcludedSuiteComponent(a.Id) || a.IsPlanned || (!a.IsInstalled && !a.HasDistBuild && !a.HasProject));
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
        string? temp = null;
        try
        {
            var path = GetCatalogPath(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temp = path + $".{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(apps, JsonOpts));
            LauncherSettings.ReplaceFile(temp, path);
        }
        catch (Exception ex) { Log.Warn("Impossibile salvare il catalogo NOREA.", ex); }
        finally { LauncherSettings.DeleteTemporaryFile(temp); }
    }
}
