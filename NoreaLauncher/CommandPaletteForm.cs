using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NoreaLauncher;

public sealed class CommandPaletteForm : Form
{
    readonly List<AppEntry> _apps;
    readonly LauncherTheme _theme;
    readonly TextBox _searchBox = new() { BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 12f) };
    readonly ListBox _listBox = new() { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 44, IntegralHeight = false };

    public sealed record ActionItem(string Title, string Subtitle, Action Execute);
    readonly List<ActionItem> _items = [];

    public CommandPaletteForm(List<AppEntry> apps, LauncherTheme theme, Action onSyncCatalog, Action onOpenSettings, Action onOpenWorkspace)
    {
        _apps = apps;
        _theme = theme;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(540, 360);
        KeyPreview = true;
        DoubleBuffered = true;

        BackColor = _theme.BorderStrong;
        Padding = new Padding(1);

        // Prepara azioni
        foreach (var app in _apps)
        {
            var a = app;
            _items.Add(new ActionItem($"Avvia {a.DisplayName}", $"{a.Runtime} · {a.Tagline}", () =>
            {
                var exe = a.InstalledExePath ?? a.DistExePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
                    });
                }
            }));
            if (a.ResolvedAnalysisFile != null)
            {
                _items.Add(new ActionItem($"Apri Analisi REA di {a.DisplayName}", a.ResolvedAnalysisFile, () =>
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(a.ResolvedAnalysisFile) { UseShellExecute = true });
                }));
            }
        }
        _items.Add(new ActionItem("Sincronizza Catalogo", "Ricarica e verifica lo stato dei binari", onSyncCatalog));
        _items.Add(new ActionItem("Apri Cartella Workspace NOREA", "Esplora file di progetto e binari dist/", onOpenWorkspace));
        _items.Add(new ActionItem("Impostazioni Launcher", "Configura lingua, tema e cartella di installazione", onOpenSettings));

        var root = new Panel { Dock = DockStyle.Fill, BackColor = _theme.CardBg, Padding = new Padding(12) };

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 6), BackColor = _theme.InputBg };
        topPanel.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.Border);
            e.Graphics.DrawRectangle(p, 0, 0, topPanel.Width - 1, topPanel.Height - 1);
        };
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BackColor = _theme.InputBg;
        _searchBox.ForeColor = _theme.TextPrimary;
        _searchBox.PlaceholderText = "Cerca comandi o applicazioni...";
        topPanel.Controls.Add(_searchBox);

        _listBox.Dock = DockStyle.Fill;
        _listBox.BackColor = _theme.CardBg;
        _listBox.ForeColor = _theme.TextPrimary;
        _listBox.DrawItem += OnDrawItem;
        _listBox.DoubleClick += (_, _) => ExecuteSelected();

        _searchBox.TextChanged += (_, _) => Filter();
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                if (_listBox.SelectedIndex < _listBox.Items.Count - 1)
                    _listBox.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_listBox.SelectedIndex > 0)
                    _listBox.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };

        var listContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
        listContainer.Controls.Add(_listBox);

        root.Controls.Add(listContainer);
        root.Controls.Add(topPanel);
        Controls.Add(root);

        Filter();
    }

    void Filter()
    {
        string q = _searchBox.Text.Trim();
        _listBox.BeginUpdate();
        _listBox.Items.Clear();
        foreach (var item in _items)
        {
            if (string.IsNullOrEmpty(q) || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || item.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase))
                _listBox.Items.Add(item);
        }
        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;
        _listBox.EndUpdate();
    }

    void ExecuteSelected()
    {
        if (_listBox.SelectedItem is ActionItem item)
        {
            Close();
            item.Execute();
        }
    }

    void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _listBox.Items.Count) return;
        var item = (ActionItem)_listBox.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;

        var g = e.Graphics;
        g.Clear(selected ? _theme.CardHoverBg : _theme.CardBg);

        if (selected)
        {
            using var activeBrush = new SolidBrush(_theme.BrandBlue);
            g.FillRectangle(activeBrush, 0, e.Bounds.Y + 4, 3, e.Bounds.Height - 8);
        }

        using var fTitle = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var fSub = new Font("Segoe UI", 8.2f);

        TextRenderer.DrawText(g, item.Title, fTitle, new Point(e.Bounds.X + 12, e.Bounds.Y + 4), _theme.TextPrimary);
        TextRenderer.DrawText(g, item.Subtitle, fSub, new Point(e.Bounds.X + 12, e.Bounds.Y + 23), _theme.TextSecondary);
    }
}
