using System.Diagnostics;

namespace NoreaLauncher;

public sealed class SettingsView : UserControl
{
    readonly LauncherSettings _settings;
    LauncherTheme _theme = LauncherTheme.Dark;

    readonly Panel _scrollHost = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Panel _canvas = new();
    readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 64 };

    // Controlli impostazioni
    readonly ComboBox _cbUpdateMode = MakeCombo(160);
    readonly ComboBox _cbInstallFormat = MakeCombo(250);
    readonly Panel _installFolderBox = new() { Height = 30, Padding = new Padding(6, 5, 6, 4) };
    readonly TextBox _tbInstallFolder = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.2f) };
    readonly ToolTip _toolTip = new();

    readonly CheckBox _chkAutoInstall = new() { AutoSize = true, Text = "Installa automaticamente gli aggiornamenti disponibili", UseMnemonic = false };
    readonly CheckBox _chkNotify = new() { AutoSize = true, Text = "Mostra notifica quando è disponibile un aggiornamento", UseMnemonic = false };
    readonly CheckBox _chkHidePlanned = new() { AutoSize = true, Text = "Nascondi applicazioni pianificate non compilate", UseMnemonic = false };
    readonly CheckBox _chkTray = new() { AutoSize = true, Text = "Riduci nella barra delle applicazioni alla chiusura", UseMnemonic = false };
    readonly CheckBox _chkLogin = new() { AutoSize = true, Text = "Avvia NOREA Launcher all'accesso di Windows", UseMnemonic = false };

    readonly Button _btnSys = new(), _btnDark = new(), _btnLight = new();
    readonly Button _btnBrowse = new(), _btnDefaultFolder = new();
    readonly Button _btnOpenWs = new(), _btnEditCatalog = new(), _btnAddApp = new();
    readonly Button _btnQuit = new(), _btnDone = new();

    readonly List<Panel> _cards = [];
    readonly List<Label> _headers = [];
    readonly List<Label> _labels = [];
    readonly List<Label> _mutedLabels = [];

    public event EventHandler? DoneRequested;
    public event EventHandler? QuitRequested;
    public event EventHandler? ThemeChanged;
    public event EventHandler? AddAppRequested;
    public event EventHandler? CheckCloudUpdatesRequested;
    public event EventHandler? TestFlyoutRequested;
    public event EventHandler? SetupAiRequested;

    public SettingsView(LauncherSettings settings)
    {
        _settings = settings;
        Dock = DockStyle.Fill;

        _cbUpdateMode.Items.AddRange(["Automatico", "Manuale"]);
        _cbInstallFormat.Items.AddRange(["Portable (.exe) + Associazioni", "Portable (.exe)", "Collegamento Desktop + Portable"]);

        BuildLayout();
        LoadFromSettings();
    }

    static ComboBox MakeCombo(int width)
    {
        var cb = new ComboBox
        {
            Width = width,
            Height = 28,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        return cb;
    }

    void BuildLayout()
    {
        Controls.Add(_scrollHost);
        Controls.Add(_footer);

        var headerBox = new Panel { Location = new Point(28, 16), Size = new Size(500, 64) };
        headerBox.Paint += (_, e) =>
        {
            GraphicsHelper.DrawNoreaPrismLogo(e.Graphics, new RectangleF(0, 4, 38, 38), _theme.BrandBlue, _theme.BrandRed);
            using var fTitle = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var fSub = new Font("Consolas", 8.2f, FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, "Impostazioni", fTitle, new Point(48, 2), _theme.TextPrimary, TextFormatFlags.NoPadding);
            string lVer = typeof(SettingsView).Assembly.GetName().Version?.ToString(3) ?? "2.2.2";
            TextRenderer.DrawText(e.Graphics, $"NOREA LAUNCHER v{lVer}", fSub, new Point(50, 36), _theme.TextMuted, TextFormatFlags.NoPadding);
        };
        _canvas.Controls.Add(headerBox);

        int leftX = 28, rightX = 510, colW = 460;

        // --- COLONNA SINISTRA ---
        var cardUpd = MakeCard("AGGIORNAMENTI", leftX, 86, colW, 160);
        cardUpd.Controls.Add(MakeBodyLabel("Controllo aggiornamenti", 18, 44));
        _cbUpdateMode.Location = new Point(colW - 18 - _cbUpdateMode.Width, 40);
        _cbUpdateMode.SelectedIndexChanged += (_, _) => { _settings.CheckUpdatesMode = _cbUpdateMode.Text; _settings.Save(); };
        cardUpd.Controls.Add(_cbUpdateMode);

        _chkAutoInstall.Location = new Point(18, 80);
        _chkNotify.Location = new Point(18, 114);
        foreach (var chk in new[] { _chkAutoInstall, _chkNotify })
        {
            chk.CheckedChanged += (_, _) => SyncBooleans();
            cardUpd.Controls.Add(chk);
        }

        var cardApp = MakeCard("ASPETTO E TEMA", leftX, 260, colW, 134);
        SetupSegmentButton(_btnSys, "SISTEMA", 18, 44, 110, () => SetAppearance("System"));
        SetupSegmentButton(_btnDark, "SCURO", 134, 44, 85, () => SetAppearance("Dark"));
        SetupSegmentButton(_btnLight, "CHIARO", 225, 44, 90, () => SetAppearance("Light"));
        cardApp.Controls.AddRange([_btnSys, _btnDark, _btnLight]);

        _chkHidePlanned.Location = new Point(18, 92);
        _chkHidePlanned.CheckedChanged += (_, _) => SyncBooleans();
        cardApp.Controls.Add(_chkHidePlanned);

        var cardCloud = MakeCard("AGGIORNAMENTI CLOUD (GITHUB)", leftX, 408, colW, 160);
        cardCloud.Controls.Add(MakeMutedLabel("Repository GitHub per release e aggiornamenti:", 18, 36));

        var repoBox = new Panel { Location = new Point(18, 56), Size = new Size(colW - 36, 28), Padding = new Padding(6, 4, 6, 4) };
        var tbRepo = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Text = _settings.GitHubRepo, Font = new Font("Consolas", 9.2f) };
        tbRepo.TextChanged += (_, _) => { _settings.GitHubRepo = tbRepo.Text.Trim(); _settings.Save(); };
        repoBox.Controls.Add(tbRepo);
        repoBox.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.BorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, repoBox.Width - 1, repoBox.Height - 1);
        };
        cardCloud.Controls.Add(repoBox);

        var chkCloudStartup = new CheckBox
        {
            Location = new Point(18, 92),
            AutoSize = true,
            Text = "Verifica aggiornamenti cloud all'avvio",
            Checked = _settings.CheckCloudUpdatesOnStartup
        };
        chkCloudStartup.CheckedChanged += (_, _) => { _settings.CheckCloudUpdatesOnStartup = chkCloudStartup.Checked; _settings.Save(); };
        cardCloud.Controls.Add(chkCloudStartup);

        var btnCheckCloud = new Button();
        SetupOutlineButton(btnCheckCloud, "VERIFICA ORA", 18, 118, 140, () => CheckCloudUpdatesRequested?.Invoke(this, EventArgs.Empty));
        cardCloud.Controls.Add(btnCheckCloud);

        var btnTestFlyout = new Button();
        SetupOutlineButton(btnTestFlyout, "PROVA FLYOUT NOTIFICA", 168, 118, 200, () => TestFlyoutRequested?.Invoke(this, EventArgs.Empty));
        cardCloud.Controls.Add(btnTestFlyout);

        var cardAi = MakeCard("INTEGRAZIONE AI (CLAUDE & ANTIGRAVITY)", leftX, 582, colW, 114);
        cardAi.Controls.Add(MakeMutedLabel("Collega NotepadREA e NoreaLauncher come strumenti per assistenti AI", 18, 38));
        var btnSetupAi = new Button();
        SetupPrimaryButton(btnSetupAi, "COLLEGA AD AI", 18, 64, 160, () => SetupAiRequested?.Invoke(this, EventArgs.Empty));
        cardAi.Controls.Add(btnSetupAi);

        // --- COLONNA DESTRA ---
        var cardFolder = MakeCard("CARTELLA DI INSTALLAZIONE", rightX, 86, colW, 194);
        cardFolder.Controls.Add(MakeBodyLabel("Cartella programmi predefinita", 18, 40));
        _installFolderBox.Location = new Point(18, 64);
        _installFolderBox.Width = colW - 36;
        _installFolderBox.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.BorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, _installFolderBox.Width - 1, _installFolderBox.Height - 1);
        };
        _tbInstallFolder.ReadOnly = true;
        _installFolderBox.Controls.Add(_tbInstallFolder);
        cardFolder.Controls.Add(_installFolderBox);

        cardFolder.Controls.Add(MakeMutedLabel("Le applicazioni della suite verranno installate qui.", 18, 100));
        SetupOutlineButton(_btnBrowse, "SFOGLIA...", 18, 124, 105, BrowseInstallFolder);
        SetupOutlineButton(_btnDefaultFolder, "PREDEFINITA", 128, 124, 130, () =>
        {
            _settings.InstallFolder = LauncherSettings.DefaultInstallFolder;
            _settings.Save();
            _tbInstallFolder.Text = LauncherUtil.AnonymizePath(_settings.InstallFolder);
            _toolTip.SetToolTip(_tbInstallFolder, _settings.InstallFolder);
        });

        var btnTogglePath = new Button { Height = 28 };
        void UpdatePathBtn()
        {
            bool isSet = LauncherUtil.IsPathConfigured();
            btnTogglePath.Text = isSet ? "✓ PATH ATTIVO" : "+ INTEGRA NEL PATH";
        }
        UpdatePathBtn();
        SetupOutlineButton(btnTogglePath, btnTogglePath.Text, 264, 124, 175, () =>
        {
            bool nowSet = LauncherUtil.IsPathConfigured();
            LauncherUtil.SetUserPathConfigured(!nowSet);
            UpdatePathBtn();
        });
        cardFolder.Controls.AddRange([_btnBrowse, _btnDefaultFolder, btnTogglePath]);
        cardFolder.Controls.Add(MakeMutedLabel("I file esistenti mantengono la loro posizione corrente.", 18, 162));

        var cardWin = MakeCard("FORMATO DI INSTALLAZIONE", rightX, 294, colW, 112);
        cardWin.Controls.Add(MakeBodyLabel("Tipo installazione", 18, 44));
        _cbInstallFormat.Location = new Point(colW - 18 - _cbInstallFormat.Width, 40);
        _cbInstallFormat.SelectedIndexChanged += (_, _) => { _settings.InstallFormat = _cbInstallFormat.Text; _settings.Save(); };
        cardWin.Controls.Add(_cbInstallFormat);
        cardWin.Controls.Add(MakeMutedLabel("Gli aggiornamenti preservano il formato configurato.", 18, 80));

        var cardBg = MakeCard("ESECUZIONE IN BACKGROUND", rightX, 420, colW, 110);
        _chkTray.Location = new Point(18, 44);
        _chkLogin.Location = new Point(18, 74);
        _chkTray.CheckedChanged += (_, _) => SyncBooleans();
        _chkLogin.CheckedChanged += (_, _) => { SyncBooleans(); _settings.ApplyStartupRegistry(); };
        cardBg.Controls.AddRange([_chkTray, _chkLogin]);

        var cardCommunity = MakeCard("CATALOGO E AREA DI LAVORO", rightX, 544, colW, 112);
        cardCommunity.Controls.Add(MakeMutedLabel("Gestione progetti analizzati e catalogo della suite", 18, 40));
        SetupPrimaryButton(_btnAddApp, "NUOVA APP", 18, 64, 125, () => AddAppRequested?.Invoke(this, EventArgs.Empty));
        SetupOutlineButton(_btnOpenWs, "WORKSPACE", 152, 64, 125, () =>
        {
            var ws = _settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
            if (Directory.Exists(ws)) Process.Start("explorer.exe", ws);
        });
        SetupOutlineButton(_btnEditCatalog, "CATALOGO", 286, 64, 125, () =>
        {
            var cat = CatalogService.GetCatalogPath(_settings);
            var notepadRea = Path.Combine(_settings.WorkspaceDir ?? "", "dist", "NotepadREA.exe");
            if (File.Exists(notepadRea)) Process.Start(notepadRea, $"\"{cat}\"");
            else Process.Start("notepad.exe", $"\"{cat}\"");
        });
        cardCommunity.Controls.AddRange([_btnAddApp, _btnOpenWs, _btnEditCatalog]);

        _canvas.Size = new Size(1000, 720);
        _scrollHost.Controls.Add(_canvas);
        _scrollHost.Resize += (_, _) =>
        {
            int x = Math.Max(0, (_scrollHost.ClientSize.Width - _canvas.Width) / 2);
            _canvas.Location = new Point(x, _scrollHost.AutoScrollPosition.Y);
        };

        // --- FOOTER BAR ---
        _footer.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.Border);
            e.Graphics.DrawLine(p, 0, 0, _footer.Width, 0);
        };

        SetupOutlineButton(_btnQuit, "ESCI", 28, 14, 100, () => QuitRequested?.Invoke(this, EventArgs.Empty));
        SetupPrimaryButton(_btnDone, "FATTO", 0, 14, 100, () => DoneRequested?.Invoke(this, EventArgs.Empty));
        _footer.Controls.AddRange([_btnQuit, _btnDone]);
        _footer.Resize += (_, _) => _btnDone.Location = new Point(_footer.Width - 28 - _btnDone.Width, 14);

        ApplyVisualStyles();
    }

    Panel MakeCard(string title, int x, int y, int w, int h)
    {
        var p = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h)
        };
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(p, true);
        p.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(_theme.CardBg);
            using var pen = new Pen(_theme.Border);
            g.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };

        var lbl = new Label
        {
            Text = title,
            Font = new Font("Consolas", 8.2f, FontStyle.Bold),
            ForeColor = _theme.TextMuted,
            Location = new Point(18, 16),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        p.Controls.Add(lbl);
        _headers.Add(lbl);
        _cards.Add(p);
        _canvas.Controls.Add(p);
        return p;
    }

    Label MakeBodyLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9.2f),
            ForeColor = _theme.TextPrimary,
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _labels.Add(lbl);
        return lbl;
    }

    Label MakeMutedLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = _theme.TextMuted,
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _mutedLabels.Add(lbl);
        return lbl;
    }

    void SetupSegmentButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    void SetupOutlineButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    void SetupPrimaryButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    bool _loading;

    public void LoadFromSettings()
    {
        _loading = true;
        _cbUpdateMode.SelectedItem = _settings.CheckUpdatesMode == "Manuale" ? "Manuale" : "Automatico";
        _cbInstallFormat.SelectedItem = _cbInstallFormat.Items.Contains(_settings.InstallFormat) ? _settings.InstallFormat : _cbInstallFormat.Items[0];
        _tbInstallFolder.Text = LauncherUtil.AnonymizePath(_settings.InstallFolder);
        _toolTip.SetToolTip(_tbInstallFolder, _settings.InstallFolder);

        _chkAutoInstall.Checked = _settings.AutoInstallUpdates;
        _chkNotify.Checked = _settings.NotifyUpdates;
        _chkHidePlanned.Checked = _settings.HidePlannedApps;
        _chkTray.Checked = _settings.KeepInTray;
        _chkLogin.Checked = _settings.StartOnLogin;
        _loading = false;
        UpdateAppearanceButtons();
    }

    void SyncBooleans()
    {
        if (_loading) return;
        _settings.AutoInstallUpdates = _chkAutoInstall.Checked;
        _settings.NotifyUpdates = _chkNotify.Checked;
        _settings.HidePlannedApps = _chkHidePlanned.Checked;
        _settings.KeepInTray = _chkTray.Checked;
        _settings.StartOnLogin = _chkLogin.Checked;
        _settings.Save();
    }

    void SetAppearance(string mode)
    {
        _settings.Appearance = mode;
        _settings.Save();
        UpdateAppearanceButtons();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    void BrowseInstallFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Seleziona la cartella di installazione delle applicazioni NOREA",
            SelectedPath = _settings.InstallFolder,
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            _settings.InstallFolder = dlg.SelectedPath;
            _settings.Save();
            _tbInstallFolder.Text = LauncherUtil.AnonymizePath(_settings.InstallFolder);
            _toolTip.SetToolTip(_tbInstallFolder, _settings.InstallFolder);
        }
    }

    void UpdateAppearanceButtons()
    {
        string cur = _settings.Appearance;
        StyleSegment(_btnSys, cur == "System");
        StyleSegment(_btnDark, cur == "Dark");
        StyleSegment(_btnLight, cur == "Light");
    }

    void StyleSegment(Button b, bool selected)
    {
        b.BackColor = selected ? _theme.PrimaryBtnBg : _theme.SecondaryBtnBg;
        b.ForeColor = selected ? _theme.PrimaryBtnFg : _theme.SecondaryBtnFg;
        b.FlatAppearance.BorderColor = selected ? _theme.PrimaryBtnBg : _theme.BorderStrong;
    }

    public void SetTheme(LauncherTheme theme)
    {
        _theme = theme;
        ApplyVisualStyles();
    }

    void ApplyVisualStyles()
    {
        var t = _theme;
        BackColor = t.Bg;
        _scrollHost.BackColor = t.Bg;
        _canvas.BackColor = t.Bg;
        _footer.BackColor = t.CardBg;

        foreach (var p in _cards) { p.BackColor = t.CardBg; p.Invalidate(); }
        foreach (var h in _headers) h.ForeColor = t.TextMuted;
        foreach (var l in _labels) l.ForeColor = t.TextPrimary;
        foreach (var m in _mutedLabels) m.ForeColor = t.TextMuted;

        foreach (var cb in new[] { _cbUpdateMode, _cbInstallFormat })
        {
            cb.BackColor = t.InputBg;
            cb.ForeColor = t.TextPrimary;
        }

        foreach (var chk in new[] { _chkAutoInstall, _chkNotify, _chkHidePlanned, _chkTray, _chkLogin })
        {
            chk.ForeColor = t.TextPrimary;
            chk.BackColor = Color.Transparent;
        }

        _installFolderBox.BackColor = t.InputBg;
        _tbInstallFolder.BackColor = t.InputBg;
        _tbInstallFolder.ForeColor = t.TextPrimary;

        foreach (var ob in new[] { _btnBrowse, _btnDefaultFolder, _btnOpenWs, _btnEditCatalog, _btnQuit })
        {
            ob.BackColor = t.SecondaryBtnBg;
            ob.ForeColor = t.SecondaryBtnFg;
            ob.FlatAppearance.BorderColor = t.BorderStrong;
            ob.FlatAppearance.MouseOverBackColor = t.SecondaryBtnHoverBg;
        }

        foreach (var pb in new[] { _btnAddApp, _btnDone })
        {
            pb.BackColor = t.PrimaryBtnBg;
            pb.ForeColor = t.PrimaryBtnFg;
        }

        UpdateAppearanceButtons();
        Invalidate(true);
    }
}
