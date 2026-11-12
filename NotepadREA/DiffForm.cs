using System.Drawing.Drawing2D;
using ScintillaNET;

namespace NotepadREA;

public sealed class DiffForm : Form
{
    private const int MarkerModified = 28;
    private const int MarkerInserted = 29;
    private const int MarkerDeleted = 30;

    private readonly ComboBox _cbLeft = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _cbRight = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly Button _btnBrowseLeft = new() { Text = "Sfoglia...", AutoSize = true };
    private readonly Button _btnBrowseRight = new() { Text = "Sfoglia...", AutoSize = true };
    private readonly Button _btnPrev = new() { Text = "▲ Prec (Shift+F7)", AutoSize = true };
    private readonly Button _btnNext = new() { Text = "▼ Succ (F7)", AutoSize = true };
    private readonly Button _btnSwap = new() { Text = "⇄ Inverti", AutoSize = true };
    private readonly Button _btnRefresh = new() { Text = "⟳ Aggiorna (F5)", AutoSize = true };
    private readonly Label _lblStats = new() { AutoSize = true, Text = "Calcolo diff in corso..." };

    private readonly Scintilla _sciLeft = new() { Dock = DockStyle.Fill, BorderStyle = ScintillaNET.BorderStyle.None };
    private readonly Scintilla _sciRight = new() { Dock = DockStyle.Fill, BorderStyle = ScintillaNET.BorderStyle.None };
    private readonly SplitContainer _splitter = new() { Dock = DockStyle.Fill, SplitterWidth = 6 };

    private readonly IReadOnlyList<Document> _docs;
    private readonly Theme _theme;
    private bool _syncing;
    private readonly List<int> _diffLineIndices = [];
    private int _currentDiffIndex = -1;

