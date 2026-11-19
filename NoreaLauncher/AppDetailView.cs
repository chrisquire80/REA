using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class AppDetailView : Panel
{
    AppEntry? _app;
    LauncherTheme _theme = LauncherTheme.Dark;

    readonly Panel _header = new() { Dock = DockStyle.Top, Height = 112 };
    readonly Panel _body = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly Panel _actionsBar = new() { Dock = DockStyle.Bottom, Height = 72 };

    // Split button
    readonly Button _btnOpenMain = new() { Height = 36, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
    readonly Button _btnOpenSplit = new() { Width = 32, Height = 36, FlatStyle = FlatStyle.Flat, Text = "▾", Cursor = Cursors.Hand };
    readonly Button _btnAnalysis = new() { Height = 36, FlatStyle = FlatStyle.Flat, Text = "Report Analisi", Cursor = Cursors.Hand };
    readonly Button _btnMore = new() { Width = 36, Height = 36, FlatStyle = FlatStyle.Flat, Text = "", Cursor = Cursors.Hand };

    static readonly Font TitleFont = new("Segoe UI", 16f, FontStyle.Bold);
    static readonly Font SubtitleFont = new("Segoe UI", 9.5f);
    static readonly Font MonoBadgeFont = new("Consolas", 8.5f, FontStyle.Bold);
    static readonly Font H2Font = new("Segoe UI", 10.5f, FontStyle.Bold);
    static readonly Font BodyFont = new("Segoe UI", 9.5f);
    static readonly Font MonoSmallFont = new("Consolas", 8.5f);
    static readonly Font EmptyPromptFont = new("Segoe UI", 11f);
    static readonly Font BtnOpenFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    static readonly Font BtnAnalysisFont = new("Segoe UI", 9f);
    static readonly Font BtnMoreFont = new("Segoe UI", 10f, FontStyle.Bold);

    public event EventHandler<AppEntry>? OpenRequested;
    public event EventHandler<AppEntry>? InstallRequested;
    public event EventHandler<AppEntry>? UpdateRequested;
    public event EventHandler<AppEntry>? UninstallRequested;
    public event EventHandler<AppEntry>? OpenAsAdminRequested;
    public event EventHandler<AppEntry>? OpenCustomArgsRequested;
    public event EventHandler<AppEntry>? StopProcessRequested;
    public event EventHandler<AppEntry>? AnalysisRequested;
    public event EventHandler<AppEntry>? ShowBinFolderRequested;
    public event EventHandler<AppEntry>? CleanCacheRequested;
    public event EventHandler<AppEntry>? VerifyIntegrityRequested;
    public event EventHandler<AppEntry>? RollbackRequested;
    public event EventHandler<(AppEntry App, string FilePath)>? OpenRecentFileRequested;
    public event EventHandler<bool>? PathConfigChanged;

    private readonly System.Windows.Forms.Timer _telemetryTimer = new() { Interval = 1500 };

    public AppDetailView()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        SetupControls();

        _telemetryTimer.Tick += (_, _) =>
        {
            if (_app != null && _app.IsRunning && _body.Visible)
            {
                _body.Invalidate();
            }
        };
        _telemetryTimer.Start();
    }

    void SetupControls()
    {
        _header.Paint += OnHeaderPaint;
        _body.Paint += OnBodyPaint;
        _actionsBar.Paint += OnActionsBarPaint;

        _btnOpenMain.Click += (_, _) =>
        {
            if (_app == null || _app.IsOperating) return;
            if (_app.IsRunning) StopProcessRequested?.Invoke(this, _app);
            else if (_app.HasUpdate) UpdateRequested?.Invoke(this, _app);
            else if (_app.IsInstalled) OpenRequested?.Invoke(this, _app);
            else InstallRequested?.Invoke(this, _app);
        };

        _btnOpenSplit.Click += (_, _) => ShowSplitMenu();
        _btnAnalysis.Click += (_, _) => { if (_app != null) AnalysisRequested?.Invoke(this, _app); };
        _btnMore.Click += (_, _) => ShowMoreMenu();
        _btnMore.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_btnMore.ForeColor);
            int cy = _btnMore.Height / 2;
            int cx = _btnMore.Width / 2;
            int r = 2;
            g.FillEllipse(b, cx - 7 - r, cy - r, r * 2, r * 2);
            g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            g.FillEllipse(b, cx + 7 - r, cy - r, r * 2, r * 2);
        };

        _actionsBar.Controls.AddRange([_btnOpenMain, _btnOpenSplit, _btnAnalysis, _btnMore]);
        _actionsBar.Resize += (_, _) => LayoutActions();

        Controls.Add(_body);
        Controls.Add(_actionsBar);
        Controls.Add(_header);
    }

    void LayoutActions()
    {
        int h = 38;
        int y = (_actionsBar.Height - h) / 2;
        int x = 24;

        _btnOpenMain.Size = new Size(140, h);
        _btnOpenMain.Location = new Point(x, y);

        _btnOpenSplit.Size = new Size(36, h);
        _btnOpenSplit.Location = new Point(x + 140, y);

        _btnAnalysis.Size = new Size(148, h);
        _btnAnalysis.Location = new Point(x + 140 + 36 + 12, y);

        _btnMore.Size = new Size(40, h);
        _btnMore.Location = new Point(_actionsBar.Width - 24 - 40, y);
    }

    public void SetApp(AppEntry? app, LauncherTheme theme)
    {
        _app = app;
        _theme = theme;
        _body.AutoScrollPosition = Point.Empty;
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
        bool isOperating = _app?.IsOperating == true;

        if (isOperating)
        {
            _btnOpenMain.Text = "Installazione in corso...";
            _btnOpenMain.BackColor = _theme.SecondaryBtnBg;
            _btnOpenMain.ForeColor = _theme.TextMuted;
            _btnOpenMain.Enabled = false;
        }
        else if (isRunning)
        {
            _btnOpenMain.Text = "In esecuzione (Stop)";
            _btnOpenMain.BackColor = _theme.StatusCoral;
            _btnOpenMain.ForeColor = Color.White;
            _btnOpenMain.Enabled = true;
        }
        else if (_app?.HasUpdate == true)
        {
            _btnOpenMain.Text = "Aggiorna";
            _btnOpenMain.BackColor = _theme.BrandBlue;
            _btnOpenMain.ForeColor = Color.White;
            _btnOpenMain.Enabled = true;
        }
        else
        {
            _btnOpenMain.Text = _app?.IsInstalled == true ? "Apri" : "Installa";
            _btnOpenMain.BackColor = _theme.PrimaryBtnBg;
            _btnOpenMain.ForeColor = _theme.PrimaryBtnFg;
            _btnOpenMain.Enabled = true;
        }
        _btnOpenMain.Font = BtnOpenFont;
        _btnOpenMain.UseVisualStyleBackColor = false;
        _btnOpenMain.FlatAppearance.BorderSize = 0;
        _btnOpenMain.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_btnOpenMain.BackColor, .1f);
        _btnOpenMain.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(_btnOpenMain.BackColor, .08f);

        _btnOpenSplit.BackColor = isRunning ? _theme.StatusCoral : _theme.SecondaryBtnBg;
        _btnOpenSplit.ForeColor = isRunning ? Color.White : _theme.SecondaryBtnFg;
        _btnOpenSplit.UseVisualStyleBackColor = false;
        _btnOpenSplit.FlatAppearance.BorderColor = _theme.BorderStrong;

        _btnAnalysis.BackColor = _theme.SecondaryBtnBg;
        _btnAnalysis.ForeColor = _theme.SecondaryBtnFg;
        _btnAnalysis.UseVisualStyleBackColor = false;
        _btnAnalysis.FlatAppearance.BorderColor = _theme.BorderStrong;
        _btnAnalysis.Font = BtnAnalysisFont;

        _btnMore.BackColor = _theme.SecondaryBtnBg;
        _btnMore.ForeColor = _theme.SecondaryBtnFg;
        _btnMore.UseVisualStyleBackColor = false;
        _btnMore.FlatAppearance.BorderColor = _theme.BorderStrong;
        _btnMore.Font = BtnMoreFont;

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
        var iconRect = new Rectangle(28, 24, 60, 60);
        GraphicsHelper.DrawAppIcon(g, iconRect, _app);

        // Titolo & Runtime & Version
        int tx = 104, ty = 26;
        TextRenderer.DrawText(g, _app.DisplayName, TitleFont, new Point(tx, ty), _theme.TextPrimary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        var titleSize = TextRenderer.MeasureText(g, _app.DisplayName, TitleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        // Version badge (sobrio e accessibile)
        var verColBg = _app.IsCanary ? _theme.VersionCanaryBg : _theme.VersionStableBg;
        var verColFg = _app.IsCanary ? _theme.VersionCanaryFg : _theme.VersionStableFg;
        GraphicsHelper.DrawPill(g, _app.Version, MonoBadgeFont, new Point(tx + titleSize.Width + 12, ty + 6), verColBg, verColFg);

        // Runtime / sottotitolo: pulito senza ridondanze
        string subtitle = _app.DisplayName == "NotepadREA"
            ? "Editor di testo e codice · .NET 10 (Scintilla/Lexilla)"
            : $"{_app.Tagline} · {_app.Runtime}";
        TextRenderer.DrawText(g, subtitle, SubtitleFont, new Point(tx, ty + 34), _theme.TextSecondary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    void OnBodyPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_app == null)
        {
            _body.AutoScrollMinSize = Size.Empty;
            TextRenderer.DrawText(g, "Seleziona un'applicazione dalla barra laterale.", EmptyPromptFont, new Point(24, 24), _theme.TextMuted);
            return;
        }

        int scrollX = _body.AutoScrollPosition.X;
        int scrollY = _body.AutoScrollPosition.Y;
        int y = 24 + scrollY;
        int left = 28 + scrollX;
        int contentW = Math.Max(1, _body.ClientSize.Width - 56);

        // Telemetria Live se in esecuzione
        if (_app.IsRunning)
        {
            var telem = LauncherUtil.GetProcessTelemetry(_app.RunningPid);
            if (telem.HasValue)
            {
                var telemRect = new RectangleF(left, y, contentW, 62);
                using var tPath = GraphicsHelper.RoundRect(telemRect, 6f);
                using (var b = new SolidBrush(_theme.CardBg)) g.FillPath(b, tPath);
                using (var p = new Pen(_theme.Border)) g.DrawPath(p, tPath);
                using (var accent = new SolidBrush(_theme.StatusGreen)) g.FillRectangle(accent, left, y + 9, 3, 44);

                int ty = y + 10;
                int tx = left + 18;
                TextRenderer.DrawText(g, $"IN ESECUZIONE · PID {_app.RunningPid}", MonoSmallFont, new Point(tx, ty), _theme.StatusGreen);

                string stats = $"RAM: {telem.Value.RamMb} MB   |   Uptime: {telem.Value.Uptime.Hours:D2}:{telem.Value.Uptime.Minutes:D2}:{telem.Value.Uptime.Seconds:D2}   |   Thread: {telem.Value.ThreadCount}";
                TextRenderer.DrawText(g, stats, BodyFont, new Point(tx, ty + 20), _theme.TextPrimary);

                y += 76;
            }
        }

        // 1. Descrizione
        TextRenderer.DrawText(g, "Descrizione", H2Font, new Point(left, y), _theme.TextPrimary);
        y += 28;
        var descRect = new Rectangle(left, y, contentW, 44);
        TextRenderer.DrawText(g, _app.Description, BodyFont, descRect, _theme.TextSecondary, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        y += 52;

        // 2. Scheda Informazioni Tecniche (Card)
        var infoCardRect = new RectangleF(left, y, contentW, 148);
        using var cardPath = GraphicsHelper.RoundRect(infoCardRect, 6f);
        using (var b = new SolidBrush(_theme.CardBg)) g.FillPath(b, cardPath);
        using (var p = new Pen(_theme.Border)) g.DrawPath(p, cardPath);

        int cy = y + 15;
        int cx = left + 18;
        void Row(string label, string val, bool isMono = false)
        {
            TextRenderer.DrawText(g, label, BodyFont, new Point(cx, cy), _theme.TextMuted);
            TextRenderer.DrawText(g, val, isMono ? MonoSmallFont : BodyFont, new Point(cx + 205, cy), _theme.TextPrimary, TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);
            cy += 25;
        }

        string rawBinPath = _app.InstalledExePath ?? _app.DistExePath ?? "Non compilato";
        string sanitizedPath = LauncherUtil.AnonymizePath(rawBinPath);

        Row("Stato installazione:", _app.IsInstalled ? "Installato localmente" : "Pronto per l'installazione");
        Row("Percorso binario:", sanitizedPath, true);
        Row("Dimensione su disco:", $"{_app.EstimatedSizeMb:0.#} MB");
        Row("Ultimo aggiornamento:", $" {_app.LastUpdatedText.Trim()}");
        Row("Integrità binario:", !string.IsNullOrEmpty(_app.Sha256Short) ? $"SHA-256: {_app.Sha256Short}" : "Verifica integrità disponibile", true);
        y += 164;

        // 3. Esito Analisi Statica (se presente nei metadati dell'app)
        if (_app.Analysis != null && _app.Analysis.Rows.Count > 0)
        {
            y += 30;
            TextRenderer.DrawText(g, _app.Analysis.Title, H2Font, new Point(left, y), _theme.TextPrimary);
            y += 28;

            int boxH = Math.Max(70, _app.Analysis.Rows.Count * 23 + 20);
            var analysisBox = new RectangleF(left, y, contentW, boxH);
            using var aPath = GraphicsHelper.RoundRect(analysisBox, 6f);
            using (var b = new SolidBrush(_theme.CardBg)) g.FillPath(b, aPath);
            using (var p = new Pen(_theme.Border)) g.DrawPath(p, aPath);

            int ay = y + 12;
            int ax = left + 16;
            foreach (var row in _app.Analysis.Rows)
            {
                TextRenderer.DrawText(g, row.Label, MonoSmallFont, new Point(ax, ay), _theme.BrandBlue);
                TextRenderer.DrawText(g, row.Value, BodyFont, new Point(ax + 195, ay), _theme.TextSecondary);
                ay += 22;
            }

            y += boxH;
        }

        if (_app.UsageGuide is { } usageGuide)
        {
            y += 30;
            DrawUsageGuide(g, usageGuide, left, contentW, ref y);
        }

        int totalHeight = (y - scrollY) + 28;
        var desiredScrollSize = new Size(0, Math.Max(_body.ClientSize.Height, totalHeight));
        if (_body.AutoScrollMinSize != desiredScrollSize)
            _body.AutoScrollMinSize = desiredScrollSize;
    }

    void DrawUsageGuide(Graphics g, UsageGuide guide, int left, int contentWidth, ref int y)
    {
        TextRenderer.DrawText(g, guide.Title, H2Font, new Point(left, y), _theme.TextPrimary);
        y += 28;

        int innerWidth = Math.Max(1, contentWidth - 32);
        int codeWidth = Math.Max(1, innerWidth - 16);
        int introHeight = MeasureWrappedText(g, guide.Introduction, BodyFont, innerWidth);
        var measuredExamples = guide.Examples.Select(example => new
        {
            Example = example,
            CommandHeight = MeasureWrappedText(g, example.Command, MonoSmallFont, codeWidth),
            DescriptionHeight = MeasureWrappedText(g, example.Description, BodyFont, innerWidth)
        }).ToArray();
        var measuredNotes = guide.Notes
            .Select(note => (Text: note, Height: MeasureWrappedText(g, "• " + note, BodyFont, innerWidth)))
            .ToArray();

        int cardHeight = 16;
        if (introHeight > 0) cardHeight += introHeight + 14;
        foreach (var item in measuredExamples)
        {
            cardHeight += 22;
            if (item.CommandHeight > 0) cardHeight += item.CommandHeight + 18;
            if (item.DescriptionHeight > 0) cardHeight += item.DescriptionHeight + 10;
            cardHeight += 10;
        }
        if (measuredNotes.Length > 0)
        {
            cardHeight += 18;
            foreach (var note in measuredNotes) cardHeight += note.Height + 7;
        }
        cardHeight += 12;

        var cardRect = new RectangleF(left, y, contentWidth, cardHeight);
        using var cardPath = GraphicsHelper.RoundRect(cardRect, 6f);
        using (var brush = new SolidBrush(_theme.CardBg)) g.FillPath(brush, cardPath);
        using (var pen = new Pen(_theme.Border)) g.DrawPath(pen, cardPath);

        int innerX = left + 16;
        int drawY = y + 14;
        if (introHeight > 0)
        {
            TextRenderer.DrawText(g, guide.Introduction, BodyFont,
                new Rectangle(innerX, drawY, innerWidth, introHeight), _theme.TextSecondary,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            drawY += introHeight + 14;
        }

        foreach (var item in measuredExamples)
        {
            TextRenderer.DrawText(g, item.Example.Title, BodyFont, new Point(innerX, drawY), _theme.BrandBlue);
            drawY += 22;

            if (item.CommandHeight > 0)
            {
                var codeRect = new Rectangle(innerX, drawY, innerWidth, item.CommandHeight + 8);
                using var codePath = GraphicsHelper.RoundRect(codeRect, 4f);
                using (var brush = new SolidBrush(_theme.Bg)) g.FillPath(brush, codePath);
                using (var pen = new Pen(_theme.Border)) g.DrawPath(pen, codePath);
                TextRenderer.DrawText(g, item.Example.Command, MonoSmallFont,
                    new Rectangle(codeRect.X + 8, codeRect.Y + 4, codeWidth, item.CommandHeight), _theme.TextPrimary,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                drawY += item.CommandHeight + 18;
            }

            if (item.DescriptionHeight > 0)
            {
                TextRenderer.DrawText(g, item.Example.Description, BodyFont,
                    new Rectangle(innerX, drawY, innerWidth, item.DescriptionHeight), _theme.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                drawY += item.DescriptionHeight + 10;
            }

            drawY += 10;
        }

        if (measuredNotes.Length > 0)
        {
            using var separator = new Pen(_theme.Border);
            g.DrawLine(separator, innerX, drawY, innerX + innerWidth, drawY);
            drawY += 12;
            foreach (var note in measuredNotes)
            {
                TextRenderer.DrawText(g, "• " + note.Text, BodyFont,
                    new Rectangle(innerX, drawY, innerWidth, note.Height), _theme.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                drawY += note.Height + 7;
            }
        }

        y += cardHeight;
    }

    static int MeasureWrappedText(Graphics g, string text, Font font, int width)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return TextRenderer.MeasureText(g, text, font, new Size(Math.Max(1, width), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
    }

    void OnActionsBarPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.CardBg);
        using var p = new Pen(_theme.Border);
        g.DrawLine(p, 0, 0, _actionsBar.Width, 0);
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

        var recent = LauncherUtil.GetRecentFiles(_app.Id);
        if (recent.Count > 0)
        {
            var recentMenu = new ToolStripMenuItem("File Recenti");
            foreach (var r in recent)
            {
                string filePath = r;
                string display = Path.GetFileName(filePath) + " (" + LauncherUtil.AnonymizePath(Path.GetDirectoryName(filePath) ?? "") + ")";
                recentMenu.DropDownItems.Add(display, null, (_, _) =>
                {
                    OpenRecentFileRequested?.Invoke(this, (_app, filePath));
                });
            }
            cm.Items.Add(recentMenu);
            cm.Items.Add(new ToolStripSeparator());
        }

        cm.Items.Add("Mostra cartella binari", null, (_, _) => ShowBinFolderRequested?.Invoke(this, _app));
        cm.Items.Add("Copia percorso binario", null, (_, _) =>
        {
            var exe = _app.InstalledExePath ?? _app.DistExePath;
            if (!string.IsNullOrEmpty(exe)) Clipboard.SetText(exe);
        });
        cm.Items.Add("Verifica integrità file", null, (_, _) => VerifyIntegrityRequested?.Invoke(this, _app));
        cm.Items.Add("Pulisci cache e file temporanei", null, (_, _) => CleanCacheRequested?.Invoke(this, _app));

        cm.Items.Add(new ToolStripSeparator());
        bool isPathConfigured = LauncherUtil.IsPathConfigured();
        string pathText = isPathConfigured ? "Rimuovi dal PATH utente (Terminale CLI)" : "Aggiungi al PATH utente (Terminale CLI)";
        cm.Items.Add(pathText, null, (_, _) =>
        {
            bool ok = LauncherUtil.SetUserPathConfigured(!isPathConfigured);
            PathConfigChanged?.Invoke(this, ok);
        });

        if (_app.HasRollback)
        {
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Rollback alla versione precedente", null, (_, _) => RollbackRequested?.Invoke(this, _app));
        }

        if (_app.IsInstalled)
        {
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Disinstalla applicazione...", null, (_, _) => UninstallRequested?.Invoke(this, _app));
        }

        cm.Show(_btnMore, new Point(0, _btnMore.Height));
    }
}
