using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NoreaDesign;

namespace NotepadREA;

/// <summary>Tema completo: colori dell'editor (sintassi) e dell'interfaccia (menu, toolbar, schede, barra di stato).</summary>
public sealed record Theme(
    string Name, bool IsDark,
    // Editor
    Color Back, Color Fore, Color Comment, Color Keyword, Color Keyword2, Color String, Color Number,
    Color Operator, Color Preprocessor, Color Tag, Color Attribute, Color CaretLine, Color Selection,
    Color MarginBack, Color MarginFore, Color Caret, Color SmartHighlight,
    // Interfaccia
    Color UiBack, Color UiFore, Color UiBorder, Color UiHover, Color TabActive, Color TabInactive,
    Color Accent, Color Dirty)
{
    static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    public static readonly Theme Light = new("Chiaro", false,
        Back: C(0xFFFFFF), Fore: C(0x000000), Comment: C(0x008000), Keyword: C(0x0000FF), Keyword2: C(0x8000FF),
        String: C(0x808080), Number: C(0xFF8000), Operator: C(0x000080), Preprocessor: C(0x804000), Tag: C(0x0000FF),
        Attribute: C(0xFF0000), CaretLine: C(0xE8E8FF), Selection: C(0xC0C0C0), MarginBack: C(0xF0F0F0),
        MarginFore: C(0x8A8A8A), Caret: C(0x000000), SmartHighlight: C(0x00C800),
        UiBack: C(0xF3F3F3), UiFore: C(0x1E1E1E), UiBorder: C(0xD4D4D4), UiHover: C(0xDDE8F5),
        TabActive: C(0xFFFFFF), TabInactive: C(0xE6E6E6), Accent: C(0x0078D4), Dirty: C(0xD13438));

    public static readonly Theme Dark = new("Scuro", true,
        Back: C(0x1E1E1E), Fore: C(0xDCDCDC), Comment: C(0x6A9955), Keyword: C(0x569CD6), Keyword2: C(0xC586C0),
        String: C(0xCE9178), Number: C(0xB5CEA8), Operator: C(0xD4D4D4), Preprocessor: C(0x9B9B9B), Tag: C(0x569CD6),
        Attribute: C(0x9CDCFE), CaretLine: C(0x2A2D2E), Selection: C(0x264F78), MarginBack: C(0x1E1E1E),
        MarginFore: C(0x858585), Caret: C(0xFFFFFF), SmartHighlight: C(0x3FA34D),
        UiBack: C(0x252526), UiFore: C(0xE0E0E0), UiBorder: C(0x3C3C3C), UiHover: C(0x37373D),
        TabActive: C(0x1E1E1E), TabInactive: C(0x2D2D2D), Accent: C(0x3794FF), Dirty: C(0xF14C4C));

    public static readonly Theme Monokai = new("Monokai", true,
        Back: C(0x272822), Fore: C(0xF8F8F2), Comment: C(0x75715E), Keyword: C(0xF92672), Keyword2: C(0x66D9EF),
        String: C(0xE6DB74), Number: C(0xAE81FF), Operator: C(0xF8F8F2), Preprocessor: C(0xA6E22E), Tag: C(0xF92672),
        Attribute: C(0xA6E22E), CaretLine: C(0x3E3D32), Selection: C(0x49483E), MarginBack: C(0x272822),
        MarginFore: C(0x90908A), Caret: C(0xF8F8F0), SmartHighlight: C(0xA6E22E),
        UiBack: C(0x1E1F1C), UiFore: C(0xF8F8F2), UiBorder: C(0x3E3D32), UiHover: C(0x3E3D32),
        TabActive: C(0x272822), TabInactive: C(0x1E1F1C), Accent: C(0xA6E22E), Dirty: C(0xF92672));

    public static readonly Theme Solarized = new("Solarized", true,
        Back: C(0x002B36), Fore: C(0x839496), Comment: C(0x586E75), Keyword: C(0x859900), Keyword2: C(0x268BD2),
        String: C(0x2AA198), Number: C(0xD33682), Operator: C(0x93A1A1), Preprocessor: C(0xCB4B16), Tag: C(0x268BD2),
        Attribute: C(0xB58900), CaretLine: C(0x073642), Selection: C(0x274642), MarginBack: C(0x002B36),
        MarginFore: C(0x586E75), Caret: C(0x93A1A1), SmartHighlight: C(0xB58900),
        UiBack: C(0x00212B), UiFore: C(0x93A1A1), UiBorder: C(0x0A3F4C), UiHover: C(0x073642),
        TabActive: C(0x002B36), TabInactive: C(0x00212B), Accent: C(0xB58900), Dirty: C(0xDC322F));

    public static readonly Theme[] All = [Light, Dark, Monokai, Solarized];

    public static Theme ByName(string? name) => All.FirstOrDefault(t => t.Name == name) ?? Light;

    public void ApplyTitleBar(Form f) => NoreaStyles.ApplyTitleBar(f, IsDark);

    /// <summary>Colora ricorsivamente controlli standard (TextBox, Button, Label...).</summary>
    public void Paint(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox or ComboBox or ListBox:
                    c.BackColor = Back; c.ForeColor = Fore; break;
                case Button b:
                    b.FlatStyle = FlatStyle.Flat; b.BackColor = TabInactive; b.ForeColor = UiFore;
                    b.FlatAppearance.BorderColor = UiBorder; b.FlatAppearance.MouseOverBackColor = UiHover; break;
                case CheckBox cb:
                    cb.ForeColor = UiFore; cb.BackColor = UiBack;
                    if (cb.Appearance == Appearance.Button) { cb.FlatStyle = FlatStyle.Flat; cb.FlatAppearance.CheckedBackColor = Color.FromArgb(90, Accent); cb.FlatAppearance.BorderColor = UiBorder; }
                    break;
                case ScintillaNET.Scintilla:
                    continue;
                default:
                    c.BackColor = UiBack; c.ForeColor = UiFore; break;
            }
            if (c.HasChildren) Paint(c);
        }
    }
}

