using System.Diagnostics;
using System.Text.Json;

namespace NoreaLauncher;

public sealed class AppManifestItem
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTime InstalledUtc { get; set; }
}

public static class InstallService
{
    static string ManifestPath => Path.Combine(LauncherSettings.AppDataDir, "manifest.json");
    static readonly object Sync = new();

    public static Dictionary<string, AppManifestItem> LoadManifest()
    {
        lock (Sync)
        {
            try
            {
                if (File.Exists(ManifestPath))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, AppManifestItem>>(File.ReadAllText(ManifestPath))
                           ?? new(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Errore caricamento manifest", ex);
            }
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SaveManifest(Dictionary<string, AppManifestItem> manifest)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(LauncherSettings.AppDataDir);
                File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log.Warn("Errore salvataggio manifest", ex);
            }
        }
    }

    public static async Task<(bool Success, string Message)> InstallOrUpdateAppAsync(AppEntry app, LauncherSettings settings)
    {
        string? sourceExe = app.DistExePath;
        if (string.IsNullOrEmpty(sourceExe) || !File.Exists(sourceExe))
            return (false, $"Eseguibile compilato di {app.DisplayName} non trovato in dist.");

        try
        {
            string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", app.Id);
            Directory.CreateDirectory(installDir);

            string targetExe = Path.Combine(installDir, app.Id + ".exe");
            string prevExe = targetExe + ".prev";
            string newExe = targetExe + ".new";

            // Copia preliminare su file .new per verifica hash
            File.Copy(sourceExe, newExe, overwrite: true);
            string srcHash = LauncherUtil.ComputeFileSha256(sourceExe);
            string newHash = LauncherUtil.ComputeFileSha256(newExe);

            if (srcHash != newHash)
            {
                File.Delete(newExe);
                return (false, "Verifica integrità fallita durante la copia.");
            }

            // Se esiste già una versione installata, la spostiamo su .prev per consentire il rollback
            if (File.Exists(targetExe))
            {
                try
                {
                    if (File.Exists(prevExe)) File.Delete(prevExe);
                    File.Move(targetExe, prevExe);
                }
                catch (Exception ex)
                {
                    Log.Warn("Impossibile creare rollback .prev", ex);
                }
            }

            // Spostamento atomico della nuova versione
            File.Move(newExe, targetExe, overwrite: true);

            // Copia cartella runtimes e DLL native/dipendenti (es. Scintilla, Lexilla)
            string? sourceDir = Path.GetDirectoryName(sourceExe);
            if (!string.IsNullOrEmpty(sourceDir))
            {
                string sourceRuntimes = Path.Combine(sourceDir, "runtimes");
                if (Directory.Exists(sourceRuntimes))
                {
                    CopyDirectory(sourceRuntimes, Path.Combine(installDir, "runtimes"));
                }
                foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
                {
                    try { File.Copy(dll, Path.Combine(installDir, Path.GetFileName(dll)), overwrite: true); } catch { }
                }
            }

            // Copia icona se presente
            string? projDir = app.ResolvedProjectDir;
            if (projDir != null)
            {
                string iconPath = Path.Combine(projDir, "app.ico");
                if (File.Exists(iconPath))
                {
                    try { File.Copy(iconPath, Path.Combine(installDir, app.Id + ".ico"), overwrite: true); } catch { }
                }
            }

            // Registra nel manifest
            var manifest = LoadManifest();
            manifest[app.Id] = new AppManifestItem
            {
                Id = app.Id,
                Version = app.Version,
                ExePath = targetExe,
                Sha256 = srcHash,
                InstalledUtc = DateTime.UtcNow
            };
            SaveManifest(manifest);

            // Esecuzione dello script di installazione associazioni se configurato
            if (!string.IsNullOrEmpty(app.ResolvedInstallScript) && File.Exists(app.ResolvedInstallScript) &&
                settings.InstallFormat.Contains("Associazioni", StringComparison.OrdinalIgnoreCase))
            {
                await RunPowerShellScriptAsync(app.ResolvedInstallScript, targetExe);
            }

            // Collegamento Desktop se richiesto
            if (settings.InstallFormat.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                CreateDesktopShortcut(app.DisplayName, targetExe);
            }

            app.InstalledExePath = targetExe;
            return (true, $"{app.DisplayName} installato con successo!");
        }
        catch (Exception ex)
        {
            Log.Error($"Errore durante l'installazione di {app.DisplayName}", ex);
            return (false, $"Errore installazione: {ex.Message}");
        }
    }