    public DiffForm(IReadOnlyList<Document> docs, Document? activeDoc, Theme theme)
    {
        _docs = docs;
        _theme = theme;

        Text = "Confronta File (Side-by-Side Diff) — NotepadREA";
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterParent;
        Icon = activeDoc?.FindForm()?.Icon ?? SystemIcons.Application;
        KeyPreview = true;

        SetupUI();
        PopulateCombos(activeDoc);
        ApplyTheme();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F7 && !e.Shift) { JumpToNextDiff(1); e.Handled = true; }
            else if (e.KeyCode == Keys.F7 && e.Shift) { JumpToNextDiff(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { RunDiff(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        };
    }

    private void SetupUI()
    {
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 7, 8, 4),
            BackColor = _theme.UiBack,
            WrapContents = false,
            AutoScroll = true
        };

        var lblL = new Label { Text = "Sinistra:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = _theme.UiFore };
        var lblR = new Label { Text = "Destra:", AutoSize = true, Margin = new Padding(12, 5, 4, 0), ForeColor = _theme.UiFore };

        _btnBrowseLeft.Click += (_, _) => BrowseForFile(_cbLeft);
        _btnBrowseRight.Click += (_, _) => BrowseForFile(_cbRight);
        _btnPrev.Click += (_, _) => JumpToNextDiff(-1);
        _btnNext.Click += (_, _) => JumpToNextDiff(1);
        _btnSwap.Click += (_, _) => SwapSides();
        _btnRefresh.Click += (_, _) => RunDiff();

        _cbLeft.SelectedIndexChanged += (_, _) => RunDiff();
        _cbRight.SelectedIndexChanged += (_, _) => RunDiff();

        _lblStats.Margin = new Padding(16, 6, 0, 0);
        _lblStats.ForeColor = _theme.UiFore;

        topPanel.Controls.AddRange([
            lblL, _cbLeft, _btnBrowseLeft,
            lblR, _cbRight, _btnBrowseRight,
            _btnPrev, _btnNext, _btnSwap, _btnRefresh, _lblStats
        ]);

        ConfigureEditor(_sciLeft);
        ConfigureEditor(_sciRight);

        // Scroll sincronizzato
        _sciLeft.UpdateUI += (_, a) =>
        {
            if (_syncing || (a.Change & UpdateChange.VScroll) == 0) return;
            _syncing = true;
            try
            {
                int firstLine = _sciLeft.FirstVisibleLine;
                if (_sciRight.FirstVisibleLine != firstLine)
                {
                    _sciRight.FirstVisibleLine = firstLine;
                }
            }
            finally { _syncing = false; }
        };

        _sciRight.UpdateUI += (_, a) =>
        {
            if (_syncing || (a.Change & UpdateChange.VScroll) == 0) return;
            _syncing = true;
            try
            {
                int firstLine = _sciRight.FirstVisibleLine;
                if (_sciLeft.FirstVisibleLine != firstLine)
                {
                    _sciLeft.FirstVisibleLine = firstLine;
                }
            }
            finally { _syncing = false; }
        };

        _splitter.Panel1.Controls.Add(_sciLeft);
        _splitter.Panel2.Controls.Add(_sciRight);

        Controls.Add(_splitter);
        Controls.Add(topPanel);

        Load += (_, _) =>
        {
            _splitter.SplitterDistance = _splitter.Width / 2;
            RunDiff();
        };
    }

    private void ConfigureEditor(Scintilla sci)
    {
        sci.ReadOnly = true;
        sci.Margins[0].Type = MarginType.Number;
        sci.Margins[0].Width = 42;
        sci.Margins[1].Width = 0;
        sci.Margins[2].Width = 0;
        sci.ScrollWidthTracking = true;
        sci.ScrollWidth = 1;

        // Configurazione marker visivi di riga
        // Modificato: ambra/arancio tenue
        var mMod = sci.Markers[MarkerModified];
        mMod.Symbol = MarkerSymbol.Background;
        mMod.SetBackColor(_theme.IsDark ? Color.FromArgb(70, 60, 20) : Color.FromArgb(255, 245, 190));

        // Inserito/Aggiunto: verde tenue
        var mIns = sci.Markers[MarkerInserted];
        mIns.Symbol = MarkerSymbol.Background;
        mIns.SetBackColor(_theme.IsDark ? Color.FromArgb(25, 60, 30) : Color.FromArgb(215, 245, 215));

        // Rimosso/Cancellato: rosso tenue
        var mDel = sci.Markers[MarkerDeleted];
        mDel.Symbol = MarkerSymbol.Background;
        mDel.SetBackColor(_theme.IsDark ? Color.FromArgb(70, 25, 25) : Color.FromArgb(255, 215, 215));
    }

    private void PopulateCombos(Document? activeDoc)
    {
        _cbLeft.Items.Clear();
        _cbRight.Items.Clear();

        foreach (var doc in _docs)
        {
            var item = new DocComboItem(doc.DisplayName, doc.FilePath, doc);
            _cbLeft.Items.Add(item);
            _cbRight.Items.Add(item);
        }

        if (_docs.Count > 0)
        {
            int activeIdx = activeDoc != null ? _docs.ToList().IndexOf(activeDoc) : 0;
            _cbLeft.SelectedIndex = Math.Max(0, activeIdx);
            _cbRight.SelectedIndex = _docs.Count > 1 ? (activeIdx == 0 ? 1 : 0) : 0;
        }
    }

    private void BrowseForFile(ComboBox cb)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Seleziona file da confrontare",
            Filter = "Tutti i file (*.*)|*.*|Sorgenti (*.cs;*.cpp;*.py;*.js;*.json;*.xml)|*.cs;*.cpp;*.py;*.js;*.json;*.xml"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            var item = new DocComboItem(Path.GetFileName(ofd.FileName), ofd.FileName, null);
            cb.Items.Insert(0, item);
            cb.SelectedIndex = 0;
        }
    }

    public void LoadFiles(string fileA, string fileB)
    {
        if (File.Exists(fileA))
        {
            var itemA = new DocComboItem(Path.GetFileName(fileA), fileA, null);
            _cbLeft.Items.Insert(0, itemA);
            _cbLeft.SelectedIndex = 0;
        }
        if (File.Exists(fileB))
        {
            var itemB = new DocComboItem(Path.GetFileName(fileB), fileB, null);
            _cbRight.Items.Insert(0, itemB);
            _cbRight.SelectedIndex = 0;
        }
        RunDiff();
    }

    private void SwapSides()
    {
        var tempIdx = _cbLeft.SelectedIndex;
        var tempItems = _cbLeft.SelectedItem;
        _cbLeft.SelectedItem = _cbRight.SelectedItem;
        _cbRight.SelectedItem = tempItems;
    }

    private void RunDiff()
    {
        string leftText = GetTextFromCombo(_cbLeft);
        string rightText = GetTextFromCombo(_cbRight);

        var diff = DiffEngine.ComputeDiff(leftText, rightText);

        _sciLeft.ReadOnly = false;
        _sciRight.ReadOnly = false;

        _sciLeft.Text = "";
        _sciRight.Text = "";

        _sciLeft.MarkerDeleteAll(MarkerModified);
        _sciLeft.MarkerDeleteAll(MarkerInserted);
        _sciLeft.MarkerDeleteAll(MarkerDeleted);

        _sciRight.MarkerDeleteAll(MarkerModified);
        _sciRight.MarkerDeleteAll(MarkerInserted);
        _sciRight.MarkerDeleteAll(MarkerDeleted);

        _diffLineIndices.Clear();
        _currentDiffIndex = -1;

        int addedCount = 0;
        int deletedCount = 0;
        int modifiedCount = 0;

        var sbLeft = new System.Text.StringBuilder();
        var sbRight = new System.Text.StringBuilder();

        for (int i = 0; i < diff.Count; i++)
        {
            var line = diff[i];
            sbLeft.AppendLine(line.LeftText ?? "");
            sbRight.AppendLine(line.RightText ?? "");
        }

        _sciLeft.Text = sbLeft.ToString();
        _sciRight.Text = sbRight.ToString();

        _sciLeft.ReadOnly = true;
        _sciRight.ReadOnly = true;

        for (int i = 0; i < diff.Count; i++)
        {
            var line = diff[i];
            switch (line.Type)
            {
                case DiffLineType.Modified:
                    _sciLeft.Lines[i].MarkerAdd(MarkerModified);
                    _sciRight.Lines[i].MarkerAdd(MarkerModified);
                    _diffLineIndices.Add(i);
                    modifiedCount++;
                    break;
                case DiffLineType.Inserted:
                    _sciRight.Lines[i].MarkerAdd(MarkerInserted);
                    _diffLineIndices.Add(i);
                    addedCount++;
                    break;
                case DiffLineType.Deleted:
                    _sciLeft.Lines[i].MarkerAdd(MarkerDeleted);
                    _diffLineIndices.Add(i);
                    deletedCount++;
                    break;
            }
        }

        _lblStats.Text = $"Differenze: {modifiedCount} modificate, {addedCount} aggiunte, {deletedCount} eliminate (Totale: {_diffLineIndices.Count})";
    }

    private void JumpToNextDiff(int direction)
    {
        if (_diffLineIndices.Count == 0) return;

        _currentDiffIndex += direction;
        if (_currentDiffIndex >= _diffLineIndices.Count) _currentDiffIndex = 0;
        if (_currentDiffIndex < 0) _currentDiffIndex = _diffLineIndices.Count - 1;

        int line = _diffLineIndices[_currentDiffIndex];
        _sciLeft.Lines[line].Goto();
        _sciRight.Lines[line].Goto();
    }

    private static string GetTextFromCombo(ComboBox cb)
    {
        if (cb.SelectedItem is DocComboItem item)
        {
            if (item.Doc != null) return item.Doc.Editor.Text;
            if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
            {
                try { return File.ReadAllText(item.FilePath); } catch { }
            }
        }
        return "";
    }

    private void ApplyTheme()
    {
        BackColor = _theme.UiBack;
        _splitter.BackColor = _theme.UiBorder;
        _cbLeft.BackColor = _theme.UiBack;
        _cbLeft.ForeColor = _theme.UiFore;
        _cbRight.BackColor = _theme.UiBack;
        _cbRight.ForeColor = _theme.UiFore;

        Button[] buttons = [_btnBrowseLeft, _btnBrowseRight, _btnPrev, _btnNext, _btnSwap, _btnRefresh];
        foreach (var btn in buttons)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = _theme.UiBorder;
            btn.BackColor = _theme.UiBack;
            btn.ForeColor = _theme.UiFore;
        }

        Languages.Apply(_sciLeft, Languages.Normal, _theme, "Consolas", 10);
        Languages.Apply(_sciRight, Languages.Normal, _theme, "Consolas", 10);
    }

    private sealed record DocComboItem(string Name, string? FilePath, Document? Doc)
    {
        public override string ToString() => string.IsNullOrEmpty(FilePath) ? Name : $"{Name} ({FilePath})";
    }
}
