using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class AppListSideItem : Control
{
    public AppEntry App { get; }
    public bool IsSelected { get; set; }

    LauncherTheme _theme = LauncherTheme.Dark;
    bool _isHover;

    public AppListSideItem(AppEntry app, LauncherTheme theme)
    {
        App = app;
        _theme = theme;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 72;
        Cursor = Cursors.Hand;
    }

    public void SetTheme(LauncherTheme theme)
    {
        _theme = theme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color bg = IsSelected ? _theme.CardSelectedBg : _isHover ? _theme.CardHoverBg : _theme.PanelBg;
        g.Clear(bg);

        // Indicatore barra attiva
        if (IsSelected)
        {
            using var activeBrush = new SolidBrush(_theme.BrandBlue);
            g.FillRectangle(activeBrush, 0, 12, 3, Height - 24);
        }

        using var borderPen = new Pen(_theme.Border);
        g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);

        // Icona
        var iconRect = new Rectangle(14, 14, 44, 44);
        GraphicsHelper.DrawAppIcon(g, iconRect, App);

        // Titolo
        int tx = 70;
        int ty = 16;
        using var titleFont = new Font("Segoe UI", 10.8f, FontStyle.Bold);
        TextRenderer.DrawText(g, App.DisplayName, titleFont, new Point(tx, ty), _theme.TextPrimary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        var titleSize = TextRenderer.MeasureText(g, App.DisplayName, titleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        // Version badge
        using var monoFont = new Font("Consolas", 8f, FontStyle.Bold);
        var vBg = App.IsCanary ? _theme.VersionCanaryBg : _theme.VersionStableBg;
        var vFg = App.IsCanary ? _theme.VersionCanaryFg : _theme.VersionStableFg;
        GraphicsHelper.DrawPill(g, App.Version, monoFont, new Point(tx + titleSize.Width + 9, ty + 2), vBg, vFg, padX: 7, height: 17);

        // Runtime o stato esecuzione
        using var subFont = new Font("Segoe UI", 8.7f);
        if (App.IsRunning)
        {
            using var dotBrush = new SolidBrush(_theme.StatusGreen);
            g.FillEllipse(dotBrush, tx, ty + 29, 7, 7);
            TextRenderer.DrawText(g, "In esecuzione", subFont, new Point(tx + 12, ty + 24), _theme.StatusGreen);
        }
        else
        {
            TextRenderer.DrawText(g, App.Runtime, subFont, new Point(tx, ty + 24), _theme.TextSecondary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHover = false;
        Invalidate();
    }
}
