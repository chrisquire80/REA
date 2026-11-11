using System.Drawing.Imaging;

namespace NoreaLauncher;

public static class PreviewExporter
{
    public static void ExportPreviews(MainForm form, string outDir)
    {
        Directory.CreateDirectory(outDir);
        form.ToggleSettings(false);
        form.PerformLayout();
        using (var bmp1 = RenderTree(form))
            bmp1.Save(Path.Combine(outDir, "norea_launcher_suite.png"), ImageFormat.Png);

        form.ToggleSettings(true);
        form.PerformLayout();
        using (var bmp2 = RenderTree(form))
            bmp2.Save(Path.Combine(outDir, "norea_launcher_settings.png"), ImageFormat.Png);

        form.QuitApplication();
    }

    public static Bitmap RenderTree(Control root)
    {
        var bmp = new Bitmap(root.Width, root.Height);
        void Walk(Control parent, Point origin, Rectangle clipRect)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)
            {
                var c = parent.Controls[i];
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                var pt = new Point(origin.X + c.Left, origin.Y + c.Top);
                var bounds = new Rectangle(pt.X, pt.Y, c.Width, c.Height);
                var visibleClip = Rectangle.Intersect(clipRect, bounds);
                if (visibleClip.Width <= 0 || visibleClip.Height <= 0) continue;

                using (var cb = new Bitmap(c.Width, c.Height))
                {
                    using (var cg = Graphics.FromImage(cb))
                    {
                        cg.Clear(c.BackColor);
                        c.InvokePaintBackground(c, new PaintEventArgs(cg, new Rectangle(0, 0, c.Width, c.Height)));
                        c.InvokePaint(c, new PaintEventArgs(cg, new Rectangle(0, 0, c.Width, c.Height)));

                        if (c is TextBox tb)
                        {
                            string t = string.IsNullOrEmpty(tb.Text) ? tb.PlaceholderText : tb.Text;
                            Color tc = string.IsNullOrEmpty(tb.Text) ? Color.FromArgb(140, 140, 140) : tb.ForeColor;
                            TextRenderer.DrawText(cg, t, tb.Font, new Point(4, (tb.Height - tb.Font.Height) / 2), tc, TextFormatFlags.NoPadding);
                        }
                        else if (c is ComboBox cbx)
                        {
                            cg.Clear(cbx.BackColor);
                            using var pen = new Pen(Color.FromArgb(70, 70, 70));
                            cg.DrawRectangle(pen, 0, 0, cbx.Width - 1, cbx.Height - 1);
                            string text = cbx.SelectedItem?.ToString() ?? cbx.Text;
                            TextRenderer.DrawText(cg, text, cbx.Font, new Point(6, (cbx.Height - cbx.Font.Height) / 2), cbx.ForeColor, TextFormatFlags.NoPadding);
                            TextRenderer.DrawText(cg, "▾", cbx.Font, new Point(cbx.Width - 18, (cbx.Height - cbx.Font.Height) / 2), Color.FromArgb(140, 140, 140), TextFormatFlags.NoPadding);
                        }
                    }
                    using var g = Graphics.FromImage(bmp);
                    g.SetClip(visibleClip);
                    g.DrawImageUnscaled(cb, pt);
                }
                if (c.HasChildren) Walk(c, pt, visibleClip);
            }
        }
        using (var g0 = Graphics.FromImage(bmp)) g0.Clear(root.BackColor);
        Walk(root, Point.Empty, new Rectangle(0, 0, root.Width, root.Height));
        return bmp;
    }
}

public static class ControlExtensions
{
    public static void InvokePaintBackground(this Control c, Control target, PaintEventArgs e)
    {
        typeof(Control).GetMethod("InvokePaintBackground", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(c, [target, e]);
    }

    public static void InvokePaint(this Control c, Control target, PaintEventArgs e)
    {
        typeof(Control).GetMethod("InvokePaint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(c, [target, e]);
    }
}
