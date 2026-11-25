using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NoreaDesign;

namespace NoreaLauncher;

public sealed record LauncherTheme(
    string Name,
    bool IsDark,
    Color Bg,
    Color HeaderBg,
    Color PanelBg,
    Color CardBg,
    Color CardHoverBg,
    Color CardSelectedBg,
    Color InputBg,
    Color Border,
    Color BorderStrong,
    Color TextPrimary,
    Color TextSecondary,
    Color TextMuted,
    Color BrandRed,
    Color BrandBlue,
    Color BadgeBg,
    Color BadgeFg,
    Color VersionStableBg,
    Color VersionStableFg,
    Color VersionCanaryBg,
    Color VersionCanaryFg,
    Color StatusGreen,
    Color StatusAmber,
    Color StatusCoral,
    Color PrimaryBtnBg,
    Color PrimaryBtnFg,
    Color SecondaryBtnBg,
    Color SecondaryBtnHoverBg,
    Color SecondaryBtnFg)
{
    public static readonly LauncherTheme Dark = FromPalette(NoreaDesign.NoreaStyles.Dark);
    public static readonly LauncherTheme Light = FromPalette(NoreaDesign.NoreaStyles.Light);

    static LauncherTheme FromPalette(NoreaPalette palette) => new(
        Name: palette.Name,
        IsDark: palette.IsDark,
        Bg: palette.Bg,
        HeaderBg: palette.HeaderBg,
        PanelBg: palette.PanelBg,
        CardBg: palette.CardBg,
        CardHoverBg: palette.CardHoverBg,
        CardSelectedBg: palette.CardSelectedBg,
        InputBg: palette.InputBg,
        Border: palette.Border,
        BorderStrong: palette.BorderStrong,
        TextPrimary: palette.TextPrimary,
        TextSecondary: palette.TextSecondary,
        TextMuted: palette.TextMuted,
        BrandRed: palette.BrandRed,
        BrandBlue: palette.BrandBlue,
        BadgeBg: palette.BadgeBg,
        BadgeFg: palette.BadgeFg,
        VersionStableBg: palette.VersionStableBg,
        VersionStableFg: palette.VersionStableFg,
        VersionCanaryBg: palette.VersionCanaryBg,
        VersionCanaryFg: palette.VersionCanaryFg,
        StatusGreen: palette.StatusGreen,
        StatusAmber: palette.StatusAmber,
        StatusCoral: palette.StatusCoral,
        PrimaryBtnBg: palette.PrimaryBtnBg,
        PrimaryBtnFg: palette.PrimaryBtnFg,
        SecondaryBtnBg: palette.SecondaryBtnBg,
        SecondaryBtnHoverBg: palette.SecondaryBtnHoverBg,
        SecondaryBtnFg: palette.SecondaryBtnFg);

    public static LauncherTheme FromAppearance(string mode) => mode switch
    {
        "Light" => Light,
        "Dark" => Dark,
        _ => NoreaDesign.NoreaStyles.CurrentPalette.IsDark ? Dark : Light,
    };

    public static Color ParseHex(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return ColorTranslator.FromHtml(hex); }
        catch { return fallback; }
    }
}

