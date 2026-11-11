using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class AppDetailView : Panel
{
    AppEntry? _app;
    LauncherTheme _theme = LauncherTheme.Dark;

    readonly Panel _header = new() { Dock = DockStyle.Top, Height = 100 };
    readonly Panel _body = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Panel _actionsBar = new() { Dock = DockStyle.Bottom, Height = 64 };

    // Split button
    readonly Button _btnOpenMain = new() { Height = 36, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
    readonly Button _btnOpenSplit = new() { Width = 32, Height = 36, FlatStyle = FlatStyle.Flat, Text = "▾", Cursor = Cursors.Hand };
    readonly Button _btnAnalysis = new() { Height = 36, FlatStyle = FlatStyle.Flat, Text = "Report Analisi", Cursor = Cursors.Hand };
    readonly Button _btnMore = new() { Width = 36, Height = 36, FlatStyle = FlatStyle.Flat, Text = "···", Cursor = Cursors.Hand };

    public event EventHandler<AppEntry>? OpenRequested;
    public event EventHandler<AppEntry>? OpenAsAdminRequested;
    public event EventHandler<AppEntry>? OpenCustomArgsRequested;
    public event EventHandler<AppEntry>? StopProcessRequested;
    public event EventHandler<AppEntry>? AnalysisRequested;
    public event EventHandler<AppEntry>? ShowBinFolderRequested;
    public event EventHandler<AppEntry>? CleanCacheRequested;
    public event EventHandler<AppEntry>? VerifyIntegrityRequested;
    public event EventHandler<AppEntry>? RollbackRequested;

    public AppDetailView()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        SetupControls();
    }

    void SetupControls()
    {
        _header.Paint += OnHeaderPaint;
        _body.Paint += OnBodyPaint;
        _actionsBar.Paint += OnActionsBarPaint;

        _btnOpenMain.Click += (_, _) =>
        {
            if (_app == null) return;
            if (_app.IsRunning) StopProcessRequested?.Invoke(this, _app);
            else OpenRequested?.Invoke(this, _app);
        };

        _btnOpenSplit.Click += (_, _) => ShowSplitMenu();
        _btnAnalysis.Click += (_, _) => { if (_app != null) AnalysisRequested?.Invoke(this, _app); };
        _btnMore.Click += (_, _) => ShowMoreMenu();

        _actionsBar.Controls.AddRange([_btnOpenMain, _btnOpenSplit, _btnAnalysis, _btnMore]);
        _actionsBar.Resize += (_, _) => LayoutActions();

        Controls.Add(_body);
        Controls.Add(_actionsBar);
        Controls.Add(_header);
    }

    void LayoutActions()
    {
        int h = 36;
        int y = (_actionsBar.Height - h) / 2;
        int x = 24;

        _btnOpenMain.Size = new Size(130, h);
        _btnOpenMain.Location = new Point(x, y);

        _btnOpenSplit.Size = new Size(32, h);
        _btnOpenSplit.Location = new Point(x + 130, y);

        _btnAnalysis.Size = new Size(140, h);
        _btnAnalysis.Location = new Point(x + 130 + 32 + 12, y);

        _btnMore.Size = new Size(36, h);
        _btnMore.Location = new Point(_actionsBar.Width - 24 - 36, y);
    }

    public void SetApp(AppEntry? app, LauncherTheme theme)
    {
        _app = app;
        _theme = theme;
        ApplyVisualStyles();
        _header.Invalidate();
        _body.Invalidate();
        _actionsBar.Invalidate();
    }

    public void SetTheme(LauncherTheme theme)
    {
        _theme = theme;
        ApplyVisualStyles();
        Invalidate(true);
    }

    void ApplyVisualStyles()
    {
        LayoutActions();
        BackColor = _theme.Bg;
        _header.BackColor = _theme.CardBg;
        _body.BackColor = _theme.Bg;
        _actionsBar.BackColor = _theme.CardBg;

        bool isRunning = _app?.IsRunning == true;

        if (isRunning)
        {
            _btnOpenMain.Text = "In esecuzione (Stop)";
            _btnOpenMain.BackColor = _theme.StatusCoral;
            _btnOpenMain.ForeColor = Color.White;
        }
        else
        {
            _btnOpenMain.Text = _app?.IsInstalled == true ? "Apri" : "Installa";
            _btnOpenMain.BackColor = _theme.PrimaryBtnBg;
            _btnOpenMain.ForeColor = _theme.PrimaryBtnFg;
        }
        _btnOpenMain.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        _btnOpenSplit.BackColor = isRunning ? _theme.StatusCoral : _theme.SecondaryBtnBg;
        _btnOpenSplit.ForeColor = isRunning ? Color.White : _theme.SecondaryBtnFg;
        _btnOpenSplit.FlatAppearance.BorderColor = _theme.BorderStrong;

        _btnAnalysis.BackColor = _theme.SecondaryBtnBg;
        _btnAnalysis.ForeColor = _theme.SecondaryBtnFg;
        _btnAnalysis.FlatAppearance.BorderColor = _theme.BorderStrong;
        _btnAnalysis.Font = new Font("Segoe UI", 9f);

        _btnMore.BackColor = _theme.SecondaryBtnBg;
        _btnMore.ForeColor = _theme.SecondaryBtnFg;
        _btnMore.FlatAppearance.BorderColor = _theme.BorderStrong;
        _btnMore.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

        LayoutActions();
    }

    void OnHeaderPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.CardBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var borderPen = new Pen(_theme.Border);
        g.DrawLine(borderPen, 0, _header.Height - 1, _header.Width, _header.Height - 1);

        if (_app == null) return;

        // Icona
        var iconRect = new Rectangle(24, 20, 56, 56);
        GraphicsHelper.DrawAppIcon(g, iconRect, _app);

        // Titolo & Runtime & Version
        int tx = 96, ty = 22;
        using var titleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        TextRenderer.DrawText(g, _app.DisplayName, titleFont, new Point(tx, ty), _theme.TextPrimary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        var titleSize = TextRenderer.MeasureText(g, _app.DisplayName, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        // Version badge (sobrio e accessibile)
        var verColBg = _app.IsCanary ? _theme.VersionCanaryBg : _theme.VersionStableBg;
        var verColFg = _app.IsCanary ? _theme.VersionCanaryFg : _theme.VersionStableFg;
        using var monoFont = new Font("Consolas", 8.5f, FontStyle.Bold);
        GraphicsHelper.DrawPill(g, _app.Version, monoFont, new Point(tx + titleSize.Width + 12, ty + 6), verColBg, verColFg);

        // Runtime / sottotitolo: pulito senza ridondanze
        using var subFont = new Font("Segoe UI", 9.5f);
        string subtitle = _app.DisplayName == "NotepadREA"
            ? "Editor di testo e codice · .NET 10 (Scintilla/Lexilla)"
            : $"{_app.Tagline} · {_app.Runtime}";
        TextRenderer.DrawText(g, subtitle, subFont, new Point(tx, ty + 34), _theme.TextSecondary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    void OnBodyPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_app == null)
        {
            TextRenderer.DrawText(g, "Seleziona un'applicazione dalla barra laterale.", new Font("Segoe UI", 11f), new Point(24, 24), _theme.TextMuted);
            return;
        }

        int y = 20;
        int left = 24;
        int contentW = _body.Width - 48;

        // 1. Descrizione
        using var h2Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 9.5f);
        using var monoSmall = new Font("Consolas", 8.5f);

        TextRenderer.DrawText(g, "Descrizione", h2Font, new Point(left, y), _theme.TextPrimary);
        y += 24;
        var descRect = new Rectangle(left, y, contentW, 40);
        TextRenderer.DrawText(g, _app.Description, bodyFont, descRect, _theme.TextSecondary, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        y += 46;

        // 2. Scheda Informazioni Tecniche (Card)
        var infoCardRect = new RectangleF(left, y, contentW, 140);
        using var cardPath = GraphicsHelper.RoundRect(infoCardRect, 6f);
        using (var b = new SolidBrush(_theme.CardBg)) g.FillPath(b, cardPath);
        using (var p = new Pen(_theme.Border)) g.DrawPath(p, cardPath);

        int cy = y + 14;
        int cx = left + 16;
        void Row(string label, string val, bool isMono = false)
        {
            TextRenderer.DrawText(g, label, bodyFont, new Point(cx, cy), _theme.TextMuted);
            TextRenderer.DrawText(g, val, isMono ? monoSmall : bodyFont, new Point(cx + 195, cy), _theme.TextPrimary, TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);
            cy += 24;
        }

        string rawBinPath = _app.InstalledExePath ?? _app.DistExePath ?? "Non compilato";
        string sanitizedPath = AnonymizePath(rawBinPath);

        Row("Stato installazione:", _app.IsInstalled ? "Installato localmente" : "Pronto per l'installazione");
        Row("Percorso binario:", sanitizedPath, true);
        Row("Dimensione su disco:", $"{_app.EstimatedSizeMb:0.#} MB");
        Row("Ultimo aggiornamento:", $" {_app.LastUpdatedText.Trim()}");
        Row("Integrità binario:", !string.IsNullOrEmpty(_app.Sha256Short) ? $"SHA-256: {_app.Sha256Short}" : "Verifica integrità disponibile", true);
        y += 156;

        // 3. Esito Analisi Statica REA
        TextRenderer.DrawText(g, "Esito Analisi Statica (Notepad++ 8.9.8.1 Portable)", h2Font, new Point(left, y), _theme.TextPrimary);
        y += 24;

        var analysisBox = new RectangleF(left, y, contentW, 110);
        using var aPath = GraphicsHelper.RoundRect(analysisBox, 6f);
        using (var b = new SolidBrush(_theme.CardBg)) g.FillPath(b, aPath);
        using (var p = new Pen(_theme.Border)) g.DrawPath(p, aPath);

        int ay = y + 12;
        int ax = left + 16;
        void ARow(string k, string v)
        {
            TextRenderer.DrawText(g, k, monoSmall, new Point(ax, ay), _theme.BrandBlue);
            TextRenderer.DrawText(g, v, bodyFont, new Point(ax + 195, ay), _theme.TextSecondary);
            ay += 22;
        }

        ARow("Target analizzato:", "notepad++.exe (PE32+, AMD64, 8.5 MB, Authenticode firmato)");
        ARow("Motore individuato:", "Scintilla + Lexilla linkati staticamente (nessuna DLL esterna)");
        ARow("Hardening:", "Control Flow Guard attivo, Security Cookie /GS presente");
        ARow("Ricostruzione NOREA:", "C# .NET 10 WinForms con Scintilla5.NET (zero codice sorgente Notepad++)");
    }

    void OnActionsBarPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.CardBg);
        using var p = new Pen(_theme.Border);
        g.DrawLine(p, 0, 0, _actionsBar.Width, 0);
    }

    static string AnonymizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (path.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                return "%LocalAppData%" + path[localAppData.Length..];

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
                return "%AppData%" + path[appData.Length..];

            return "%UserProfile%" + path[userProfile.Length..];
        }
        return path;
    }

    void ShowSplitMenu()
    {
        if (_app == null) return;
        var cm = new ContextMenuStrip();
        cm.Items.Add("Esegui come Amministratore", null, (_, _) => OpenAsAdminRequested?.Invoke(this, _app));
        cm.Items.Add("Avvia con parametri custom...", null, (_, _) => OpenCustomArgsRequested?.Invoke(this, _app));
        cm.Show(_btnOpenSplit, new Point(0, _btnOpenSplit.Height));
    }

    void ShowMoreMenu()
    {
        if (_app == null) return;
        var cm = new ContextMenuStrip();

        cm.Items.Add("Mostra cartella binari", null, (_, _) => ShowBinFolderRequested?.Invoke(this, _app));
        cm.Items.Add("Verifica integrità file", null, (_, _) => VerifyIntegrityRequested?.Invoke(this, _app));
        cm.Items.Add("Pulisci cache e file temporanei", null, (_, _) => CleanCacheRequested?.Invoke(this, _app));

        if (_app.HasRollback)
        {
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Rollback alla versione precedente", null, (_, _) => RollbackRequested?.Invoke(this, _app));
        }

        cm.Show(_btnMore, new Point(0, _btnMore.Height));
    }
}
