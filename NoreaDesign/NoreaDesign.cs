using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NoreaDesign;

public enum NoreaButtonStyle
{
    Primary,
    Secondary,
    Danger,
    Quiet
}

public sealed record NoreaPalette(
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
    Color SecondaryBtnFg);

public static class NoreaStyles
{
    public const string UiFontFamily = "Segoe UI";
    public const string DataFontFamily = "Consolas";
    public const float BodyFontSize = 9.5f;
    public const float SectionFontSize = 10.5f;
    public const float TitleFontSize = 16f;
    public const int Space1 = 4;
    public const int Space2 = 8;
    public const int Space3 = 12;
    public const int Space4 = 16;
    public const int Space6 = 24;
    public const int Space8 = 32;
    public const int ControlHeight = 34;

    public static readonly NoreaPalette Dark = new(
        Name: "Dark",
        IsDark: true,
        Bg: C(0x101722),
        HeaderBg: C(0x121D2B),
        PanelBg: C(0x141F2E),
        CardBg: C(0x1B2939),
        CardHoverBg: C(0x233348),
        CardSelectedBg: C(0x203A58),
        InputBg: C(0x111B29),
        Border: C(0x2B3B50),
        BorderStrong: C(0x41556F),
        TextPrimary: C(0xF2F6FB),
        TextSecondary: C(0xBDCAD9),
        TextMuted: C(0x91A3B8),
        BrandRed: C(0xF27867),
        BrandBlue: C(0x6DA9FF),
        BadgeBg: C(0x1A2C44),
        BadgeFg: C(0xA8CAFF),
        VersionStableBg: C(0x253446),
        VersionStableFg: C(0xBFCEE0),
        VersionCanaryBg: C(0x3D2926),
        VersionCanaryFg: C(0xFFAC9B),
        StatusGreen: C(0x55C99A),
        StatusAmber: C(0xF3B85E),
        StatusCoral: C(0xB33830),
        PrimaryBtnBg: C(0x2E6DC9),
        PrimaryBtnFg: C(0xFFFFFF),
        SecondaryBtnBg: C(0x26374B),
        SecondaryBtnHoverBg: C(0x31445B),
        SecondaryBtnFg: C(0xEDF3FA));

    public static readonly NoreaPalette Light = new(
        Name: "Light",
        IsDark: false,
        Bg: C(0xF3F6FA),
        HeaderBg: C(0xFFFFFF),
        PanelBg: C(0xEFF3F8),
        CardBg: C(0xFFFFFF),
        CardHoverBg: C(0xF5F8FC),
        CardSelectedBg: C(0xE7F1FF),
        InputBg: C(0xFFFFFF),
        Border: C(0xD9E1EA),
        BorderStrong: C(0xB8C7D6),
        TextPrimary: C(0x172333),
        TextSecondary: C(0x47576A),
        TextMuted: C(0x64788E),
        BrandRed: C(0xC85D50),
        BrandBlue: C(0x205FC0),
        BadgeBg: C(0xE7F0FF),
        BadgeFg: C(0x174A9B),
        VersionStableBg: C(0xEDF1F6),
        VersionStableFg: C(0x405168),
        VersionCanaryBg: C(0xFDEBE7),
        VersionCanaryFg: C(0x9A3D32),
        StatusGreen: C(0x167C57),
        StatusAmber: C(0x945A00),
        StatusCoral: C(0xA32D26),
        PrimaryBtnBg: C(0x205FC0),
        PrimaryBtnFg: C(0xFFFFFF),
        SecondaryBtnBg: C(0xEDF1F6),
        SecondaryBtnHoverBg: C(0xE1E9F2),
        SecondaryBtnFg: C(0x172333));

    private static readonly Font UiFont = new(UiFontFamily, BodyFontSize);
    private static readonly Font UiSemiboldFont = new(UiFontFamily, BodyFontSize, FontStyle.Bold);
    private static readonly Font HeaderFont = new(UiFontFamily, TitleFontSize, FontStyle.Bold);
    private static readonly Font MonoFont = new(DataFontFamily, BodyFontSize);

    public static NoreaPalette CurrentPalette => IsSystemDark() ? Dark : Light;

    public static Font CreateUiFont(float size = BodyFontSize, FontStyle style = FontStyle.Regular) =>
        new(UiFontFamily, size, style);