/// <summary>Palette per il ToolStripProfessionalRenderer derivata dal tema.</summary>
sealed class ThemeColorTable(Theme t) : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => t.UiBack;
    public override Color MenuStripGradientEnd => t.UiBack;
    public override Color ToolStripGradientBegin => t.UiBack;
    public override Color ToolStripGradientMiddle => t.UiBack;
    public override Color ToolStripGradientEnd => t.UiBack;
    public override Color ToolStripBorder => t.UiBack;
    public override Color ToolStripDropDownBackground => t.UiBack;
    public override Color ToolStripContentPanelGradientBegin => t.UiBack;
    public override Color ToolStripContentPanelGradientEnd => t.UiBack;
    public override Color StatusStripGradientBegin => t.UiBack;
    public override Color StatusStripGradientEnd => t.UiBack;
    public override Color MenuBorder => t.UiBorder;
    public override Color MenuItemBorder => t.UiHover;
    public override Color MenuItemSelected => t.UiHover;
    public override Color MenuItemSelectedGradientBegin => t.UiHover;
    public override Color MenuItemSelectedGradientEnd => t.UiHover;
    public override Color MenuItemPressedGradientBegin => t.UiHover;
    public override Color MenuItemPressedGradientMiddle => t.UiHover;
    public override Color MenuItemPressedGradientEnd => t.UiHover;
    public override Color ImageMarginGradientBegin => t.UiBack;
    public override Color ImageMarginGradientMiddle => t.UiBack;
    public override Color ImageMarginGradientEnd => t.UiBack;
    public override Color ButtonSelectedHighlight => t.UiHover;
    public override Color ButtonSelectedGradientBegin => t.UiHover;
    public override Color ButtonSelectedGradientMiddle => t.UiHover;
    public override Color ButtonSelectedGradientEnd => t.UiHover;
    public override Color ButtonSelectedBorder => t.UiBorder;
    public override Color ButtonPressedGradientBegin => t.UiBorder;
    public override Color ButtonPressedGradientMiddle => t.UiBorder;
    public override Color ButtonPressedGradientEnd => t.UiBorder;
    public override Color ButtonPressedBorder => t.UiBorder;
    public override Color ButtonCheckedGradientBegin => Color.FromArgb(70, t.Accent);
    public override Color ButtonCheckedGradientMiddle => Color.FromArgb(70, t.Accent);
    public override Color ButtonCheckedGradientEnd => Color.FromArgb(70, t.Accent);
    public override Color CheckBackground => Color.FromArgb(70, t.Accent);
    public override Color CheckSelectedBackground => Color.FromArgb(110, t.Accent);
    public override Color CheckPressedBackground => Color.FromArgb(110, t.Accent);
    public override Color SeparatorDark => t.UiBorder;
    public override Color SeparatorLight => t.UiBorder;
    public override Color GripDark => t.UiBorder;
    public override Color GripLight => t.UiBack;
}

/// <summary>Renderer dei ToolStrip (menu, toolbar, barra di stato, menu contestuali) con i colori del tema.</summary>
sealed class ThemedRenderer : ToolStripProfessionalRenderer
{
    readonly Theme _t;
    public ThemedRenderer(Theme t) : base(new ThemeColorTable(t)) { _t = t; RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _t.UiFore : Color.FromArgb(120, _t.UiFore);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _t.UiFore;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        r.Inflate(2, 2);
        using (var bg = new SolidBrush(Color.FromArgb(70, _t.Accent))) e.Graphics.FillRectangle(bg, r);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(_t.UiFore, 1.6f);
        float x = r.X + r.Width * 0.22f, y = r.Y + r.Height * 0.52f;
        e.Graphics.DrawLines(pen, [new PointF(x, y), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.72f), new PointF(r.X + r.Width * 0.8f, r.Y + r.Height * 0.3f)]);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown) using (var p = new Pen(_t.UiBorder)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        else if (e.ToolStrip is ToolStrip and not MenuStrip and not StatusStrip)
            using (var p = new Pen(_t.UiBorder)) e.Graphics.DrawLine(p, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
    }
}

/// <summary>Icone vettoriali: glifi del font di sistema Segoe Fluent Icons (Win11) o Segoe MDL2 Assets (Win10), nitidi a qualsiasi DPI.</summary>
public static class Icons
{
    public const char New = '', Open = '', Save = '', SaveAll = '', Close = '',
        Cut = '', Copy = '', Paste = '', Undo = '', Redo = '', Find = '',
        Replace = '', ZoomIn = '', ZoomOut = '', Wrap = '', Symbols = '',
        Theme = '', Minimap = '', QuickOpen = '';

    static readonly string Family = new InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons")
        ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static Bitmap Render(char glyph, Color color, int px)
    {
        var bmp = new Bitmap(px, px);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font(Family, px * 0.78f, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(glyph.ToString(), font, brush, new RectangleF(0, 0, px, px), sf);
        return bmp;
    }
}
