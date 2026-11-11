using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class AppCardControl : Control
{
    public AppEntry App { get; }
    public bool IsListMode { get; set; }

    LauncherTheme _theme = LauncherTheme.Dark;
    bool _hoverCard, _hoverPrimaryBtn, _hoverMenuBtn, _hoverSourceLink;
    Rectangle _primaryBtnRect, _menuBtnRect, _sourceLinkRect;

    public event EventHandler<AppEntry>? PrimaryActionRequested;
    public event EventHandler<(AppEntry App, Point ScreenPos)>? MenuRequested;
    public event EventHandler<AppEntry>? SourceLinkRequested;

    public AppCardControl(AppEntry app, LauncherTheme theme, bool listMode = false)
    {
        App = app;
        _theme = theme;
        IsListMode = listMode;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = listMode ? new Size(760, 84) : new Size(256, 264);
        Margin = new Padding(0, 0, 12, 12);
    }

    public void SetTheme(LauncherTheme t)
    {
        _theme = t;
        Invalidate();
    }

    void ComputeLayout()
    {
        if (IsListMode)
        {
            _sourceLinkRect = new Rectangle(Width - 260, (Height - 24) / 2, 85, 24);
            _primaryBtnRect = new Rectangle(Width - 160, (Height - 30) / 2, 108, 30);
            _menuBtnRect = new Rectangle(Width - 44, (Height - 30) / 2, 30, 30);
        }
        else
        {
            _sourceLinkRect = new Rectangle(Width - 116, 6, 102, 18);
            int btnY = Height - 42;
            _menuBtnRect = new Rectangle(Width - 14 - 30, btnY, 30, 28);
            _primaryBtnRect = new Rectangle(14, btnY, Width - 14 - 30 - 6 - 14, 28);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ComputeLayout();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bg = _hoverCard ? _theme.CardHoverBg : _theme.CardBg;
        g.Clear(bg);

        using var borderPen = new Pen(_hoverCard ? _theme.BorderStrong : _theme.Border, 1f);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        if (IsListMode)
            PaintListMode(g, borderPen);
        else
            PaintGridMode(g, borderPen);
    }

    void PaintGridMode(Graphics g, Pen borderPen)
    {
        var accent = LauncherTheme.ParseHex(App.AccentHex, _theme.BrandRed);

        // 1. Top strip: Version + GITHUB/ANALISI link
        using var monoSmall = new Font("Consolas", 8f, FontStyle.Bold);
        var verColor = App.IsInstalled ? accent : _theme.TextSecondary;
        TextRenderer.DrawText(g, App.Version.ToUpperInvariant(), monoSmall, new Rectangle(12, 7, 110, 16), verColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        var linkText = App.ResolvedAnalysisFile != null ? "ANALISI ↗" : App.HasProject ? "SOURCE ↗" : "NOREA ↗";
        var linkCol = _hoverSourceLink ? _theme.TextPrimary : _theme.TextSecondary;
        TextRenderer.DrawText(g, linkText, monoSmall, _sourceLinkRect, linkCol,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        g.DrawLine(borderPen, 0, 29, Width, 29);

        // 2. Icon + App Title + BY NOREA badge + Tagline
        var iconRect = new Rectangle(14, 42, 42, 42);
        GraphicsHelper.DrawAppIcon(g, iconRect, App);

        int tx = 66, ty = 40;
        using (var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
        {
            var prefixSize = TextRenderer.MeasureText(g, App.Prefix, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, App.Prefix, titleFont, new Point(tx, ty), _theme.TextPrimary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            var suffixColor = App.IsInstalled ? accent : _theme.TextSecondary;
            var suffixSize = TextRenderer.MeasureText(g, App.Suffix, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, App.Suffix, titleFont, new Point(tx + prefixSize.Width, ty), suffixColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Badge "BY NOREA"
            int bx = tx + prefixSize.Width + suffixSize.Width + 6;
            DrawPillBadge(g, App.AuthorBadge, new Point(bx, ty + 2), _theme.BadgeBg, _theme.BadgeFg);
        }

        using (var taglineFont = new Font("Segoe UI", 8f))
        {
            var tagRect = new Rectangle(tx, ty + 24, Width - tx - 12, 34);
            var oldClip = g.Clip;
            g.SetClip(tagRect);
            TextRenderer.DrawText(g, App.Tagline, taglineFont, tagRect, _theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            g.Clip = oldClip;
        }

        // 3. Status Pill + Description + Size + Last Updated
        int bodyY = 104;
        if (!App.IsInstalled)
        {
            string statusText = App.CanBuildOrInstall ? "AVAILABLE TO INSTALL" : "AVAILABLE IN SUITE";
            DrawPillBadge(g, statusText, new Point(14, bodyY), _theme.BadgeBg, _theme.BadgeFg);
            bodyY += 26;

            using var metaFont = new Font("Segoe UI", 8f);
            TextRenderer.DrawText(g, $"Download size: {App.EstimatedSizeMb:0.#} MB", metaFont, new Rectangle(14, bodyY, Width - 28, 16),
                _theme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            bodyY += 18;
        }
        else if (App.HasUpdate)
        {
            DrawPillBadge(g, "UPDATE AVAILABLE", new Point(14, bodyY), _theme.BadgeBg, _theme.BadgeFg);
            bodyY += 24;
        }

        using (var descFont = new Font("Segoe UI", 8.2f))
        {
            int descH = App.IsInstalled ? 42 : 30;
            var descRect = new Rectangle(14, bodyY, Width - 28, descH);
            var oldClip = g.Clip;
            g.SetClip(descRect);
            TextRenderer.DrawText(g, App.Description, descFont, descRect, _theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            g.Clip = oldClip;
            bodyY += descH + 4;
        }

        using (var dateFont = new Font("Segoe UI", 7.8f))
        {
            TextRenderer.DrawText(g, $"Last updated: {App.LastUpdatedText}", dateFont, new Rectangle(14, bodyY, Width - 28, 16),
                _theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        // 4. Bottom Action Buttons (OPEN / INSTALL + ...)
        DrawActionButtons(g);
    }

    void PaintListMode(Graphics g, Pen borderPen)
    {
        var accent = LauncherTheme.ParseHex(App.AccentHex, _theme.BrandRed);
        var iconRect = new Rectangle(16, (Height - 44) / 2, 44, 44);
        GraphicsHelper.DrawAppIcon(g, iconRect, App);

        int tx = 74, ty = 16;
        using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
        {
            var pSize = TextRenderer.MeasureText(g, App.Prefix, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, App.Prefix, titleFont, new Point(tx, ty), _theme.TextPrimary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            var sColor = App.IsInstalled ? accent : _theme.TextSecondary;
            var sSize = TextRenderer.MeasureText(g, App.Suffix, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, App.Suffix, titleFont, new Point(tx + pSize.Width, ty), sColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int bx = tx + pSize.Width + sSize.Width + 8;
            bx += DrawPillBadge(g, App.AuthorBadge, new Point(bx, ty + 2), _theme.BadgeBg, _theme.BadgeFg) + 6;
            DrawPillBadge(g, App.Version.ToUpperInvariant(), new Point(bx, ty + 2), _theme.SecondaryBtnBg, sColor);
        }

        using (var subFont = new Font("Segoe UI", 8.5f))
        {
            var info = $"{App.Tagline}  ·  {App.EstimatedSizeMb:0.#} MB  ·  Last updated: {App.LastUpdatedText}";
            TextRenderer.DrawText(g, info, subFont, new Rectangle(tx, ty + 26, _sourceLinkRect.Left - tx - 12, 22),
                _theme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        using (var monoSmall = new Font("Consolas", 8f, FontStyle.Bold))
        {
            var linkText = App.ResolvedAnalysisFile != null ? "ANALISI ↗" : "SOURCE ↗";
            TextRenderer.DrawText(g, linkText, monoSmall, _sourceLinkRect, _hoverSourceLink ? _theme.TextPrimary : _theme.TextSecondary,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        DrawActionButtons(g);
    }

    int DrawPillBadge(Graphics g, string text, Point loc, Color bg, Color fg)
    {
        using var f = new Font("Consolas", 7f, FontStyle.Bold);
        var sz = TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var rect = new Rectangle(loc.X, loc.Y, sz.Width + 10, 16);
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, rect);
        using (var p = new Pen(Color.FromArgb(60, fg))) g.DrawRectangle(p, rect);
        TextRenderer.DrawText(g, text, f, rect, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        return rect.Width;
    }

    void DrawActionButtons(Graphics g)
    {
        using var btnFont = new Font("Consolas", 8.2f, FontStyle.Bold);

        if (App.IsInstalled && !App.HasUpdate)
        {
            // Pulsante OPEN scuro con bordo (come PdfCraft in Screenshot 1)
            using (var bg = new SolidBrush(_hoverPrimaryBtn ? _theme.SecondaryBtnHoverBg : _theme.SecondaryBtnBg))
                g.FillRectangle(bg, _primaryBtnRect);
            using (var p = new Pen(_hoverPrimaryBtn ? _theme.BorderStrong : _theme.Border))
                g.DrawRectangle(p, _primaryBtnRect);
            TextRenderer.DrawText(g, "OPEN", btnFont, _primaryBtnRect, _theme.SecondaryBtnFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            // Pulsante INSTALL / UPDATE bianco pieno con testo scuro (come CADCraft, DeckCraft, ecc. in Screenshot 1)
            string label = App.HasUpdate ? "UPDATE" : "INSTALL";
            var fill = _hoverPrimaryBtn
                ? Color.FromArgb(255, Math.Max(0, _theme.PrimaryBtnBg.R - 22), Math.Max(0, _theme.PrimaryBtnBg.G - 22), Math.Max(0, _theme.PrimaryBtnBg.B - 22))
                : _theme.PrimaryBtnBg;
            using (var bg = new SolidBrush(fill))
                g.FillRectangle(bg, _primaryBtnRect);
            TextRenderer.DrawText(g, label, btnFont, _primaryBtnRect, _theme.PrimaryBtnFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // Pulsante "..."
        using (var bg = new SolidBrush(_hoverMenuBtn ? _theme.SecondaryBtnHoverBg : _theme.SecondaryBtnBg))
            g.FillRectangle(bg, _menuBtnRect);
        using (var p = new Pen(_hoverMenuBtn ? _theme.BorderStrong : _theme.Border))
            g.DrawRectangle(p, _menuBtnRect);
        TextRenderer.DrawText(g, "···", btnFont, _menuBtnRect, _theme.SecondaryBtnFg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ComputeLayout();
        bool hp = _primaryBtnRect.Contains(e.Location);
        bool hm = _menuBtnRect.Contains(e.Location);
        bool hs = _sourceLinkRect.Contains(e.Location);
        Cursor = (hp || hm || hs) ? Cursors.Hand : Cursors.Default;
        if (!_hoverCard || hp != _hoverPrimaryBtn || hm != _hoverMenuBtn || hs != _hoverSourceLink)
        {
            _hoverCard = true;
            _hoverPrimaryBtn = hp;
            _hoverMenuBtn = hm;
            _hoverSourceLink = hs;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCard = _hoverPrimaryBtn = _hoverMenuBtn = _hoverSourceLink = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        ComputeLayout();
        if (e.Button == MouseButtons.Left)
        {
            if (_primaryBtnRect.Contains(e.Location))
                PrimaryActionRequested?.Invoke(this, App);
            else if (_menuBtnRect.Contains(e.Location))
                MenuRequested?.Invoke(this, (App, PointToScreen(new Point(_menuBtnRect.Left, _menuBtnRect.Bottom + 2))));
            else if (_sourceLinkRect.Contains(e.Location))
                SourceLinkRequested?.Invoke(this, App);
        }
        else if (e.Button == MouseButtons.Right)
        {
            MenuRequested?.Invoke(this, (App, PointToScreen(e.Location)));
        }
    }
}
