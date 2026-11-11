using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace NoreaLauncher;

public sealed class MainForm : Form
{
    readonly LauncherSettings _settings = LauncherSettings.Load();
    List<AppEntry> _apps = [];
    LauncherTheme _theme = LauncherTheme.Dark;
    AppEntry? _selectedApp;

    // Struttura interfaccia
    readonly Panel _titleBar = new() { Dock = DockStyle.Top, Height = 44 };
    readonly Panel _toolbar = new() { Dock = DockStyle.Top, Height = 50 };
    readonly InfoBarControl _infoBar = new();
    readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = 32 };

    // Layout Master-Detail
    readonly Panel _mainHost = new() { Dock = DockStyle.Fill };
    readonly Panel _sidebar = new() { Dock = DockStyle.Left, Width = 310 };
    readonly Panel _sidebarList = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Panel _sidebarHeader = new() { Dock = DockStyle.Top, Height = 32 };
    readonly AppDetailView _detailView = new();
    readonly Panel _emptyStatePanel = new() { Dock = DockStyle.Fill };

    readonly SettingsView _settingsView;

    // Toolbar controls
    readonly Button _btnRefresh = new() { Text = "", Size = new Size(32, 30), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
    readonly Panel _searchBox = new() { Size = new Size(260, 30), Padding = new Padding(8, 5, 62, 5) };
    readonly TextBox _tbSearch = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, PlaceholderText = "Cerca apps...", Font = new Font("Segoe UI", 9.2f) };
    readonly Button _chipCtrlK = new() { Text = "Ctrl+K", Size = new Size(54, 20), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold) };

    // Filtri interattivi barra di stato
    string _activeFilter = "ALL"; // "ALL", "INSTALLED", "READY"
    Rectangle _chipInstalledRect, _chipReadyRect, _chipTotalRect;

    readonly System.Windows.Forms.Timer _procTimer = new() { Interval = 2000 };
    readonly NotifyIcon _trayIcon;
    readonly ContextMenuStrip _trayMenu = new();

    Rectangle _gearRect, _minRect, _maxRect, _closeRect;
    int _hoverTitleBtn = -1;
    bool _inSettings;
    bool _forceQuit;

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainForm()
    {
        Text = "NOREA Launcher";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(960, 620);
        Size = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;

        if (_settings.Bounds is { } b && SystemInformation.VirtualScreen.IntersectsWith(b))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = b;
        }
        if (_settings.Maximized) WindowState = FormWindowState.Maximized;

        _settingsView = new SettingsView(_settings) { Visible = false };
        _settingsView.DoneRequested += (_, _) => ToggleSettings(false);
        _settingsView.QuitRequested += (_, _) => QuitApplication();
        _settingsView.ThemeChanged += (_, _) => ApplyTheme();

        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "NOREA Launcher — App Suite",
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayMenu.Opening += (_, _) => RebuildTrayMenu();

        BuildTitleBar();
        BuildToolbar();
        BuildMasterDetail();
        BuildStatusBar();
        BuildEmptyState();

        Controls.Add(_mainHost);
        Controls.Add(_settingsView);
        Controls.Add(_infoBar);
        Controls.Add(_toolbar);
        Controls.Add(_titleBar);
        Controls.Add(_statusBar);

        _procTimer.Tick += (_, _) => CheckProcessStatus();
        _procTimer.Start();

        KeyDown += OnFormKeyDown;
        HandleCreated += (_, _) => ApplyDwmDarkFrame();

        ReloadCatalog();
        ApplyTheme();
    }

    void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.K)
        {
            OpenCommandPalette();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_inSettings) ToggleSettings(false);
            else if (!string.IsNullOrEmpty(_tbSearch.Text)) _tbSearch.Clear();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
        {
            NavigateList(e.KeyCode == Keys.Down ? 1 : -1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && !_inSettings && _selectedApp != null)
        {
            LaunchApp(_selectedApp);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            ReloadCatalog();
            e.Handled = true;
        }
    }

    void NavigateList(int delta)
    {
        var items = _sidebarList.Controls.OfType<AppListSideItem>().ToList();
        if (items.Count == 0) return;
        int idx = items.FindIndex(i => i.App == _selectedApp);
        int next = Math.Clamp(idx + delta, 0, items.Count - 1);
        SelectApp(items[next].App);
    }

    void CheckProcessStatus()
    {
        if (CatalogService.RefreshRunningProcesses(_apps))
        {
            _detailView.SetApp(_selectedApp, _theme);
            foreach (var item in _sidebarList.Controls.OfType<AppListSideItem>())
                item.Invalidate();
        }
    }

    public void RestoreFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = _settings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    public void QuitApplication()
    {
        _forceQuit = true;
        _trayIcon.Visible = false;
        Close();
    }

    void ApplyDwmDarkFrame()
    {
        if (!IsHandleCreated) return;
        int dark = _theme.IsDark ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    void BuildTitleBar()
    {
        _titleBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(_theme.HeaderBg);
            using var border = new Pen(_theme.Border);

            // Brand
            using (var brandFont = new Font("Segoe UI", 11f, FontStyle.Bold))
                TextRenderer.DrawText(g, "NOREA", brandFont, new Point(16, 12), _theme.TextPrimary, TextFormatFlags.NoPadding);

            g.DrawLine(border, 86, 12, 86, 31);

            using (var suiteFont = new Font("Consolas", 8.2f, FontStyle.Regular))
                TextRenderer.DrawText(g, "APP SUITE", suiteFont, new Point(98, 15), _theme.TextSecondary, TextFormatFlags.NoPadding);

            // Pulsanti finestra
            int w = _titleBar.Width, h = _titleBar.Height;
            _closeRect = new Rectangle(w - 44, 0, 44, h - 1);
            _maxRect = new Rectangle(w - 88, 0, 44, h - 1);
            _minRect = new Rectangle(w - 132, 0, 44, h - 1);
            _gearRect = new Rectangle(w - 178, 0, 46, h - 1);

            g.DrawLine(border, _gearRect.Left, 0, _gearRect.Left, h - 1);
            g.DrawLine(border, _minRect.Left, 0, _minRect.Left, h - 1);

            DrawTitleBtn(g, _gearRect, "⚙", _hoverTitleBtn == 0 || _inSettings, false);
            DrawTitleBtn(g, _minRect, "─", _hoverTitleBtn == 1, false);
            DrawTitleBtn(g, _maxRect, WindowState == FormWindowState.Maximized ? "❐" : "□", _hoverTitleBtn == 2, false);
            DrawTitleBtn(g, _closeRect, "✕", _hoverTitleBtn == 3, true);

            g.DrawLine(border, 0, h - 1, w, h - 1);
        };

        _titleBar.MouseMove += (_, e) =>
        {
            int h = _gearRect.Contains(e.Location) ? 0
                  : _minRect.Contains(e.Location) ? 1
                  : _maxRect.Contains(e.Location) ? 2
                  : _closeRect.Contains(e.Location) ? 3 : -1;
            _titleBar.Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != _hoverTitleBtn) { _hoverTitleBtn = h; _titleBar.Invalidate(); }
        };

        _titleBar.MouseLeave += (_, _) => { _hoverTitleBtn = -1; _titleBar.Invalidate(); };

        _titleBar.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (_gearRect.Contains(e.Location)) { ToggleSettings(!_inSettings); return; }
            if (_minRect.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
            if (_maxRect.Contains(e.Location))
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                _titleBar.Invalidate();
                return;
            }
            if (_closeRect.Contains(e.Location)) { Close(); return; }

            if (e.Clicks == 2)
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                _titleBar.Invalidate();
            }
            else
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };
    }

    void DrawTitleBtn(Graphics g, Rectangle r, string glyph, bool hover, bool isClose)
    {
        if (hover)
        {
            using var bg = new SolidBrush(isClose ? Color.FromArgb(200, 50, 50) : _theme.SecondaryBtnHoverBg);
            g.FillRectangle(bg, r);
        }
        using var f = new Font("Segoe UI Symbol", 9.5f);
        var fg = hover && isClose ? Color.White : hover ? _theme.TextPrimary : _theme.TextSecondary;
        TextRenderer.DrawText(g, glyph, f, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    void BuildToolbar()
    {
        _btnRefresh.Location = new Point(16, 10);
        _btnRefresh.Click += (_, _) => ReloadCatalog();
        _btnRefresh.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var p = new Pen(_btnRefresh.ForeColor, 1.8f);
            var rect = new Rectangle(8, 7, 14, 14);
            g.DrawArc(p, rect, 45, 270);
            PointF tip = new PointF(19, 9);
            PointF p1 = new PointF(14, 6);
            PointF p2 = new PointF(15, 12);
            using var brush = new SolidBrush(_btnRefresh.ForeColor);
            g.FillPolygon(brush, [tip, p1, p2]);
        };

        _searchBox.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.BorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, _searchBox.Width - 1, _searchBox.Height - 1);
        };

        _chipCtrlK.Padding = Padding.Empty;
        _chipCtrlK.TextAlign = ContentAlignment.MiddleCenter;
        _chipCtrlK.Click += (_, _) => OpenCommandPalette();

        _searchBox.Controls.Add(_tbSearch);
        _searchBox.Controls.Add(_chipCtrlK);
        _searchBox.Resize += (_, _) =>
        {
            _chipCtrlK.Location = new Point(_searchBox.Width - _chipCtrlK.Width - 5, (_searchBox.Height - _chipCtrlK.Height) / 2);
        };
        _chipCtrlK.Location = new Point(_searchBox.Width - _chipCtrlK.Width - 5, (_searchBox.Height - _chipCtrlK.Height) / 2);
        _tbSearch.TextChanged += (_, _) => FilterSidebarList();

        _toolbar.Controls.AddRange([_btnRefresh, _searchBox]);
        _toolbar.Resize += (_, _) => LayoutToolbar();
        LayoutToolbar();
    }

    void LayoutToolbar()
    {
        int right = _toolbar.Width - 16;
        _searchBox.Location = new Point(right - _searchBox.Width, 10);
    }

    void BuildMasterDetail()
    {
        // Sidebar sinistra
        _sidebarHeader.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(_theme.PanelBg);
            using var font = new Font("Consolas", 8f, FontStyle.Bold);
            TextRenderer.DrawText(g, "APPLICAZIONI SUITE", font, new Point(16, 8), _theme.TextMuted);
            using var p = new Pen(_theme.Border);
            g.DrawLine(p, 0, _sidebarHeader.Height - 1, _sidebar.Width, _sidebarHeader.Height - 1);
        };
        _sidebar.Controls.Add(_sidebarList);
        _sidebar.Controls.Add(_sidebarHeader);

        _sidebar.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.Border);
            e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
        };

        // Area Dettaglio Destra
        _detailView.OpenRequested += (_, app) => LaunchApp(app);
        _detailView.OpenAsAdminRequested += (_, app) => LaunchApp(app, runAsAdmin: true);
        _detailView.OpenCustomArgsRequested += (_, app) => LaunchAppCustomArgs(app);
        _detailView.StopProcessRequested += (_, app) => StopAppProcess(app);
        _detailView.AnalysisRequested += (_, app) => OpenAppSourceOrAnalysis(app);
        _detailView.ShowBinFolderRequested += (_, app) => ShowAppBinFolder(app);
        _detailView.CleanCacheRequested += (_, app) => CleanAppCache(app);
        _detailView.VerifyIntegrityRequested += (_, app) => VerifyAppIntegrity(app);
        _detailView.RollbackRequested += (_, app) => RollbackAppVersion(app);

        _mainHost.Controls.Add(_detailView);
        _mainHost.Controls.Add(_emptyStatePanel);
        _mainHost.Controls.Add(_sidebar);
    }

    void BuildEmptyState()
    {
        _emptyStatePanel.Visible = false;
        _emptyStatePanel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(_theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = _emptyStatePanel.Width / 2;
            int cy = _emptyStatePanel.Height / 2 - 40;

            using var iconFont = new Font("Segoe UI Symbol", 36f);
            using var titleFont = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 9.5f);

            TextRenderer.DrawText(g, "🔍", iconFont, new Point(cx - 30, cy - 60), _theme.TextMuted);
            TextRenderer.DrawText(g, "Nessuna applicazione trovata", titleFont, new Point(cx - 130, cy), _theme.TextPrimary);
            TextRenderer.DrawText(g, "Prova a modificare i filtri o la ricerca.", subFont, new Point(cx - 120, cy + 28), _theme.TextSecondary);
        };

        var btnReset = new Button
        {
            Text = "Reimposta filtri",
            Size = new Size(130, 32),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
        };
        btnReset.Click += (_, _) =>
        {
            _tbSearch.Clear();
            _activeFilter = "ALL";
            FilterSidebarList();
            _statusBar.Invalidate();
        };
        _emptyStatePanel.Resize += (_, _) =>
        {
            btnReset.Location = new Point((_emptyStatePanel.Width - btnReset.Width) / 2, _emptyStatePanel.Height / 2 + 36);
        };
        _emptyStatePanel.Controls.Add(btnReset);
    }

    void BuildStatusBar()
    {
        _statusBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(_theme.HeaderBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var border = new Pen(_theme.Border);
            g.DrawLine(border, 0, 0, _statusBar.Width, 0);

            // Indicatore di stato / connessione a semaforo
            using var dotBrush = new SolidBrush(_theme.StatusGreen);
            g.FillEllipse(dotBrush, 16, 12, 8, 8);

            using var fStatus = new Font("Segoe UI", 8.5f);
            TextRenderer.DrawText(g, "Sincronizzato col workspace", fStatus, new Point(30, 7), _theme.TextSecondary);

            // Chip interattivi contatori
            using var monoFont = new Font("Consolas", 8f, FontStyle.Bold);
            int installed = _apps.Count(a => a.IsInstalled);
            int ready = _apps.Count(a => a.CanBuildOrInstall);
            int total = _apps.Count;

            int right = _statusBar.Width - 16;
            string totalText = $"TOTAL: {total}";
            string readyText = $"READY: {ready}";
            string instText = $"INSTALLED: {installed}";

            int wTot = GraphicsHelper.MeasurePillWidth(g, totalText, monoFont, padX: 8);
            int wRed = GraphicsHelper.MeasurePillWidth(g, readyText, monoFont, padX: 8);
            int wIns = GraphicsHelper.MeasurePillWidth(g, instText, monoFont, padX: 8);

            _chipTotalRect = new Rectangle(right - wTot, 6, wTot, 20);
            _chipReadyRect = new Rectangle(_chipTotalRect.Left - 8 - wRed, 6, wRed, 20);
            _chipInstalledRect = new Rectangle(_chipReadyRect.Left - 8 - wIns, 6, wIns, 20);

            void DrawChip(Rectangle r, string text, bool active, Color col)
            {
                var bg = active ? Color.FromArgb(40, col) : _theme.SecondaryBtnBg;
                GraphicsHelper.DrawPill(g, text, monoFont, r.Location, bg, col, border: active ? col : _theme.Border, padX: 8, height: 20);
            }

            DrawChip(_chipInstalledRect, instText, _activeFilter == "INSTALLED", _theme.StatusGreen);
            DrawChip(_chipReadyRect, readyText, _activeFilter == "READY", _theme.BrandBlue);
            DrawChip(_chipTotalRect, totalText, _activeFilter == "ALL", _theme.TextPrimary);
        };

        _statusBar.MouseMove += (_, e) =>
        {
            bool overChip = _chipInstalledRect.Contains(e.Location) || _chipReadyRect.Contains(e.Location) || _chipTotalRect.Contains(e.Location);
            _statusBar.Cursor = overChip ? Cursors.Hand : Cursors.Default;
        };

        _statusBar.MouseClick += (_, e) =>
        {
            if (_chipInstalledRect.Contains(e.Location)) _activeFilter = _activeFilter == "INSTALLED" ? "ALL" : "INSTALLED";
            else if (_chipReadyRect.Contains(e.Location)) _activeFilter = _activeFilter == "READY" ? "ALL" : "READY";
            else if (_chipTotalRect.Contains(e.Location)) _activeFilter = "ALL";
            else return;

            FilterSidebarList();
            _statusBar.Invalidate();
        };
    }

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
            foreach (var app in list)
            {
                var item = new AppListSideItem(app, _theme)
                {
                    Dock = DockStyle.Top,
                    IsSelected = app == _selectedApp || (_selectedApp == null && app == list[0])
                };
                var a = app;
                item.Click += (_, _) => SelectApp(a);
                _sidebarList.Controls.Add(item);
                y += item.Height;
            }

            if (_selectedApp == null || !list.Contains(_selectedApp))
                SelectApp(list[0]);
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

    void ToggleSettings(bool showSettings)
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
            });
        cp.ShowDialog(this);
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
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            if (runAsAdmin) psi.Verb = "runas";
            Process.Start(psi);
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
            Process.Start(new ProcessStartInfo(exe, input) { UseShellExecute = true });
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
            var procs = Process.GetProcessesByName(app.Id);
            foreach (var p in procs) { p.Kill(); p.Dispose(); }
            _infoBar.ShowMessage($"Processo {app.DisplayName} terminato.", InfoBarControl.BarType.Warning, _theme);
            CheckProcessStatus();
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Impossibile arrestare il processo: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
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
        var exe = app.InstalledExePath ?? app.DistExePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            _infoBar.ShowMessage("Binario non trovato per la verifica.", InfoBarControl.BarType.Error, _theme);
            return;
        }

        try
        {
            string hash = CatalogService.ComputeFileSha256(exe);
            app.Sha256Short = hash[..16] + "...";
            _detailView.SetApp(app, _theme);
            _infoBar.ShowMessage($"Integrità verificata con successo! SHA-256: {hash}", InfoBarControl.BarType.Success, _theme,
                actionText: "Copia Hash", action: () => Clipboard.SetText(hash));
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Verifica fallita: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }

    void RollbackAppVersion(AppEntry app)
    {
        if (!app.HasRollback || app.InstalledExePath == null || app.RollbackExePath == null)
        {
            _infoBar.ShowMessage("Nessuna versione precedente disponibile per il rollback.", InfoBarControl.BarType.Warning, _theme);
            return;
        }

        try
        {
            var cur = app.InstalledExePath;
            var prev = app.RollbackExePath;
            var tmp = cur + ".tmp";

            File.Move(cur, tmp);
            File.Move(prev, cur);
            File.Move(tmp, prev);

            ReloadCatalog();
            _infoBar.ShowMessage($"Rollback di {app.DisplayName} completato con successo.", InfoBarControl.BarType.Success, _theme);
        }
        catch (Exception ex)
        {
            _infoBar.ShowMessage($"Errore durante il rollback: {ex.Message}", InfoBarControl.BarType.Error, _theme);
        }
    }

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
        Directory.CreateDirectory(outDir);
        ToggleSettings(false);
        PerformLayout();
        LayoutToolbar();
        using (var bmp1 = RenderTree(this))
            bmp1.Save(Path.Combine(outDir, "norea_launcher_suite.png"), System.Drawing.Imaging.ImageFormat.Png);

        ToggleSettings(true);
        PerformLayout();
        using (var bmp2 = RenderTree(this))
            bmp2.Save(Path.Combine(outDir, "norea_launcher_settings.png"), System.Drawing.Imaging.ImageFormat.Png);

        QuitApplication();
    }

    Bitmap RenderTree(Control root)
    {
        var bmp = new Bitmap(root.Width, root.Height);
        void Walk(Control parent, Point origin, Rectangle clipRect)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)
            {
                var c = parent.Controls[i];
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                var pt = new Point(origin.X + c.Left, origin.Y + c.Top);
                var bounds = new Rectangle(pt.X, pt.Y, c.Width, c.Height);
                var visibleClip = Rectangle.Intersect(clipRect, bounds);
                if (visibleClip.Width <= 0 || visibleClip.Height <= 0) continue;

                using (var cb = new Bitmap(c.Width, c.Height))
                {
                    using (var cg = Graphics.FromImage(cb))
                    {
                        cg.Clear(c.BackColor);
                        InvokePaintBackground(c, new PaintEventArgs(cg, new Rectangle(0, 0, c.Width, c.Height)));
                        InvokePaint(c, new PaintEventArgs(cg, new Rectangle(0, 0, c.Width, c.Height)));
                        if (c is TextBox tb)
                        {
                            string t = string.IsNullOrEmpty(tb.Text) ? tb.PlaceholderText : tb.Text;
                            Color tc = string.IsNullOrEmpty(tb.Text) ? _theme.TextMuted : tb.ForeColor;
                            TextRenderer.DrawText(cg, t, tb.Font, new Point(2, (tb.Height - tb.Font.Height) / 2), tc, TextFormatFlags.NoPadding);
                        }
                        else if (c == _chipCtrlK)
                        {
                            TextRenderer.DrawText(cg, _chipCtrlK.Text, _chipCtrlK.Font, new Rectangle(0, 0, _chipCtrlK.Width, _chipCtrlK.Height), _chipCtrlK.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        }
                        else if (c is ComboBox cbx)
                        {
                            cg.Clear(cbx.BackColor);
                            using var pen = new Pen(_theme.BorderStrong);
                            cg.DrawRectangle(pen, 0, 0, cbx.Width - 1, cbx.Height - 1);
                            string text = cbx.SelectedItem?.ToString() ?? cbx.Text;
                            TextRenderer.DrawText(cg, text, cbx.Font, new Point(6, (cbx.Height - cbx.Font.Height) / 2), cbx.ForeColor, TextFormatFlags.NoPadding);
                            TextRenderer.DrawText(cg, "▾", cbx.Font, new Point(cbx.Width - 18, (cbx.Height - cbx.Font.Height) / 2), _theme.TextMuted, TextFormatFlags.NoPadding);
                        }
                    }
                    using var g = Graphics.FromImage(bmp);
                    g.SetClip(visibleClip);
                    g.DrawImageUnscaled(cb, pt);
                }
                if (c.HasChildren) Walk(c, pt, visibleClip);
            }
        }
        using (var g0 = Graphics.FromImage(bmp)) g0.Clear(root.BackColor);
        Walk(root, Point.Empty, new Rectangle(0, 0, root.Width, root.Height));
        return bmp;
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
