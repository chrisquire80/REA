using System.Diagnostics;

namespace NoreaLauncher;

public sealed class SettingsView : Panel
{
    readonly LauncherSettings _settings;
    LauncherTheme _theme = LauncherTheme.Dark;

    readonly Panel _scrollHost = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Panel _canvas = new() { Location = new Point(0, 0), Size = new Size(980, 710) };
    readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 64 };

    readonly ComboBox _cbLang = MakeCombo(380);
    readonly ComboBox _cbUpdateMode = MakeCombo(155);
    readonly ComboBox _cbLauncherVer = MakeCombo(195);
    readonly ComboBox _cbInstallFormat = MakeCombo(225);
    readonly Panel _installFolderBox = new() { Height = 30, Padding = new Padding(6, 5, 6, 4) };
    readonly TextBox _tbInstallFolder = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.2f) };

    readonly CheckBox _chkAutoInstall = new() { AutoSize = true, Text = "Automatically build and install updates", UseMnemonic = false };
    readonly CheckBox _chkAskOnce = new() { AutoSize = true, Text = "Ask for permission once when updating all apps", UseMnemonic = false };
    readonly CheckBox _chkNotify = new() { AutoSize = true, Text = "Notify me when an installed app has an update", UseMnemonic = false };
    readonly CheckBox _chkPreRelease = new() { AutoSize = true, Text = "Include pre-releases (betas and release candidates)", UseMnemonic = false };
    readonly CheckBox _chkHidePlanned = new() { AutoSize = true, Text = "Hide planned / unbuilt suite apps", UseMnemonic = false };
    readonly CheckBox _chkTray = new() { AutoSize = true, Text = "Keep running in the tray when the window is closed", UseMnemonic = false };
    readonly CheckBox _chkLogin = new() { AutoSize = true, Text = "Start NOREA Launcher when I log in", UseMnemonic = false };

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

    public SettingsView(LauncherSettings settings)
    {
        _settings = settings;
        Dock = DockStyle.Fill;

        _cbLang.Items.AddRange(["Auto", "Italiano", "English"]);
        _cbUpdateMode.Items.AddRange(["Automatically", "Manually"]);
        _cbLauncherVer.Items.AddRange(["v1.0.0 (latest release)"]);
        _cbInstallFormat.Items.AddRange(["Portable (.exe) + Associazioni", "Portable (.exe)", "Collegamento Desktop + Portable"]);

        BuildLayout();
        LoadFromSettings();
    }

    static ComboBox MakeCombo(int width)
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22,
            Width = width,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
        };
        return cb;
    }

    void BindOwnerDrawCombo(ComboBox cb)
    {
        cb.DrawItem += (_, e) =>
        {
            e.Graphics.Clear(_theme.InputBg);
            if (e.Index >= 0 && e.Index < cb.Items.Count)
            {
                var txt = cb.Items[e.Index]?.ToString() ?? "";
                TextRenderer.DrawText(e.Graphics, txt, cb.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height),
                    _theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            if ((e.State & DrawItemState.ComboBoxEdit) != 0)
            {
                TextRenderer.DrawText(e.Graphics, "▾", cb.Font, new Rectangle(cb.Width - 22, 0, 18, cb.Height),
                    _theme.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
        cb.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.BorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, cb.Width - 1, cb.Height - 1);
        };
    }

    void BuildLayout()
    {
        foreach (var cb in new[] { _cbLang, _cbUpdateMode, _cbLauncherVer, _cbInstallFormat })
            BindOwnerDrawCombo(cb);

        var headerBox = new Panel { Location = new Point(28, 16), Size = new Size(500, 56) };
        headerBox.Paint += (_, e) =>
        {
            GraphicsHelper.DrawNoreaPrismLogo(e.Graphics, new RectangleF(0, 6, 40, 40), _theme.BrandBlue, _theme.BrandRed);
            using var fTitle = new Font("Segoe UI", 17f, FontStyle.Bold);
            using var fSub = new Font("Consolas", 8.5f, FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, "Settings", fTitle, new Point(50, 2), _theme.TextPrimary, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, "NOREA LAUNCHER 1.0.0", fSub, new Point(52, 34), _theme.TextMuted, TextFormatFlags.NoPadding);
        };
        _canvas.Controls.Add(headerBox);

        int leftX = 28, rightX = 500, colW = 450;

        // --- COLONNA SINISTRA ---
        var cardLang = MakeCard("LANGUAGE", leftX, 86, colW, 92);
        _cbLang.Location = new Point(18, 44);
        _cbLang.Width = colW - 36;
        _cbLang.SelectedIndexChanged += (_, _) => { _settings.Language = _cbLang.Text; _settings.Save(); };
        cardLang.Controls.Add(_cbLang);

        var cardUpd = MakeCard("UPDATES", leftX, 194, colW, 256);
        cardUpd.Controls.Add(MakeBodyLabel("Check for updates", 18, 48));
        _cbUpdateMode.Location = new Point(colW - 18 - _cbUpdateMode.Width, 44);
        _cbUpdateMode.SelectedIndexChanged += (_, _) => { _settings.CheckUpdatesMode = _cbUpdateMode.Text; _settings.Save(); };
        cardUpd.Controls.Add(_cbUpdateMode);

        _chkAutoInstall.Location = new Point(18, 84);
        _chkAskOnce.Location = new Point(18, 114);
        _chkNotify.Location = new Point(18, 144);
        _chkPreRelease.Location = new Point(18, 174);
        foreach (var chk in new[] { _chkAutoInstall, _chkAskOnce, _chkNotify, _chkPreRelease })
        {
            chk.CheckedChanged += (_, _) => SyncBooleans();
            cardUpd.Controls.Add(chk);
        }

        cardUpd.Controls.Add(MakeBodyLabel("Launcher version", 18, 212));
        _cbLauncherVer.Location = new Point(colW - 18 - _cbLauncherVer.Width, 208);
        cardUpd.Controls.Add(_cbLauncherVer);

        var cardApp = MakeCard("APPEARANCE", leftX, 466, colW, 134);
        SetupSegmentButton(_btnSys, "MATCH SYSTEM", 18, 44, 128, () => SetAppearance("System"));
        SetupSegmentButton(_btnDark, "DARK", 146, 44, 72, () => SetAppearance("Dark"));
        SetupSegmentButton(_btnLight, "LIGHT", 218, 44, 74, () => SetAppearance("Light"));
        cardApp.Controls.AddRange([_btnSys, _btnDark, _btnLight]);

        _chkHidePlanned.Location = new Point(18, 92);
        _chkHidePlanned.CheckedChanged += (_, _) => SyncBooleans();
        cardApp.Controls.Add(_chkHidePlanned);

        // --- COLONNA DESTRA ---
        var cardFolder = MakeCard("DEFAULT INSTALL FOLDER", rightX, 86, colW, 194);
        cardFolder.Controls.Add(MakeBodyLabel("Install folder", 18, 40));
        _installFolderBox.Location = new Point(18, 64);
        _installFolderBox.Width = colW - 36;
        _installFolderBox.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.BorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, _installFolderBox.Width - 1, _installFolderBox.Height - 1);
        };
        _tbInstallFolder.TextChanged += (_, _) => { _settings.InstallFolder = _tbInstallFolder.Text.Trim(); _settings.Save(); };
        _installFolderBox.Controls.Add(_tbInstallFolder);
        cardFolder.Controls.Add(_installFolderBox);

        cardFolder.Controls.Add(MakeBodyLabel("Apps are placed inside this folder.", 18, 100));
        SetupOutlineButton(_btnBrowse, "BROWSE...", 18, 124, 102, BrowseInstallFolder);
        SetupOutlineButton(_btnDefaultFolder, "USE DEFAULT", 132, 124, 118, () =>
        {
            _tbInstallFolder.Text = LauncherSettings.DefaultInstallFolder;
            _settings.InstallFolder = LauncherSettings.DefaultInstallFolder;
            _settings.Save();
        });
        cardFolder.Controls.AddRange([_btnBrowse, _btnDefaultFolder]);
        cardFolder.Controls.Add(MakeMutedLabel("New installs use this folder. Updates stay in their current location.", 18, 162));

        var cardWin = MakeCard("INSTALLING ON WINDOWS", rightX, 294, colW, 112);
        cardWin.Controls.Add(MakeBodyLabel("Install new apps as", 18, 44));
        _cbInstallFormat.Location = new Point(colW - 18 - _cbInstallFormat.Width, 40);
        _cbInstallFormat.SelectedIndexChanged += (_, _) => { _settings.InstallFormat = _cbInstallFormat.Text; _settings.Save(); };
        cardWin.Controls.Add(_cbInstallFormat);
        cardWin.Controls.Add(MakeMutedLabel("Updates always use the format an app was installed with.", 18, 80));

        var cardBg = MakeCard("RUNNING IN THE BACKGROUND", rightX, 420, colW, 110);
        _chkTray.Location = new Point(18, 44);
        _chkLogin.Location = new Point(18, 74);
        _chkTray.CheckedChanged += (_, _) => SyncBooleans();
        _chkLogin.CheckedChanged += (_, _) => { SyncBooleans(); _settings.ApplyStartupRegistry(); };
        cardBg.Controls.AddRange([_chkTray, _chkLogin]);

        var cardCommunity = MakeCard("NOREA WORKSPACE & CATALOG", rightX, 544, colW, 112);
        cardCommunity.Controls.Add(MakeMutedLabel("Manage reverse-engineered projects and catalog entries", 18, 40));
        SetupPrimaryButton(_btnAddApp, "+ ADD APP", 18, 64, 120, () => AddAppRequested?.Invoke(this, EventArgs.Empty));
        SetupOutlineButton(_btnOpenWs, "WORKSPACE", 148, 64, 116, () =>
        {
            var ws = _settings.WorkspaceDir ?? LauncherSettings.DetectWorkspaceDir();
            if (Directory.Exists(ws)) Process.Start("explorer.exe", ws);
        });
        SetupOutlineButton(_btnEditCatalog, "CATALOG.JSON", 274, 64, 130, () =>
        {
            var cat = CatalogService.GetCatalogPath(_settings);
            var notepadRea = Path.Combine(_settings.WorkspaceDir ?? "", "dist", "NotepadREA.exe");
            if (File.Exists(notepadRea)) Process.Start(notepadRea, $"\"{cat}\"");
            else Process.Start("notepad.exe", $"\"{cat}\"");
        });
        cardCommunity.Controls.AddRange([_btnAddApp, _btnOpenWs, _btnEditCatalog]);

        _canvas.Size = new Size(980, 668);
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
        SetupOutlineButton(_btnQuit, "QUIT NOREA LAUNCHER", 28, 16, 200, () => QuitRequested?.Invoke(this, EventArgs.Empty));
        SetupPrimaryButton(_btnDone, "DONE", _footer.Width - 118, 16, 90, () => DoneRequested?.Invoke(this, EventArgs.Empty));
        _footer.Resize += (_, _) => _btnDone.Location = new Point(_footer.Width - 118, 16);
        _footer.Controls.AddRange([_btnQuit, _btnDone]);

        // Ordine corretto di docking: Fill per primo, Bottom dopo
        Controls.Add(_scrollHost);
        Controls.Add(_footer);
    }

    Panel MakeCard(string headerText, int x, int y, int w, int h)
    {
        var p = new Panel { Location = new Point(x, y), Size = new Size(w, h) };
        p.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        var hdr = new Label
        {
            Text = headerText,
            Location = new Point(16, 14),
            AutoSize = true,
            UseMnemonic = false,
            Font = new Font("Consolas", 8.5f, FontStyle.Bold),
        };
        _headers.Add(hdr);
        p.Controls.Add(hdr);
        _cards.Add(p);
        _canvas.Controls.Add(p);
        return p;
    }

    Label MakeBodyLabel(string text, int x, int y)
    {
        var l = new Label { Text = text, Location = new Point(x, y), AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", 9.2f) };
        _labels.Add(l);
        return l;
    }

    Label MakeMutedLabel(string text, int x, int y)
    {
        var l = new Label { Text = text, Location = new Point(x, y), AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", 8.2f) };
        _mutedLabels.Add(l);
        return l;
    }

    static void SetupSegmentButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.UseMnemonic = false;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Consolas", 8.2f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    static void SetupOutlineButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.UseMnemonic = false;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Consolas", 8.2f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    static void SetupPrimaryButton(Button b, string text, int x, int y, int w, Action onClick)
    {
        b.Text = text;
        b.UseMnemonic = false;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Consolas", 8.2f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += (_, _) => onClick();
    }

    bool _loading;

    public void LoadFromSettings()
    {
        _loading = true;
        _cbLang.SelectedItem = _cbLang.Items.Contains(_settings.Language) ? _settings.Language : "Auto";
        _cbUpdateMode.SelectedItem = _cbUpdateMode.Items.Contains(_settings.CheckUpdatesMode) ? _settings.CheckUpdatesMode : "Automatically";
        _cbLauncherVer.SelectedIndex = 0;
        _cbInstallFormat.SelectedItem = _cbInstallFormat.Items.Contains(_settings.InstallFormat) ? _settings.InstallFormat : _cbInstallFormat.Items[0];
        _tbInstallFolder.Text = _settings.InstallFolder;

        _chkAutoInstall.Checked = _settings.AutoInstallUpdates;
        _chkAskOnce.Checked = _settings.AskPermissionOnce;
        _chkNotify.Checked = _settings.NotifyUpdates;
        _chkPreRelease.Checked = _settings.IncludePreReleases;
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
        _settings.AskPermissionOnce = _chkAskOnce.Checked;
        _settings.NotifyUpdates = _chkNotify.Checked;
        _settings.IncludePreReleases = _chkPreRelease.Checked;
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
            Description = "Scegli la cartella predefinita per le applicazioni NOREA",
            SelectedPath = _settings.InstallFolder,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            _tbInstallFolder.Text = dlg.SelectedPath;
            _settings.InstallFolder = dlg.SelectedPath;
            _settings.Save();
        }
    }

    public void SetTheme(LauncherTheme t)
    {
        _theme = t;
        BackColor = t.Bg;
        _scrollHost.BackColor = t.Bg;
        _canvas.BackColor = t.Bg;
        _footer.BackColor = t.HeaderBg;

        foreach (var c in _cards) { c.BackColor = t.CardBg; c.Invalidate(); }
        foreach (var h in _headers) h.ForeColor = t.TextMuted;
        foreach (var l in _labels) l.ForeColor = t.TextPrimary;
        foreach (var m in _mutedLabels) m.ForeColor = t.TextMuted;

        foreach (var chk in new[] { _chkAutoInstall, _chkAskOnce, _chkNotify, _chkPreRelease, _chkHidePlanned, _chkTray, _chkLogin })
        {
            chk.ForeColor = t.TextPrimary;
            chk.BackColor = t.CardBg;
        }

        foreach (var cb in new[] { _cbLang, _cbUpdateMode, _cbLauncherVer, _cbInstallFormat })
        {
            cb.BackColor = t.InputBg;
            cb.ForeColor = t.TextPrimary;
            cb.Invalidate();
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

        foreach (var pb in new[] { _btnDone, _btnAddApp })
        {
            pb.BackColor = t.PrimaryBtnBg;
            pb.ForeColor = t.PrimaryBtnFg;
            pb.FlatAppearance.BorderSize = 0;
        }

        UpdateAppearanceButtons();
        Invalidate(true);
    }

    void UpdateAppearanceButtons()
    {
        void StyleSeg(Button b, bool active)
        {
            b.BackColor = active ? _theme.PrimaryBtnBg : _theme.SecondaryBtnBg;
            b.ForeColor = active ? _theme.PrimaryBtnFg : _theme.SecondaryBtnFg;
            b.FlatAppearance.BorderColor = _theme.BorderStrong;
        }
        StyleSeg(_btnSys, _settings.Appearance == "System");
        StyleSeg(_btnDark, _settings.Appearance == "Dark");
        StyleSeg(_btnLight, _settings.Appearance == "Light");
    }
}