    public static (bool Success, string Message) RollbackApp(AppEntry app)
    {
        if (!app.HasRollback || app.InstalledExePath == null || app.RollbackExePath == null)
            return (false, "Nessuna versione precedente (.prev) disponibile per il rollback.");

        try
        {
            string cur = app.InstalledExePath;
            string prev = app.RollbackExePath;
            string tmp = cur + ".tmp";

            File.Move(cur, tmp);
            File.Move(prev, cur);
            File.Move(tmp, prev);

            // Aggiorna manifest con hash del binario ripristinato
            string restoredHash = LauncherUtil.ComputeFileSha256(cur);
            var manifest = LoadManifest();
            if (manifest.TryGetValue(app.Id, out var item))
            {
                item.Sha256 = restoredHash;
                item.InstalledUtc = DateTime.UtcNow;
                SaveManifest(manifest);
            }

            return (true, $"Rollback di {app.DisplayName} completato con successo.");
        }
        catch (Exception ex)
        {
            Log.Error("Errore durante il rollback", ex);
            return (false, $"Errore rollback: {ex.Message}");
        }
    }

    public static async Task<(bool Success, string Message)> UninstallAppAsync(AppEntry app)
    {
        try
        {
            // Esegui script di rimozione se disponibile
            if (!string.IsNullOrEmpty(app.ResolvedUninstallScript) && File.Exists(app.ResolvedUninstallScript))
            {
                await RunPowerShellScriptAsync(app.ResolvedUninstallScript, app.InstalledExePath ?? "");
            }

            if (!string.IsNullOrEmpty(app.InstalledExePath))
            {
                string? dir = Path.GetDirectoryName(app.InstalledExePath);
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            // Rimuovi dal manifest
            var manifest = LoadManifest();
            if (manifest.Remove(app.Id)) SaveManifest(manifest);

            app.InstalledExePath = null;
            return (true, $"{app.DisplayName} disinstallato con successo.");
        }
        catch (Exception ex)
        {
            Log.Error("Errore durante la disinstallazione", ex);
            return (false, $"Errore disinstallazione: {ex.Message}");
        }
    }

    public static (bool Valid, string Hash, string Message) VerifyIntegrity(AppEntry app)
    {
        string? exe = app.InstalledExePath ?? app.DistExePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return (false, "", "Binario non trovato sul disco.");

        string currentHash = LauncherUtil.ComputeFileSha256(exe);
        var manifest = LoadManifest();

        if (manifest.TryGetValue(app.Id, out var item) && !string.IsNullOrEmpty(item.Sha256))
        {
            if (string.Equals(currentHash, item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return (true, currentHash, $"Integrità verificata: coincide con l'hash di rilascio registrato ({currentHash[..8]}...).");
            }
            return (false, currentHash, $"Attenzione: l'hash attuale ({currentHash[..8]}...) non coincide con quello registrato ({item.Sha256[..8]}...)!");
        }

        // Nessun hash precedente: lo registriamo come baseline
        manifest[app.Id] = new AppManifestItem
        {
            Id = app.Id,
            Version = app.Version,
            ExePath = exe,
            Sha256 = currentHash,
            InstalledUtc = DateTime.UtcNow
        };
        SaveManifest(manifest);

        return (true, currentHash, $"Nessun hash di riferimento trovato: registrato l'hash attuale ({currentHash[..8]}...) come baseline.");
    }

    static async Task RunPowerShellScriptAsync(string scriptPath, string targetExe)
    {
        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Sorgente \"{targetExe}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(10000);
            }
            catch (Exception ex)
            {
                Log.Warn($"Errore esecuzione script {scriptPath}", ex);
            }
        });
    }

    static void CreateDesktopShortcut(string name, string targetPath)
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string linkPath = Path.Combine(desktop, $"{name}.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;
            var shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
        }
        catch (Exception ex)
        {
            Log.Warn("Errore creazione collegamento Desktop", ex);
        }
    }

    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        try
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, dest);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Errore durante la copia della directory da {sourceDir} a {destinationDir}", ex);
        }
    }
}
