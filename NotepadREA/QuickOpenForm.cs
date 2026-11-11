namespace NotepadREA;

/// <summary>
/// Apertura rapida (Ctrl+P): filtra documenti aperti, file recenti e file della cartella del documento corrente.
/// Scrivere ":123" salta alla riga 123 del documento corrente.
/// </summary>
public sealed class QuickOpenForm : Form
{
    public sealed record Entry(string Path, string Source);

    readonly TextBox _box = new() { Dock = DockStyle.Top, Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle };
    readonly ListBox _list = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38, IntegralHeight = false };
    readonly List<Entry> _all;
    readonly Theme _theme;

    public string? SelectedPath { get; private set; }
    public int? GotoLine { get; private set; }

    public QuickOpenForm(IEnumerable<Entry> entries, Theme theme)
    {
        _theme = theme;
        _all = entries.DistinctBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
        Text = "Apertura rapida";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        Size = new Size(620, 420);
        Padding = new Padding(1);
        BackColor = theme.UiBorder;

        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = theme.UiBack };
        var hint = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(150, theme.UiFore), BackColor = theme.UiBack,
            Text = "Invio apre · ↑↓ naviga · Esc chiude · \":numero\" va alla riga", TextAlign = ContentAlignment.MiddleLeft };
        _box.BackColor = theme.Back; _box.ForeColor = theme.Fore;
        _list.BackColor = theme.UiBack; _list.ForeColor = theme.UiFore;
        inner.Controls.Add(_list);
        inner.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = theme.UiBack });
        inner.Controls.Add(_box);
        inner.Controls.Add(hint);
        Controls.Add(inner);

        _box.TextChanged += (_, _) => Filter();
        _list.DrawItem += DrawItem;
        _list.DoubleClick += (_, _) => Accept();
        KeyDown += OnKey;
        Deactivate += (_, _) => { if (DialogResult == DialogResult.None) Close(); };
        _list.TabStop = false;
        ActiveControl = _box;
        Shown += (_, _) => _box.Focus();
        Filter();
    }

    void OnKey(object? s, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); break;
            case Keys.Enter: Accept(); break;
            case Keys.Down: if (_list.Items.Count > 0) _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1); break;
            case Keys.Up: if (_list.Items.Count > 0) _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0); break;
            default: return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }

    void Filter()
    {
        var q = _box.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        if (q.StartsWith(':'))
        {
            _list.EndUpdate();
            return;
        }
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = _all
            .Where(e => terms.All(t => e.Path.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => terms.Length > 0 && Path.GetFileName(e.Path).StartsWith(terms[0], StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => terms.All(t => Path.GetFileName(e.Path).Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Take(200);
        foreach (var h in hits) _list.Items.Add(h);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        _list.EndUpdate();
    }

    void DrawItem(object? s, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var entry = (Entry)_list.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(sel ? Color.FromArgb(90, _theme.Accent) : _theme.UiBack)) e.Graphics.FillRectangle(bg, e.Bounds);
        var r = e.Bounds; r.Inflate(-8, 0);
        using var bold = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var small = new Font("Segoe UI", 8.5f);
        TextRenderer.DrawText(e.Graphics, Path.GetFileName(entry.Path) + "   " + entry.Source, bold, new Rectangle(r.X, r.Y + 2, r.Width, 18), _theme.UiFore, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics, Path.GetDirectoryName(entry.Path), small, new Rectangle(r.X, r.Y + 20, r.Width, 16), Color.FromArgb(150, _theme.UiFore), TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
    }

    void Accept()
    {
        var q = _box.Text.Trim();
        if (q.StartsWith(':') && int.TryParse(q[1..], out int line)) GotoLine = line;
        else if (_list.SelectedItem is Entry e) SelectedPath = e.Path;
        else return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
