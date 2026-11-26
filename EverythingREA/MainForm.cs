using System.Diagnostics;
using NoreaDesign;

namespace EverythingREA;

public sealed class MainForm : Form
{
    private readonly NoreaPalette _palette = NoreaStyles.CurrentPalette;
    private readonly EverythingSettings _settings;
    private readonly FileIndexService _indexService = new();
    private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 75 };
    private readonly System.Windows.Forms.Timer _liveRefreshDebounce = new() { Interval = 400 };

    private IReadOnlyList<FileIndexEntry> _results = [];
    private SortField _sortBy = SortField.Name;
    private bool _sortDescending;
    private QuickCategory _category = QuickCategory.All;
    private CancellationTokenSource? _indexCts;

    private readonly TextBox _searchBox = new();
    private readonly CheckBox _chkCase = new() { Text = "Maiuscole", AutoSize = true };
    private readonly CheckBox _chkPath = new() { Text = "Percorso", AutoSize = true };
    private readonly CheckBox _chkRegex = new() { Text = "Regex", AutoSize = true };
    private readonly Button _btnRoots = new() { Text = "Cartelle...", Width = 104, Height = 32 };
    private readonly Button _btnReindex = new() { Text = "Reindicizza", Width = 112, Height = 32 };
    private readonly FlowLayoutPanel _filterBar = new();
    private readonly Dictionary<QuickCategory, Button> _categoryButtons = [];
    private readonly VirtualListView _listView = new();
    private readonly Label _statusLabel = new();
    private readonly Label _rootsSummaryLabel = new();

    public MainForm(string initialQuery = "")
    {
        _settings = EverythingSettings.Load();

        Text = "EverythingREA — Ricerca Istantanea NOREA";
        Size = new Size(1180, 740);
        MinimumSize = new Size(860, 520);
        StartPosition = FormStartPosition.CenterScreen;
        Font = NoreaStyles.CreateUiFont();
        BackColor = _palette.Bg;
        ForeColor = _palette.TextPrimary;

        try
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(icoPath)) Icon = new Icon(icoPath);
        }
        catch { }

        BuildLayout();
        ApplyTheme();

        if (!string.IsNullOrWhiteSpace(initialQuery))
            _searchBox.Text = initialQuery;

        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            ExecuteSearch();
        };
        _liveRefreshDebounce.Tick += (_, _) =>
        {
            _liveRefreshDebounce.Stop();
            ExecuteSearch();
        };
        _indexService.IndexUpdated += () =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() =>
                {
                    _liveRefreshDebounce.Stop();
                    _liveRefreshDebounce.Start();
                });
        };

        Shown += async (_, _) => await InitializeIndexAsync();
        FormClosing += (_, _) =>
        {
            _indexCts?.Cancel();
            _indexService.SaveCache();
            _indexService.Dispose();
        };
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(16, 12, 16, 12), BackColor = _palette.HeaderBg };
        var searchContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = _palette.HeaderBg
        };
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Font = new Font(NoreaStyles.UiFontFamily, 11.5f);
        _searchBox.PlaceholderText = "Cerca file e cartelle... (es: *.cs, report | fattura, !obj, ext:json;md, size:>1mb)";
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.TextChanged += (_, _) =>
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };

        _chkCase.Margin = new Padding(10, 8, 4, 0);
        _chkPath.Margin = new Padding(6, 8, 4, 0);
        _chkRegex.Margin = new Padding(6, 8, 10, 0);
        _chkCase.CheckedChanged += (_, _) => ExecuteSearch();
        _chkPath.CheckedChanged += (_, _) => ExecuteSearch();
        _chkRegex.CheckedChanged += (_, _) => ExecuteSearch();

        _btnRoots.Margin = new Padding(4, 2, 4, 0);
        _btnReindex.Margin = new Padding(4, 2, 0, 0);
        _btnRoots.Click += async (_, _) => await EditRootsAsync();
        _btnReindex.Click += async (_, _) => await RebuildIndexAsync();

        searchContainer.Controls.Add(_searchBox, 0, 0);
        searchContainer.Controls.Add(_chkCase, 1, 0);
        searchContainer.Controls.Add(_chkPath, 2, 0);
        searchContainer.Controls.Add(_chkRegex, 3, 0);
        searchContainer.Controls.Add(_btnRoots, 4, 0);
        searchContainer.Controls.Add(_btnReindex, 5, 0);
        header.Controls.Add(searchContainer);

        _filterBar.Dock = DockStyle.Top;
        _filterBar.Height = 42;
        _filterBar.Padding = new Padding(16, 6, 16, 6);
        _filterBar.BackColor = _palette.PanelBg;

        (QuickCategory Cat, string Label)[] categories =
        [
            (QuickCategory.All, "Tutti"),
            (QuickCategory.Folders, "Cartelle"),
            (QuickCategory.Files, "File"),
            (QuickCategory.Code, "Codice"),
            (QuickCategory.Documents, "Documenti"),
            (QuickCategory.Images, "Immagini"),
            (QuickCategory.Archives, "Archivi")
        ];

        foreach (var (cat, label) in categories)
        {
            var btn = new Button
            {
                Text = label,
                Height = 28,
                AutoSize = true,
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0, 0, 6, 0),
                Tag = cat
            };
            btn.Click += (_, _) =>
            {
                _category = cat;
                UpdateCategoryButtons();
                ExecuteSearch();
            };
            _categoryButtons[cat] = btn;
            _filterBar.Controls.Add(btn);
        }

        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.VirtualMode = true;
        _listView.HideSelection = false;
        _listView.BorderStyle = BorderStyle.None;
        _listView.Columns.Add("Nome", 310);
        _listView.Columns.Add("Percorso", 470);
        _listView.Columns.Add("Dimensione", 110, HorizontalAlignment.Right);
        _listView.Columns.Add("Ultima modifica", 150);
        _listView.Columns.Add("Tipo", 90);
        _listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
        _listView.ColumnClick += OnColumnClick;
        _listView.DoubleClick += (_, _) => OpenSelected();
        _listView.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.C) { CopySelectedPath(); e.Handled = true; }
        };
        _listView.ContextMenuStrip = BuildContextMenu();

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(16, 5, 16, 5), BackColor = _palette.HeaderBg };
        _statusLabel.Dock = DockStyle.Left;
        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Inizializzazione indice...";
        _rootsSummaryLabel.Dock = DockStyle.Right;
        _rootsSummaryLabel.AutoSize = true;
        footer.Controls.Add(_statusLabel);
        footer.Controls.Add(_rootsSummaryLabel);

        Controls.Add(_listView);
        Controls.Add(_filterBar);
        Controls.Add(header);
        Controls.Add(footer);

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                _ = RebuildIndexAsync();
                e.Handled = true;
            }
        };
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri", null, (_, _) => OpenSelected());
        menu.Items.Add("Apri in NotepadREA", null, (_, _) => OpenInNotepadRea());
        menu.Items.Add("Apri cartella contenitore", null, (_, _) => OpenContainingFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copia percorso completo\tCtrl+Shift+C", null, (_, _) => CopySelectedPath());
        menu.Items.Add("Copia nome file", null, (_, _) => CopySelectedName());
        return menu;
    }

    private void ApplyTheme()
    {
        NoreaStyles.ApplyForm(this, _palette);
        _searchBox.BackColor = _palette.InputBg;
        _searchBox.ForeColor = _palette.TextPrimary;
        _chkCase.ForeColor = _palette.TextSecondary;
        _chkPath.ForeColor = _palette.TextSecondary;
        _chkRegex.ForeColor = _palette.TextSecondary;
        NoreaStyles.StyleButton(_btnRoots, _palette, NoreaButtonStyle.Secondary);
        NoreaStyles.StyleButton(_btnReindex, _palette, NoreaButtonStyle.Primary);
        _listView.BackColor = _palette.Bg;
        _listView.ForeColor = _palette.TextPrimary;
        _statusLabel.ForeColor = _palette.TextSecondary;
        _rootsSummaryLabel.ForeColor = _palette.TextMuted;
        UpdateCategoryButtons();
        UpdateRootsSummary();
    }

    private void UpdateCategoryButtons()
    {
        foreach (var kv in _categoryButtons)
        {
            bool active = kv.Key == _category;
            NoreaStyles.StyleButton(kv.Value, _palette, active ? NoreaButtonStyle.Primary : NoreaButtonStyle.Secondary);
        }
    }

    private void UpdateRootsSummary()
    {
        _rootsSummaryLabel.Text = $"Percorsi: {string.Join(" · ", _settings.IndexedRoots)}";
    }

    private async Task InitializeIndexAsync()
    {
        if (_indexService.LoadCache())
        {
            ExecuteSearch();
            _statusLabel.Text = $"Cache caricata ({_indexService.Count:N0} elementi). Aggiornamento in corso...";
        }

        await RebuildIndexAsync();
    }

    private async Task RebuildIndexAsync()
    {
        _indexCts?.Cancel();
        _indexCts = new CancellationTokenSource();
        var token = _indexCts.Token;

        _btnReindex.Enabled = false;
        _statusLabel.Text = "Indicizzazione in corso...";
        var progress = new Progress<int>(count =>
        {
            if (!IsDisposed)
                _statusLabel.Text = $"Indicizzazione: {count:N0} elementi rilevati...";
        });

        try
        {
            var sw = Stopwatch.StartNew();
            int total = await _indexService.RebuildIndexAsync(_settings, progress, token);
            sw.Stop();
            _indexService.SaveCache();
            ExecuteSearch();
            _statusLabel.Text = $"{_results.Count:N0} risultati su {total:N0} elementi (indice aggiornato in {sw.ElapsedMilliseconds:N0} ms)";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _btnReindex.Enabled = true;
        }
    }

    private void ExecuteSearch()
    {
        var sw = Stopwatch.StartNew();
        var options = SearchSession.CreateOptions(_chkCase.Checked, _chkPath.Checked, _chkRegex.Checked, _category);

        _results = _indexService.Search(_searchBox.Text, options, _sortBy, _sortDescending);
        _listView.VirtualListSize = _results.Count;
        _listView.Invalidate();
        sw.Stop();

        _statusLabel.Text = SearchSession.FormatStatus(_results.Count, _indexService.Count, sw.ElapsedMilliseconds);
    }

    private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _results.Count)
        {
            e.Item = new ListViewItem(["", "", "", "", ""]);
            return;
        }

        var entry = _results[e.ItemIndex];
        string sizeText = entry.IsDirectory ? "<DIR>" : SearchSession.FormatSize(entry.SizeBytes);
        string dateText = entry.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        string typeText = entry.IsDirectory ? "Cartella" : (string.IsNullOrEmpty(entry.Extension) ? "File" : entry.Extension.TrimStart('.').ToUpperInvariant());

        e.Item = new ListViewItem([entry.Name, entry.Directory, sizeText, dateText, typeText]);
    }

    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        (_sortBy, _sortDescending) = SearchSession.ToggleSort(_sortBy, _sortDescending, e.Column);

        ExecuteSearch();
    }

    private FileIndexEntry? GetSelectedEntry()
    {
        if (_listView.SelectedIndices.Count == 0) return null;
        int idx = _listView.SelectedIndices[0];
        return idx >= 0 && idx < _results.Count ? _results[idx] : null;
    }

    private void OpenSelected()
    {
        var entry = GetSelectedEntry();
        if (entry == null) return;
        try
        {
            Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossibile aprire '{entry.Name}':\n{ex.Message}", "EverythingREA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenInNotepadRea()
    {
        var entry = GetSelectedEntry();
        if (entry == null || entry.IsDirectory) return;

        string? notepadExe = FindNotepadRea();
        if (notepadExe == null)
        {
            OpenSelected();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(notepadExe, $"\"{entry.FullPath}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossibile avviare NotepadREA:\n{ex.Message}", "EverythingREA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string? FindNotepadRea()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "NotepadREA.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NotepadREA", "NotepadREA.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NOREA", "NotepadREA.exe")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private void OpenContainingFolder()
    {
        var entry = GetSelectedEntry();
        if (entry == null) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry.FullPath}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void CopySelectedPath()
    {
        var entry = GetSelectedEntry();
        if (entry != null) Clipboard.SetText(entry.FullPath);
    }

    private void CopySelectedName()
    {
        var entry = GetSelectedEntry();
        if (entry != null) Clipboard.SetText(entry.Name);
    }

    private async Task EditRootsAsync()
    {
        using var dialog = new Form
        {
            Text = "Cartelle indicizzate — EverythingREA",
            Size = new Size(620, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = _palette.Bg,
            ForeColor = _palette.TextPrimary
        };

        var info = new Label
        {
            Text = "Inserisci un percorso o disco per riga (es. C:\\ o D:\\Progetti):",
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(12, 10, 12, 0),
            ForeColor = _palette.TextSecondary
        };
        var txt = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = NoreaStyles.CreateDataFont(),
            BackColor = _palette.InputBg,
            ForeColor = _palette.TextPrimary,
            Text = string.Join(Environment.NewLine, _settings.IndexedRoots)
        };
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = _palette.HeaderBg
        };
        var btnOk = new Button { Text = "Salva e reindicizza", Width = 150, Height = 32, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Annulla", Width = 95, Height = 32, DialogResult = DialogResult.Cancel };
        var btnAddFolder = new Button { Text = "Sfoglia...", Width = 95, Height = 32 };
        btnAddFolder.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog(dialog) == DialogResult.OK)
                txt.AppendText((txt.TextLength > 0 ? Environment.NewLine : "") + fbd.SelectedPath);
        };

        NoreaStyles.StyleButton(btnOk, _palette, NoreaButtonStyle.Primary);
        NoreaStyles.StyleButton(btnCancel, _palette, NoreaButtonStyle.Secondary);
        NoreaStyles.StyleButton(btnAddFolder, _palette, NoreaButtonStyle.Secondary);
        bar.Controls.AddRange([btnOk, btnCancel, btnAddFolder]);

        dialog.Controls.Add(txt);
        dialog.Controls.Add(info);
        dialog.Controls.Add(bar);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var updated = EverythingSettings.NormalizeRoots(txt.Lines);
            if (updated.Count > 0)
            {
                _settings.IndexedRoots = updated;
                _settings.Save();
                UpdateRootsSummary();
                await RebuildIndexAsync();
            }
        }
    }

    private sealed class VirtualListView : ListView
    {
        public VirtualListView() => DoubleBuffered = true;
    }
}
