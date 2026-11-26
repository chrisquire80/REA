using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using NoreaDesign;

namespace NoreaLauncher;

public sealed partial class MainForm
{
    async Task CheckCloudUpdatesAsync(bool manual = false)
    {
        if (manual)
        {
            _infoBar.ShowMessage("Verifica disponibilità aggiornamenti su GitHub...", InfoBarControl.BarType.Info, _theme);
        }

        string currentVer = "v" + (typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "2.5.0");
        var release = await CloudUpdateService.CheckForUpdatesAsync(_settings.GitHubRepo, currentVer, _settings.IncludePreReleases);

        if (release != null && release.IsUpdateAvailable)
        {
            _settings.LastCloudCheckUtc = DateTime.UtcNow;
            _settings.Save();

            // Mostra flyout toast in stile ArtCraft / Modern Dark
            var flyout = new UpdateNotificationFlyout(
                appName: "NotepadREA",
                oldVersion: currentVer,
                newVersion: release.RemoteVersion,
                author: "by NOREA",
                changelogUrl: release.ReleasePageUrl,
                changelogText: release.Changelog,
                appIcon: Icon,
                accentColor: Color.FromArgb(197, 22, 45));

            flyout.PositionAtBottomRight(this);
            flyout.Show(this);

            var progress = new Progress<int>(pct =>
            {
                flyout.SetProgress(pct, $"Downloading and installing update ({pct}%)...");
                _infoBar.ShowMessage($"Download aggiornamento {release.RemoteVersion}: {pct}%", InfoBarControl.BarType.Info, _theme);
            });

            string targetDir = _settings.InstallFolder;
            var result = await CloudUpdateService.DownloadAndApplyUpdateAsync(release, targetDir, progress);
            if (result.Success)
            {
                flyout.SetCompleted(result.Message);
                _infoBar.ShowMessage(result.Message, InfoBarControl.BarType.Success, _theme);
                if (result.RestartRequired)
                    QuitApplication();
                else
                    ReloadCatalog();
            }
            else
            {
                flyout.SetError($"Update failed: {result.Message}");
                _infoBar.ShowMessage(result.Message, InfoBarControl.BarType.Error, _theme);
            }
        }
        else if (manual)
        {
            _infoBar.ShowMessage($"Nessun aggiornamento trovato. La suite locale è già all'ultima versione ({currentVer}).", InfoBarControl.BarType.Success, _theme);
        }
    }

    void LaunchApp(AppEntry app, bool runAsAdmin = false)
    {
        var exe = app.InstalledExePath ?? app.DistExePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            _infoBar.ShowMessage($"L'eseguibile di {app.DisplayName} non è stato trovato.", InfoBarControl.BarType.Error, _theme);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
            };
            if (runAsAdmin) psi.Verb = "runas";
            var p = Process.Start(psi);
            if (p != null)
            {
                app.IsRunning = true;
                app.RunningPid = p.Id;
                try
                {
                    p.EnableRaisingEvents = true;
                    p.Exited += (_, _) =>
                    {
                        try
                        {
                            if (IsHandleCreated && !IsDisposed)
                                BeginInvoke(() => { app.IsRunning = false; app.RunningPid = null; CheckProcessStatus(); });
                        }
                        catch { }
                    };
                }
                catch { }
            }
            _infoBar.ShowMessage($"{app.DisplayName} avviato con successo.", InfoBarControl.BarType.Success, _theme);
            CheckProcessStatus();
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Errore nell'avvio di {app.DisplayName}: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }

    void LaunchAppCustomArgs(AppEntry app)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox("Inserisci argomenti CLI da passare all'applicazione:", $"Avvia {app.DisplayName}", "");
        if (input == null) return;

        var exe = app.InstalledExePath ?? app.DistExePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;

