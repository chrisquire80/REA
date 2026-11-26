using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace NoreaLauncher;

/// <summary>
/// Flyout moderno di notifica aggiornamento in stile ArtCraft / Modern Dark.
/// Mostra logo app, badge UPDATE, confronto versioni (vOld to vNew),
/// spinner animato con stato download/installazione, link 'What's new' e pulsante 'DISMISS'.
/// </summary>
public sealed class UpdateNotificationFlyout : Form
{
    readonly string _appName;
    readonly string _author;
    readonly string _oldVersion;
    readonly string _newVersion;
    readonly string? _changelogUrl;
    readonly string? _changelogText;
    readonly Icon? _appIcon;
    readonly Color _accentColor;

    readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 25 };
    readonly System.Windows.Forms.Timer _autoCloseTimer = new() { Interval = 12000 };

    float _spinnerAngle = 0f;
    string _statusText = "Downloading and installing update...";
    bool _isCompleted = false;
    bool _hasError = false;
    int _progressPercent = -1;

    Rectangle _closeRect;
    Rectangle _whatsNewRect;
    Rectangle _dismissRect;
    bool _hoverClose;
    bool _hoverWhatsNew;
    bool _hoverDismiss;

    public event EventHandler? DismissClicked;
    public event EventHandler? WhatsNewClicked;

    public UpdateNotificationFlyout(
        string appName,
        string oldVersion,
        string newVersion,
        string author = "by NOREA",
        string? changelogUrl = null,
        string? changelogText = null,
        Icon? appIcon = null,
        Color? accentColor = null)
    {
        _appName = appName;
        _author = author;
        _changelogUrl = changelogUrl;
        string loadedNotes = ReleaseNotes.Load();
        var published = ReleaseNotes.LatestPair(loadedNotes);
        if (string.IsNullOrWhiteSpace(changelogText))
        {
            string forVersion = ReleaseNotes.ForVersion(loadedNotes, newVersion);
            changelogText = string.IsNullOrWhiteSpace(forVersion) ? published.Body : forVersion;
        }
        if (string.IsNullOrWhiteSpace(oldVersion))
            oldVersion = string.IsNullOrWhiteSpace(published.Previous) ? published.Current : published.Previous;
        if (string.IsNullOrWhiteSpace(newVersion))
            newVersion = string.IsNullOrWhiteSpace(published.Current) ? "versione corrente" : published.Current;
        if (string.IsNullOrWhiteSpace(oldVersion)) oldVersion = "versione precedente";
        _oldVersion = oldVersion;
        _newVersion = newVersion;
        _changelogText = changelogText;
        _appIcon = appIcon;
        _accentColor = accentColor ?? Color.FromArgb(197, 22, 45); // Rosso ArtCraft / REA

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        Size = new Size(460, 160);
        BackColor = Color.FromArgb(28, 28, 30); // Dark Surface

        _animTimer.Tick += (_, _) =>
        {
            if (!_isCompleted && !_hasError)
            {
                _spinnerAngle = (_spinnerAngle + 12f) % 360f;
                Invalidate();
            }
        };
        _animTimer.Start();

        _autoCloseTimer.Tick += (_, _) =>
        {
            if (_isCompleted)
            {
                _autoCloseTimer.Stop();
                CloseWithFade();
            }
        };

        SetRoundedRegion(8);
    }

    void SetRoundedRegion(int radius)
    {
        using var path = GraphicsHelper.RoundRect(new RectangleF(0, 0, Width, Height), radius);
        Region = new Region(path);
    }

    public void SetProgress(int percent, string? customStatus = null)
    {
        _progressPercent = Math.Clamp(percent, 0, 100);
        _statusText = customStatus ?? $"Downloading and installing update ({_progressPercent}%)...";
        Invalidate();
    }

    public void SetCompleted(string? completedMessage = null)
    {
        _isCompleted = true;
        _hasError = false;
        _animTimer.Stop();
        _statusText = completedMessage ?? "Update downloaded and installed successfully!";
        Invalidate();
        _autoCloseTimer.Start();
    }

    public void SetError(string errorMessage)
    {
        _hasError = true;
        _isCompleted = false;
        _animTimer.Stop();
        _statusText = errorMessage;
        Invalidate();
    }

    public void PositionAtBottomRight(Form? parent = null)
    {
        var screen = parent != null ? Screen.FromControl(parent) : Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var workArea = screen.WorkingArea;
        int x = workArea.Right - Width - 24;
        int y = workArea.Bottom - Height - 24;
        Location = new Point(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Sfondo scuro elegante
        using (var bgBrush = new SolidBrush(Color.FromArgb(28, 28, 30)))
            g.FillRectangle(bgBrush, ClientRectangle);

        // Bordo scuro sottile
        using (var borderPen = new Pen(Color.FromArgb(50, 50, 54), 1.5f))
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // 1. Icona dell'Applicazione con sfondo rosso/accent arrotondato
        var iconRect = new Rectangle(18, 16, 42, 42);
        using (var iconPath = GraphicsHelper.RoundRect(iconRect, 8f))
        {
            using var iconBg = new SolidBrush(_accentColor);
            g.FillPath(iconBg, iconPath);

            if (_appIcon != null)
            {
                g.DrawIcon(_appIcon, new Rectangle(iconRect.X + 5, iconRect.Y + 5, 32, 32));
            }
            else
            {
                // Disegna logo stilizzato NOREA
                GraphicsHelper.DrawNoreaPrismLogo(g, new RectangleF(iconRect.X + 7, iconRect.Y + 7, 28, 28), Color.White, Color.FromArgb(255, 180, 180));
            }
        }

        // 2. Intestazione: Nome App + by NOREA + Badge UPDATE
        int textX = 72;
        int textY = 16;
        using (var fTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold))
        using (var fAuthor = new Font("Segoe UI", 9.5f, FontStyle.Regular))
        using (var fBadge = new Font("Segoe UI", 7.5f, FontStyle.Bold))
        {
            var titleSz = TextRenderer.MeasureText(g, _appName, fTitle, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _appName, fTitle, new Point(textX, textY), Color.White, TextFormatFlags.NoPadding);

            int curX = textX + titleSz.Width + 8;
            var authorSz = TextRenderer.MeasureText(g, _author, fAuthor, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _author, fAuthor, new Point(curX, textY + 2), Color.FromArgb(150, 150, 155), TextFormatFlags.NoPadding);

            curX += authorSz.Width + 10;
            // Badge UPDATE rosso vivo compatto
            var badgeRect = new Rectangle(curX, textY + 2, 54, 18);
            using (var badgePath = GraphicsHelper.RoundRect(badgeRect, 3f))
            using (var badgeBrush = new SolidBrush(Color.FromArgb(197, 22, 45)))
            {
                g.FillPath(badgeBrush, badgePath);
            }
            TextRenderer.DrawText(g, "UPDATE", fBadge, badgeRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // 3. Versioni: vOld to vNew
        using (var fVer = new Font("Consolas", 8.8f, FontStyle.Regular))
        {
            string verText = $"{_oldVersion} to {_newVersion}";
            TextRenderer.DrawText(g, verText, fVer, new Point(textX, textY + 24), Color.FromArgb(145, 145, 150), TextFormatFlags.NoPadding);
        }

        // 4. Pulsante Chiudi ✕ in alto a destra
        _closeRect = new Rectangle(Width - 36, 14, 22, 22);
        Color closeCol = _hoverClose ? Color.White : Color.FromArgb(140, 140, 145);
        using (var fClose = new Font("Segoe UI", 10.5f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, "✕", fClose, _closeRect, closeCol,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // 5. Spinner / Icona di stato e Testo avanzamento
        int statusY = 74;
        if (_isCompleted)
        {
            // Spunta verde
            var checkRect = new Rectangle(18, statusY - 1, 22, 22);
            using var okBrush = new SolidBrush(Color.FromArgb(46, 204, 113));
            g.FillEllipse(okBrush, checkRect);
            using var fCheck = new Font("Segoe UI", 9f, FontStyle.Bold);
            TextRenderer.DrawText(g, "✓", fCheck, checkRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else if (_hasError)
        {
            // Punto esclamativo rosso
            var errRect = new Rectangle(18, statusY - 1, 22, 22);
            using var errBrush = new SolidBrush(Color.FromArgb(231, 76, 60));
            g.FillEllipse(errBrush, errRect);
            using var fErr = new Font("Segoe UI", 9f, FontStyle.Bold);
            TextRenderer.DrawText(g, "!", fErr, errRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            // Spinner circolare a mezzaluna rotante stile ArtCraft
            var spinnerRect = new RectangleF(18, statusY, 20, 20);
            using (var bgPen = new Pen(Color.FromArgb(55, 55, 60), 2.5f))
                g.DrawArc(bgPen, spinnerRect, 0, 360);

            using (var spinPen = new Pen(Color.White, 2.5f))
            {
                spinPen.StartCap = LineCap.Round;
                spinPen.EndCap = LineCap.Round;
                g.DrawArc(spinPen, spinnerRect, _spinnerAngle, 110f);
            }
        }

        // Testo di stato
        using (var fStatus = new Font("Segoe UI", 9.2f, FontStyle.Regular))
        {
            Color statusColor = _hasError ? Color.FromArgb(255, 120, 120) : Color.FromArgb(220, 220, 225);
            TextRenderer.DrawText(g, _statusText, fStatus, new Point(48, statusY + 1), statusColor, TextFormatFlags.NoPadding);
        }

        // 6. Footer: What's new link a sinistra con sottolineatura rossa
        using (var fWhatsNew = new Font("Segoe UI", 9.5f, FontStyle.Underline | FontStyle.Bold))
        {
            var wnSz = TextRenderer.MeasureText(g, "What's new", fWhatsNew, Size.Empty, TextFormatFlags.NoPadding);
            _whatsNewRect = new Rectangle(18, Height - 36, wnSz.Width, wnSz.Height);

            Color wnCol = _hoverWhatsNew ? Color.FromArgb(255, 120, 120) : Color.White;
            using (var fWnClean = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "What's new", fWnClean, _whatsNewRect.Location, wnCol, TextFormatFlags.NoPadding);
            }

            // Linea rossa accentuata sotto al link
            using (var linePen = new Pen(Color.FromArgb(197, 22, 45), 2f))
            {
                int lineY = _whatsNewRect.Bottom + 1;
                g.DrawLine(linePen, _whatsNewRect.Left, lineY, _whatsNewRect.Right, lineY);
            }
        }

        // 7. Footer: Pulsante DISMISS a destra (outline stile monospace)
        _dismissRect = new Rectangle(Width - 100, Height - 42, 82, 28);
        using (var dPath = GraphicsHelper.RoundRect(_dismissRect, 3f))
        {
            Color dBg = _hoverDismiss ? Color.FromArgb(42, 42, 46) : Color.FromArgb(32, 32, 35);
            using (var b = new SolidBrush(dBg)) g.FillPath(b, dPath);
            using (var p = new Pen(Color.FromArgb(70, 70, 75), 1f)) g.DrawPath(p, dPath);

            using (var fDismiss = new Font("Consolas", 8.8f, FontStyle.Bold))
            {
                Color dCol = _hoverDismiss ? Color.White : Color.FromArgb(200, 200, 205);
                string btnLabel = _isCompleted ? "DISMISS" : "DISMISS";
                TextRenderer.DrawText(g, btnLabel, fDismiss, _dismissRect, dCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool c = _closeRect.Contains(e.Location);
        bool wn = _whatsNewRect.Contains(e.Location);
        bool d = _dismissRect.Contains(e.Location);

        if (c != _hoverClose || wn != _hoverWhatsNew || d != _hoverDismiss)
        {
            _hoverClose = c;
            _hoverWhatsNew = wn;
            _hoverDismiss = d;
            Cursor = (c || wn || d) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverClose = false;
        _hoverWhatsNew = false;
        _hoverDismiss = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (_closeRect.Contains(e.Location) || _dismissRect.Contains(e.Location))
        {
            DismissClicked?.Invoke(this, EventArgs.Empty);
            CloseWithFade();
        }
        else if (_whatsNewRect.Contains(e.Location))
        {
            WhatsNewClicked?.Invoke(this, EventArgs.Empty);
            OpenWhatsNew();
        }
    }

    void OpenWhatsNew()
    {
        if (!string.IsNullOrWhiteSpace(_changelogUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_changelogUrl) { UseShellExecute = true });
            }
            catch { }
        }
        else if (!string.IsNullOrWhiteSpace(_changelogText))
        {
            MessageBox.Show(this, _changelogText, $"Novità {_appName} {_newVersion}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/chrisquire80/REA/releases") { UseShellExecute = true });
            }
            catch { }
        }
    }

    void CloseWithFade()
    {
        _animTimer.Stop();
        _autoCloseTimer.Stop();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Dispose();
            _autoCloseTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
