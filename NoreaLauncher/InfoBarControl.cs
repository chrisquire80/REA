using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class InfoBarControl : Panel
{
    public enum BarType { Info, Success, Warning, Error }

    BarType _type = BarType.Info;
    string _message = "";
    string? _actionText;
    Action? _action;
    LauncherTheme _theme = LauncherTheme.Dark;

    readonly Button _btnAction = new() { AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
    readonly Button _btnClose = new() { Text = "✕", Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

    public InfoBarControl()
    {
        Dock = DockStyle.Top;
        Height = 38;
        Visible = false;
        DoubleBuffered = true;

        _btnAction.Click += (_, _) => _action?.Invoke();
        _btnClose.Click += (_, _) => Visible = false;

        Controls.Add(_btnAction);
        Controls.Add(_btnClose);
        Resize += (_, _) => LayoutButtons();
    }

    void LayoutButtons()
    {
        _btnClose.Location = new Point(Width - 32, (Height - 24) / 2);
        _btnAction.Location = new Point(_btnClose.Left - _btnAction.Width - 10, (Height - 24) / 2);
    }

    public void ShowMessage(string message, BarType type, LauncherTheme theme, string? actionText = null, Action? action = null)
    {
        _message = message;
        _type = type;
        _theme = theme;
        _actionText = actionText;
        _action = action;

        _btnAction.Visible = !string.IsNullOrEmpty(actionText) && action != null;
        if (_btnAction.Visible)
        {
            _btnAction.Text = actionText;
            _btnAction.BackColor = Color.Transparent;
            _btnAction.ForeColor = _theme.TextPrimary;
            _btnAction.FlatAppearance.BorderColor = _theme.BorderStrong;
        }

        _btnClose.BackColor = Color.Transparent;
        _btnClose.ForeColor = _theme.TextSecondary;
        _btnClose.FlatAppearance.BorderSize = 0;

        LayoutButtons();
        Visible = true;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color bg = _type switch
        {
            BarType.Success => Color.FromArgb(30, 20, 60, 30),
            BarType.Warning => Color.FromArgb(40, 60, 45, 15),
            BarType.Error => Color.FromArgb(40, 60, 20, 25),
            _ => Color.FromArgb(30, 25, 40, 65)
        };

        Color accent = _type switch
        {
            BarType.Success => _theme.StatusGreen,
            BarType.Warning => _theme.StatusAmber,
            BarType.Error => _theme.StatusCoral,
            _ => _theme.BrandBlue
        };

        g.Clear(bg);

        using (var border = new Pen(_theme.Border))
            g.DrawLine(border, 0, Height - 1, Width, Height - 1);

        using (var leftAccent = new SolidBrush(accent))
            g.FillRectangle(leftAccent, 0, 0, 4, Height);

        string icon = _type switch
        {
            BarType.Success => "✔",
            BarType.Warning => "⚠",
            BarType.Error => "✖",
            _ => "ℹ"
        };

        using var iconFont = new Font("Segoe UI Symbol", 10.5f, FontStyle.Bold);
        using var textFont = new Font("Segoe UI", 9f);

        TextRenderer.DrawText(g, icon, iconFont, new Point(14, 9), accent);

        int maxW = _btnAction.Visible ? _btnAction.Left - 44 : _btnClose.Left - 44;
        TextRenderer.DrawText(g, _message, textFont, new Rectangle(40, 0, maxW, Height), _theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