        try
        {
            var psi = new ProcessStartInfo(exe, input)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
            };
            var p = Process.Start(psi);
            if (p != null)
            {
                app.IsRunning = true;
                app.RunningPid = p.Id;
                try
                {
                    p.EnableRaisingEvents = true;
                    p.Exited += (_, _) =>
                    {
                        try
                        {
                            if (IsHandleCreated && !IsDisposed)
                                BeginInvoke(() => { app.IsRunning = false; app.RunningPid = null; CheckProcessStatus(); });
                        }
                        catch { }
                    };
                }
                catch { }
            }
            _infoBar.ShowMessage($"{app.DisplayName} avviato con argomenti: {input}", InfoBarControl.BarType.Info, _theme);
            CheckProcessStatus();
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Errore: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }

    void StopAppProcess(AppEntry app)
    {
        try
        {
            var procs = Process.GetProcessesByName(app.Id)
                .Where(p =>
                {
                    try
                    {
                        string? fn = p.MainModule?.FileName;
                        return !string.IsNullOrEmpty(fn) &&
                               (string.Equals(fn, app.InstalledExePath, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(fn, app.DistExePath, StringComparison.OrdinalIgnoreCase));
                    }
                    catch { return true; }
                }).ToList();

            if (procs.Count == 0)
            {
                _infoBar.ShowMessage($"Nessun processo attivo di {app.DisplayName} trovato.", InfoBarControl.BarType.Info, _theme);
                CheckProcessStatus();
                return;
            }

            // Tentativo 1: Chiusura ordinata
            foreach (var p in procs)
            {
                try { p.CloseMainWindow(); } catch { }
            }

            bool allExited = true;
            foreach (var p in procs)
            {
                try
                {
                    if (!p.WaitForExit(2500)) allExited = false;
                }
                catch { }
            }

            if (!allExited)
            {
                if (MessageBox.Show(this,
                    $"Il processo {app.DisplayName} non risponde alla chiusura ordinata.\nVuoi forzare l'arresto immediato? Le eventuali modifiche non salvate andranno perse.",
                    "Forza arresto processo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    foreach (var p in procs)
                    {
                        try { if (!p.HasExited) p.Kill(); } catch { }
                    }
                    _infoBar.ShowMessage($"Processo {app.DisplayName} arrestato forzatamente.", InfoBarControl.BarType.Warning, _theme);
                }
            }
            else
            {
                _infoBar.ShowMessage($"Processo {app.DisplayName} chiuso regolarmente.", InfoBarControl.BarType.Success, _theme);
            }

            foreach (var p in procs) p.Dispose();
            CheckProcessStatus();
        }
        catch (Exception ex)
        {
            Log.Error($"Errore arresto {app.DisplayName}", ex);
            _infoBar.ShowMessage($"Impossibile arrestare il processo: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }

    async Task InstallOrUpdateApp(AppEntry app)
    {
        if (app.IsOperating) return;
        app.IsOperating = true;
        _detailView.SetApp(app, _theme);
        _infoBar.ShowMessage($"Installazione di {app.DisplayName} in corso...", InfoBarControl.BarType.Info, _theme);

        string oldVer = app.Version;
        string newVer = "v2.2.0";
        var flyout = new UpdateNotificationFlyout(
            appName: app.DisplayName,
            oldVersion: oldVer,
            newVersion: newVer,
            author: app.AuthorBadge.StartsWith("BY ", StringComparison.OrdinalIgnoreCase) 
                ? "by " + app.AuthorBadge[3..] 
                : app.AuthorBadge,
            changelogUrl: "https://github.com/chrisquire80/REA/releases",
            appIcon: Icon,
            accentColor: Color.FromArgb(197, 22, 45));

        flyout.PositionAtBottomRight(this);
        flyout.Show(this);
        flyout.SetProgress(40, "Downloading and installing update...");

        var (success, msg) = await InstallService.InstallOrUpdateAppAsync(app, _settings);
        app.IsOperating = false;
        ReloadCatalog();

        if (success)
        {
            flyout.SetCompleted("Update downloaded and installed successfully!");
            _infoBar.ShowMessage(msg, InfoBarControl.BarType.Success, _theme);
        }
        else
        {
            flyout.SetError(msg);
            _infoBar.ShowMessage(msg, InfoBarControl.BarType.Error, _theme);
        }
    }

    async Task UninstallApp(AppEntry app)
    {
        if (MessageBox.Show(this,
            $"Confermi la disinstallazione di {app.DisplayName}?\nVerranno rimosse le associazioni file e la cartella del programma.",
            $"Disinstalla {app.DisplayName}", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        app.IsOperating = true;
        _detailView.SetApp(app, _theme);
        _infoBar.ShowMessage($"Disinstallazione di {app.DisplayName} in corso...", InfoBarControl.BarType.Info, _theme);

        var (success, msg) = await InstallService.UninstallAppAsync(app);
        app.IsOperating = false;
        ReloadCatalog();

        if (success)
            _infoBar.ShowMessage(msg, InfoBarControl.BarType.Success, _theme);
        else
            _infoBar.ShowMessage(msg, InfoBarControl.BarType.Error, _theme);
    }

    void OpenAppSourceOrAnalysis(AppEntry app)
    {
        if (app.ResolvedAnalysisFile != null && File.Exists(app.ResolvedAnalysisFile))
        {
            var notepadRea = _apps.FirstOrDefault(a => a.Id == "NotepadREA")?.InstalledExePath
                          ?? Path.Combine(_settings.WorkspaceDir ?? "", "dist", "NotepadREA.exe");
            if (File.Exists(notepadRea))
                Process.Start(notepadRea, $"\"{app.ResolvedAnalysisFile}\"");
            else
                Process.Start(new ProcessStartInfo(app.ResolvedAnalysisFile) { UseShellExecute = true });
        }
        else
        {
            _infoBar.ShowMessage("Nessun report di analisi statica trovato per questa applicazione.", InfoBarControl.BarType.Info, _theme);
        }
    }

    void ShowAppBinFolder(AppEntry app)
    {
        var exe = app.InstalledExePath ?? app.DistExePath;
        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            Process.Start("explorer.exe", $"/select,\"{exe}\"");
        else
            _infoBar.ShowMessage("Cartella binari non disponibile.", InfoBarControl.BarType.Warning, _theme);
    }

    void CleanAppCache(AppEntry app)
    {
        var cfgDir = app.GetConfigAndBackupDir();
        if (Directory.Exists(cfgDir))
        {
            try
            {
                var bakDir = Path.Combine(cfgDir, "backup");
                if (Directory.Exists(bakDir)) Directory.Delete(bakDir, recursive: true);
                _infoBar.ShowMessage($"File temporanei e backup di {app.DisplayName} ripuliti.", InfoBarControl.BarType.Success, _theme);
            }
            catch (Exception ex)
            {
                Log.Error("Errore pulizia cache", ex);
                _infoBar.ShowMessage($"Errore pulizia: {ex.Message}", InfoBarControl.BarType.Error, _theme);
            }
        }
        else
        {
            _infoBar.ShowMessage("Nessuna cache presente per questa applicazione.", InfoBarControl.BarType.Info, _theme);
        }
    }

    void VerifyAppIntegrity(AppEntry app)
    {
        var res = InstallService.VerifyIntegrity(app);
        if (res.Valid)
        {
            app.Sha256Short = res.Hash.Length >= 16 ? res.Hash[..16] + "..." : res.Hash;
            _detailView.SetApp(app, _theme);
            _infoBar.ShowMessage(res.Message, InfoBarControl.BarType.Success, _theme,
                actionText: "Copia Hash", action: () => Clipboard.SetText(res.Hash));
        }
        else
        {
            _infoBar.ShowMessage(res.Message, InfoBarControl.BarType.Error, _theme);
        }
    }

    void RollbackAppVersion(AppEntry app)
    {
        var res = InstallService.RollbackApp(app);
        if (res.Success)
        {
            ReloadCatalog();
            _infoBar.ShowMessage(res.Message, InfoBarControl.BarType.Success, _theme);
        }
        else
        {
            _infoBar.ShowMessage(res.Message, InfoBarControl.BarType.Error, _theme);
        }
    }
}