public static class GraphicsHelper
{
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0f || r.Width <= 0f || r.Height <= 0f) { p.AddRectangle(r); return p; }
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void DrawPill(Graphics g, string text, Font font, Point loc, Color bg, Color fg, Color? border = null, int padX = 8, int height = 18)
    {
        var sz = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var rect = new RectangleF(loc.X, loc.Y, sz.Width + padX * 2, height);
        using var path = RoundRect(rect, 4f);
        using (var b = new SolidBrush(bg)) g.FillPath(b, path);
        if (border is { } bc)
        {
            using var p = new Pen(bc, 1f);
            g.DrawPath(p, path);
        }
        TextRenderer.DrawText(g, text, font, Rectangle.Round(rect), fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public static int MeasurePillWidth(Graphics g, string text, Font font, int padX = 8)
    {
        var sz = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return sz.Width + padX * 2;
    }

    public static void DrawNoreaPrismLogo(Graphics g, RectangleF bounds, Color blue, Color red)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;

        using (var p1 = new GraphicsPath())
        {
            p1.AddLines(
            [
                new PointF(x + w * 0.08f, y + h * 0.88f),
                new PointF(x + w * 0.28f, y + h * 0.12f),
                new PointF(x + w * 0.48f, y + h * 0.12f),
                new PointF(x + w * 0.28f, y + h * 0.88f),
            ]);
            p1.CloseFigure();
            using var b1 = new SolidBrush(blue);
            g.FillPath(b1, p1);
        }

        using (var p2 = new GraphicsPath())
        {
            p2.AddLines(
            [
                new PointF(x + w * 0.32f, y + h * 0.12f),
                new PointF(x + w * 0.50f, y + h * 0.12f),
                new PointF(x + w * 0.78f, y + h * 0.88f),
                new PointF(x + w * 0.60f, y + h * 0.88f),
            ]);
            p2.CloseFigure();
            using var b2 = new SolidBrush(Color.FromArgb(220, Math.Min(255, blue.R + 40), Math.Min(255, blue.G + 40), 255));
            g.FillPath(b2, p2);
        }

        using (var p3 = new GraphicsPath())
        {
            p3.AddLines(
            [
                new PointF(x + w * 0.66f, y + h * 0.88f),
                new PointF(x + w * 0.80f, y + h * 0.26f),
                new PointF(x + w * 0.94f, y + h * 0.26f),
                new PointF(x + w * 0.82f, y + h * 0.88f),
            ]);
            p3.CloseFigure();
            using var b3 = new SolidBrush(red);
            g.FillPath(b3, p3);
        }
    }

    public static void DrawAppIcon(Graphics g, Rectangle bounds, AppEntry app)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var accent = Color.FromArgb(255, 70, 130, 210);

        var r = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        using var bgPath = RoundRect(r, 8f);
        var topCol = app.IsInstalled ? Color.FromArgb(255, 43, 120, 200) : Color.FromArgb(255, 46, 48, 56);
        var botCol = app.IsInstalled ? Color.FromArgb(255, 22, 68, 136) : Color.FromArgb(255, 30, 32, 38);

        using (var lg = new LinearGradientBrush(r, topCol, botCol, 45f))
            g.FillPath(lg, bgPath);

        using (var borderPen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            g.DrawPath(borderPen, bgPath);

        float u = bounds.Width / 32f;
        float ox = bounds.X, oy = bounds.Y;
        using var white = new SolidBrush(Color.FromArgb(245, 248, 250, 252));
        using var accBrush = new SolidBrush(Color.FromArgb(255, 232, 145, 45));
        using var whitePen = new Pen(Color.FromArgb(235, 240, 242, 248), 1.8f * u);

        switch (app.IconType.ToLowerInvariant())
        {
            case "notepad":
                using (var pagePath = new GraphicsPath())
                {
                    pagePath.AddLines(
                    [
                        new PointF(ox + 8 * u, oy + 5 * u),
                        new PointF(ox + 19 * u, oy + 5 * u),
                        new PointF(ox + 24 * u, oy + 10 * u),
                        new PointF(ox + 24 * u, oy + 26 * u),
                        new PointF(ox + 8 * u, oy + 26 * u),
                    ]);
                    pagePath.CloseFigure();
                    g.FillPath(white, pagePath);
                }
                using (var foldBrush = new SolidBrush(Color.FromArgb(191, 217, 242)))
                {
                    g.FillPolygon(foldBrush,
                    [
                        new PointF(ox + 19 * u, oy + 5 * u),
                        new PointF(ox + 19 * u, oy + 10 * u),
                        new PointF(ox + 24 * u, oy + 10 * u),
                    ]);
                }
                using (var b1 = new SolidBrush(Color.FromArgb(20, 80, 160)))
                    g.FillRectangle(b1, ox + 10.5f * u, oy + 12.5f * u, 7.5f * u, 2.0f * u);
                g.FillRectangle(accBrush, ox + 12f * u, oy + 16.2f * u, 8.5f * u, 2.0f * u);
                using (var b3 = new SolidBrush(Color.FromArgb(63, 163, 77)))
                    g.FillRectangle(b3, ox + 12f * u, oy + 19.9f * u, 6.5f * u, 2.0f * u);
                break;

            case "transfer":
                using (var folder = new GraphicsPath())
                {
                    folder.AddLines(
                    [
                        new PointF(ox + 6.5f * u, oy + 10f * u),
                        new PointF(ox + 13.5f * u, oy + 10f * u),
                        new PointF(ox + 16f * u, oy + 12.5f * u),
                        new PointF(ox + 25f * u, oy + 12.5f * u),
                        new PointF(ox + 25f * u, oy + 25f * u),
                        new PointF(ox + 6.5f * u, oy + 25f * u),
                    ]);
                    folder.CloseFigure();
                    g.FillPath(white, folder);
                }
                using (var upload = new SolidBrush(Color.FromArgb(15, 76, 158)))
                    g.FillPolygon(upload,
                    [
                        new PointF(ox + 9.5f * u, oy + 13.5f * u),
                        new PointF(ox + 18.5f * u, oy + 13.5f * u),
                        new PointF(ox + 18.5f * u, oy + 11.5f * u),
                        new PointF(ox + 23f * u, oy + 15.5f * u),
                        new PointF(ox + 18.5f * u, oy + 19f * u),
                        new PointF(ox + 18.5f * u, oy + 17.5f * u),
                        new PointF(ox + 9.5f * u, oy + 17.5f * u),
                    ]);
                g.FillPolygon(accBrush,
                [
                    new PointF(ox + 22.5f * u, oy + 19.5f * u),
                    new PointF(ox + 13.5f * u, oy + 19.5f * u),
                    new PointF(ox + 13.5f * u, oy + 18f * u),
                    new PointF(ox + 9f * u, oy + 22f * u),
                    new PointF(ox + 13.5f * u, oy + 25.5f * u),
                    new PointF(ox + 13.5f * u, oy + 23.5f * u),
                    new PointF(ox + 22.5f * u, oy + 23.5f * u),
                ]);
                break;

            case "search":
                using (var lensPen = new Pen(Color.FromArgb(245, 248, 250, 252), 2.5f * u))
                    g.DrawEllipse(lensPen, ox + 7f * u, oy + 7f * u, 13f * u, 13f * u);
                using (var handlePen = new Pen(Color.FromArgb(255, 242, 120, 103), 2.8f * u))
                {
                    handlePen.StartCap = LineCap.Round;
                    handlePen.EndCap = LineCap.Round;
                    g.DrawLine(handlePen, ox + 18.2f * u, oy + 18.2f * u, ox + 24.5f * u, oy + 24.5f * u);
                }
                break;

            default:
                using (var f = new Font("Segoe UI", 11f * u, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    var ch = app.Prefix.Length > 0 ? app.Prefix[..1].ToUpperInvariant() : "N";
                    g.DrawString(ch, f, white, ox + 9 * u, oy + 7 * u);
                }
                break;
        }
    }
}
