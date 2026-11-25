using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using NoreaDesign;

namespace NoreaLauncher;

public sealed partial class MainForm
{
    public void ReloadCatalog()
    {
        _apps = CatalogService.LoadApps(_settings);
        FilterSidebarList();
        _statusBar.Invalidate();
    }

    void FilterSidebarList()
    {
        _sidebarList.SuspendLayout();
        _sidebarList.Controls.Clear();

        string q = _tbSearch.Text.Trim();
        var filtered = _apps.AsEnumerable();

        if (_activeFilter == "INSTALLED") filtered = filtered.Where(a => a.IsInstalled);
        else if (_activeFilter == "READY") filtered = filtered.Where(a => a.CanBuildOrInstall);

        if (!string.IsNullOrEmpty(q))
        {
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Tagline.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();

        if (list.Count == 0)
        {
            _detailView.Visible = false;
            _emptyStatePanel.Visible = true;
            _selectedApp = null;
        }
        else
        {
            _emptyStatePanel.Visible = false;
            _detailView.Visible = true;

            int y = 0;
            var defaultApp = list.FirstOrDefault(a => a.IsInstalled) ?? list[0];
            foreach (var app in list)
            {
                var item = new AppListSideItem(app, _theme)
                {
                    Dock = DockStyle.Top,
                    IsSelected = app == _selectedApp || (_selectedApp == null && app == defaultApp)
                };
                var a = app;
                item.Click += (_, _) => SelectApp(a);
                _sidebarList.Controls.Add(item);
                y += item.Height;
            }

            if (_selectedApp == null || !list.Contains(_selectedApp))
                SelectApp(defaultApp);
            else
                _detailView.SetApp(_selectedApp, _theme);
        }

        _sidebarList.ResumeLayout();
    }

    void SelectApp(AppEntry app)
    {
        _selectedApp = app;
        foreach (var item in _sidebarList.Controls.OfType<AppListSideItem>())
            item.IsSelected = item.App == app;
        _sidebarList.Invalidate(true);
        _detailView.SetApp(app, _theme);
    }

    internal void ToggleSettings(bool showSettings)
    {
        _inSettings = showSettings;
        _toolbar.Visible = !showSettings;
        _mainHost.Visible = !showSettings;
        _settingsView.Visible = showSettings;
        if (!showSettings) ReloadCatalog();
        _titleBar.Invalidate();
    }

    void OpenCommandPalette()
    {
        using var cp = new CommandPaletteForm(
            _apps,
            _theme,
            onSyncCatalog: ReloadCatalog,
            onOpenSettings: () => ToggleSettings(true),
            onOpenWorkspace: () =>
            {
                var ws = _settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
                if (Directory.Exists(ws)) Process.Start("explorer.exe", ws);
            },
            onCheckCloudUpdates: () => _ = CheckCloudUpdatesAsync(manual: true),
            onShowUpdateDemo: ShowUpdateFlyoutDemo);
        cp.ShowDialog(this);
    }

    public void ShowUpdateFlyoutDemo()
    {
        var flyout = new UpdateNotificationFlyout(
            appName: "NotepadREA",
            oldVersion: "v2.1.0",
            newVersion: "v2.2.0",
            author: "by NOREA",
            changelogUrl: "https://github.com/chrisquire80/REA/releases",
            changelogText: "Novità v2.2.0:\n- Terminale PowerShell integrato (F12)\n- Gutter Git Diff\n- Ispezione Esadecimale (Ctrl+Alt+H)\n- Flyout notifica aggiornamenti",
            appIcon: Icon,
            accentColor: Color.FromArgb(197, 22, 45));

        flyout.PositionAtBottomRight(this);
        flyout.Show(this);

        var timer = new System.Windows.Forms.Timer { Interval = 1100 };
        int step = 0;
        timer.Tick += (_, _) =>
        {
            step++;
            if (step == 1) flyout.SetProgress(35, "Downloading and installing update (35%)...");
            else if (step == 2) flyout.SetProgress(78, "Downloading and installing update (78%)...");
            else if (step >= 3)
            {
                timer.Stop();
                timer.Dispose();
                flyout.SetCompleted("Update downloaded and installed successfully!");
            }
        };
        timer.Start();
    }

    void ConfigureAiBridge()
    {
        try
        {
            var noreaCli = Path.Combine(AppContext.BaseDirectory, "norea.exe");
            if (!File.Exists(noreaCli))
            {
                var ws = _settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
                noreaCli = Path.Combine(ws, "dist", "norea.exe");
            }

            if (File.Exists(noreaCli))
            {
                var psi = new ProcessStartInfo(noreaCli, "setup-ai")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                string outText = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit(5000);
                _infoBar.ShowMessage("Integrazione AI configurata per Claude Code e Antigravity!", InfoBarControl.BarType.Success, _theme);
                MessageBox.Show(this, outText, "Configurazione AI Bridge Completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _infoBar.ShowMessage("Eseguibile norea.exe non trovato in dist/", InfoBarControl.BarType.Error, _theme);
            }
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Errore configurazione AI: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }
}
