using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using NoreaDesign;

namespace NoreaLauncher;

public sealed partial class MainForm
{
    void RebuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add("Apri NOREA Launcher", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add(new ToolStripSeparator());
        foreach (var app in _apps.Where(a => a.IsInstalled || a.HasDistBuild))
        {
            var a = app;
            _trayMenu.Items.Add($"Avvia {a.DisplayName} ({a.Version})", null, (_, _) => LaunchApp(a));
        }
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Impostazioni...", null, (_, _) => { RestoreFromTray(); ToggleSettings(true); });
        _trayMenu.Items.Add("Esci da NOREA Launcher", null, (_, _) => QuitApplication());
    }

    void ApplyTheme()
    {
        _theme = LauncherTheme.FromAppearance(_settings.Appearance);
        ApplyDwmDarkFrame();

        BackColor = _theme.Bg;
        _toolbar.BackColor = _theme.Bg;
        _sidebar.BackColor = _theme.PanelBg;
        _sidebarList.BackColor = _theme.PanelBg;
        _detailView.SetTheme(_theme);

        _btnRefresh.BackColor = _theme.SecondaryBtnBg;
        _btnRefresh.ForeColor = _theme.SecondaryBtnFg;
        _btnRefresh.FlatAppearance.BorderColor = _theme.Border;

        _searchBox.BackColor = _theme.InputBg;
        _tbSearch.BackColor = _theme.InputBg;
        _tbSearch.ForeColor = _theme.TextPrimary;

        _chipCtrlK.BackColor = _theme.SecondaryBtnBg;
        _chipCtrlK.ForeColor = _theme.TextSecondary;
        _chipCtrlK.FlatAppearance.BorderColor = _theme.BorderStrong;

        _settingsView.SetTheme(_theme);
        foreach (var item in _sidebarList.Controls.OfType<AppListSideItem>())
            item.SetTheme(_theme);

        Invalidate(true);
    }

    public void ExportPreviews(string outDir)
    {
        PreviewExporter.ExportPreviews(this, outDir);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _settings.Maximized = WindowState == FormWindowState.Maximized;
        _settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Save();

        if (!_forceQuit && _settings.KeepInTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            var pt = PointToClient(Cursor.Position);
            int g = 6;
            if (pt.X < g && pt.Y < g) m.Result = (IntPtr)HTTOPLEFT;
            else if (pt.X > Width - g && pt.Y < g) m.Result = (IntPtr)HTTOPRIGHT;
            else if (pt.X < g && pt.Y > Height - g) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (pt.X > Width - g && pt.Y > Height - g) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (pt.X < g) m.Result = (IntPtr)HTLEFT;
            else if (pt.X > Width - g) m.Result = (IntPtr)HTRIGHT;
            else if (pt.Y < g) m.Result = (IntPtr)HTTOP;
            else if (pt.Y > Height - g) m.Result = (IntPtr)HTBOTTOM;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _procTimer.Dispose();
            _trayIcon.Dispose();
            _trayMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
