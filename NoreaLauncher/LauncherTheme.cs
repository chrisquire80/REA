using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

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
    static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    // Dark UI accessibile (WCAG >= 4.5:1): canvas #121212, card #1E1E1E, hover #252525, bordi morbidi, accenti desaturati
    public static readonly LauncherTheme Dark = new(
        Name: "Dark",
        IsDark: true,
        Bg: C(0x121212),
        HeaderBg: C(0x18181A),
        PanelBg: C(0x161618),
        CardBg: C(0x1E1E1E),
        CardHoverBg: C(0x252525),
        CardSelectedBg: C(0x262932),
        InputBg: C(0x161618),
        Border: C(0x2E2E32),          // ~rgba(255,255,255,0.08) su #1E1E1E
        BorderStrong: C(0x43454E),
        TextPrimary: C(0xE0E0E0),     // ~90% opacità
        TextSecondary: C(0xB4B4BE),   // ~70% opacità (contrasto > 6.2:1 su #1E1E1E)
        TextMuted: C(0xA0A0AA),       // ~65% opacità (contrasto >= 4.7:1 su #1E1E1E e #121212)
        BrandRed: C(0xFF7B7B),        // Corallo desaturato accessibile
        BrandBlue: C(0x79C0FF),       // Celeste pastello ad alto contrasto per testi tecnici (#79C0FF)
        BadgeBg: C(0x1E293B),
        BadgeFg: C(0x93C5FD),
        VersionStableBg: C(0x252830),
        VersionStableFg: C(0xA5B4FC),
        VersionCanaryBg: C(0x2D1E20),
        VersionCanaryFg: C(0xFF7B7B),
        StatusGreen: C(0x4ADE80),
        StatusAmber: C(0xFBBF24),
        StatusCoral: C(0xFF7B7B),
        PrimaryBtnBg: C(0xE4E4E8),
        PrimaryBtnFg: C(0x121214),
        SecondaryBtnBg: C(0x252529),
        SecondaryBtnHoverBg: C(0x2F2F35),
        SecondaryBtnFg: C(0xE0E0E4));

    public static readonly LauncherTheme Light = new(
        Name: "Light",
        IsDark: false,
        Bg: C(0xF4F5F7),
        HeaderBg: C(0xFFFFFF),
        PanelBg: C(0xECEEF2),
        CardBg: C(0xFFFFFF),
        CardHoverBg: C(0xF8F9FB),
        CardSelectedBg: C(0xEBF2FF),
        InputBg: C(0xFFFFFF),
        Border: C(0xDCDFE6),
        BorderStrong: C(0xB8BEC9),
        TextPrimary: C(0x18181B),
        TextSecondary: C(0x4B4B55),
        TextMuted: C(0x646470),
        BrandRed: C(0xC83E3E),
        BrandBlue: C(0x1D4ED8),
        BadgeBg: C(0xE0ECFF),
        BadgeFg: C(0x1E40AF),
        VersionStableBg: C(0xE5E7EB),
        VersionStableFg: C(0x374151),
        VersionCanaryBg: C(0xFEE2E2),
        VersionCanaryFg: C(0x991B1B),
        StatusGreen: C(0x16A34A),
        StatusAmber: C(0xD97706),
        StatusCoral: C(0xDC2626),
        PrimaryBtnBg: C(0x18181B),
        PrimaryBtnFg: C(0xFFFFFF),
        SecondaryBtnBg: C(0xF1F2F6),
        SecondaryBtnHoverBg: C(0xE4E6EE),
        SecondaryBtnFg: C(0x18181B));

    public static LauncherTheme FromAppearance(string mode) => mode switch
    {
        "Light" => Light,
        "Dark" => Dark,
        _ => IsSystemDark() ? Dark : Light,
    };

    static bool IsSystemDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (k?.GetValue("AppsUseLightTheme") is int v) return v == 0;
        }
        catch { }
        return true;
    }

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