    public static Font CreateDataFont(float size = BodyFontSize, FontStyle style = FontStyle.Regular) =>
        new(DataFontFamily, size, style);

    public static void ApplyForm(Form form, NoreaPalette? palette = null)
    {
        palette ??= CurrentPalette;
        form.BackColor = palette.Bg;
        form.ForeColor = palette.TextPrimary;
        form.Font = UiFont;
        form.HandleCreated += (_, _) => ApplyWindowFrame(form, palette);
        if (form.IsHandleCreated) ApplyWindowFrame(form, palette);
    }

    public static void StyleLabel(Label label, NoreaPalette palette, bool heading = false, bool muted = false, bool monospace = false)
    {
        label.BackColor = Color.Transparent;
        label.ForeColor = muted ? palette.TextMuted : heading ? palette.TextPrimary : palette.TextSecondary;
        label.Font = monospace ? MonoFont : heading ? UiSemiboldFont : UiFont;
        label.UseMnemonic = false;
    }

    public static void StyleInput(Control input, NoreaPalette palette, bool monospace = false)
    {
        input.BackColor = palette.InputBg;
        input.ForeColor = palette.TextPrimary;
        input.Font = monospace ? MonoFont : UiFont;
        if (input is TextBoxBase textBox) textBox.BorderStyle = BorderStyle.FixedSingle;
        if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        if (input is NumericUpDown numeric) numeric.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void StyleButton(Button button, NoreaPalette palette, NoreaButtonStyle style = NoreaButtonStyle.Secondary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.Font = UiSemiboldFont;
        button.Cursor = Cursors.Hand;
        button.MinimumSize = new Size(0, ControlHeight);
        button.ForeColor = style switch
        {
            NoreaButtonStyle.Primary => palette.PrimaryBtnFg,
            NoreaButtonStyle.Danger => Color.White,
            NoreaButtonStyle.Quiet => palette.TextSecondary,
            _ => palette.SecondaryBtnFg
        };
        button.BackColor = style switch
        {
            NoreaButtonStyle.Primary => palette.PrimaryBtnBg,
            NoreaButtonStyle.Danger => palette.StatusCoral,
            NoreaButtonStyle.Quiet => palette.HeaderBg,
            _ => palette.SecondaryBtnBg
        };
        button.FlatAppearance.BorderColor = style == NoreaButtonStyle.Secondary ? palette.BorderStrong : button.BackColor;
        button.FlatAppearance.BorderSize = style == NoreaButtonStyle.Secondary ? 1 : 0;
        button.FlatAppearance.MouseOverBackColor = style switch
        {
            NoreaButtonStyle.Primary => ControlPaint.Light(palette.PrimaryBtnBg, .12f),
            NoreaButtonStyle.Danger => ControlPaint.Light(palette.StatusCoral, .12f),
            NoreaButtonStyle.Quiet => palette.CardHoverBg,
            _ => palette.SecondaryBtnHoverBg
        };
        button.FlatAppearance.MouseDownBackColor = style switch
        {
            NoreaButtonStyle.Primary => ControlPaint.Dark(palette.PrimaryBtnBg, .08f),
            NoreaButtonStyle.Danger => ControlPaint.Dark(palette.StatusCoral, .08f),
            _ => palette.CardSelectedBg
        };
    }

    public static void StyleToolStrip(ToolStrip strip, NoreaPalette palette)
    {
        strip.BackColor = palette.HeaderBg;
        strip.ForeColor = palette.TextSecondary;
        strip.Font = UiFont;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new NoreaToolStripRenderer(palette);
        strip.GripStyle = ToolStripGripStyle.Hidden;
        foreach (ToolStripItem item in strip.Items)
        {
            item.ForeColor = palette.TextSecondary;
            item.Font = UiFont;
            if (item is ToolStripButton button)
            {
                button.DisplayStyle = ToolStripItemDisplayStyle.Text;
                button.Padding = new Padding(Space2, Space1, Space2, Space1);
                button.Margin = new Padding(2, 1, 2, 1);
                button.Tag ??= NoreaButtonStyle.Secondary;
            }
        }
    }

    public static void StyleToolStripButton(ToolStripButton button, NoreaPalette palette, NoreaButtonStyle style)
    {
        button.Tag = style;
        button.ForeColor = style switch
        {
            NoreaButtonStyle.Primary => palette.PrimaryBtnFg,
            NoreaButtonStyle.Danger => Color.White,
            _ => palette.SecondaryBtnFg
        };
        button.ToolTipText ??= button.Text;
    }

    public static void StyleGrid(DataGridView grid, NoreaPalette palette)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = palette.CardBg;
        grid.ForeColor = palette.TextPrimary;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = palette.Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowHeadersVisible = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = palette.PanelBg,
            ForeColor = palette.TextSecondary,
            Font = UiSemiboldFont,
            SelectionBackColor = palette.PanelBg,
            SelectionForeColor = palette.TextSecondary,
            Padding = new Padding(Space2, 0, Space2, 0)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = palette.CardBg,
            ForeColor = palette.TextPrimary,
            SelectionBackColor = palette.CardSelectedBg,
            SelectionForeColor = palette.TextPrimary,
            Font = UiFont,
            Padding = new Padding(Space2, 0, Space2, 0)
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = palette.PanelBg };
        grid.RowTemplate.Height = 34;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }

    public static void StyleStatusStrip(StatusStrip strip, NoreaPalette palette)
    {
        strip.BackColor = palette.HeaderBg;
        strip.ForeColor = palette.TextSecondary;
        strip.Font = UiFont;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new NoreaToolStripRenderer(palette);
        foreach (ToolStripItem item in strip.Items)
            item.ForeColor = palette.TextSecondary;
    }

    private static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyTitleBar(Form form, bool isDark)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int dark = isDark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static void ApplyWindowFrame(Form form, NoreaPalette palette) =>
        ApplyTitleBar(form, palette.IsDark);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private sealed class NoreaToolStripRenderer(NoreaPalette palette) : ToolStripProfessionalRenderer(new NoreaToolStripColors(palette))
    {
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is not ToolStripButton button)
            {
                base.OnRenderButtonBackground(e);
                return;
            }

            var style = button.Tag is NoreaButtonStyle value ? value : NoreaButtonStyle.Secondary;
            bool active = e.Item.Pressed;
            bool hover = e.Item.Selected;
            if (!active && !hover && style == NoreaButtonStyle.Quiet) return;

            Color fill = style switch
            {
                NoreaButtonStyle.Primary => active ? ControlPaint.Dark(palette.PrimaryBtnBg, .08f) : hover ? ControlPaint.Light(palette.PrimaryBtnBg, .12f) : palette.PrimaryBtnBg,
                NoreaButtonStyle.Danger => active ? ControlPaint.Dark(palette.StatusCoral, .08f) : hover ? ControlPaint.Light(palette.StatusCoral, .12f) : palette.StatusCoral,
                _ => active ? palette.CardSelectedBg : palette.SecondaryBtnHoverBg
            };

            using var path = GraphicsHelper.RoundRect(new RectangleF(e.Item.ContentRectangle.X, e.Item.ContentRectangle.Y, e.Item.ContentRectangle.Width, e.Item.ContentRectangle.Height), 5f);
            using var brush = new SolidBrush(fill);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    private sealed class NoreaToolStripColors(NoreaPalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => palette.HeaderBg;
        public override Color ToolStripGradientMiddle => palette.HeaderBg;
        public override Color ToolStripGradientEnd => palette.HeaderBg;
        public override Color ToolStripBorder => palette.Border;
        public override Color ButtonSelectedGradientBegin => palette.CardHoverBg;
        public override Color ButtonSelectedGradientMiddle => palette.CardHoverBg;
        public override Color ButtonSelectedGradientEnd => palette.CardHoverBg;
        public override Color ButtonPressedGradientBegin => palette.CardSelectedBg;
        public override Color ButtonPressedGradientMiddle => palette.CardSelectedBg;
        public override Color ButtonPressedGradientEnd => palette.CardSelectedBg;
        public override Color SeparatorDark => palette.Border;
        public override Color SeparatorLight => palette.HeaderBg;
        public override Color MenuBorder => palette.Border;
        public override Color MenuItemSelected => palette.CardHoverBg;
        public override Color MenuItemBorder => palette.BorderStrong;
        public override Color ToolStripDropDownBackground => palette.CardBg;
    }
}

public static class GraphicsHelper
{
    public static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (rect.Width <= 0 || rect.Height <= 0 || radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        float diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
