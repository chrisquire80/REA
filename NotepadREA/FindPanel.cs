using ScintillaNET;

namespace NotepadREA;

/// <summary>
/// Barra Trova/Sostituisci ancorata in basso: non copre il testo, ricerca incrementale mentre si scrive,
/// conteggio live dei risultati, evidenzia tutto, maiuscole/parola intera/regex, sostituisci e sostituisci tutto.
/// Invio = successivo, Maiusc+Invio = precedente, Esc = chiudi.
/// </summary>
public sealed class FindPanel : UserControl
{
    public const int MarkIndicator = 8;
    const int CountLimit = 100_000;

    readonly Func<Scintilla?> _editor;
    readonly TextBox _find = new() { Width = 260 };
    readonly TextBox _replace = new() { Width = 260 };
    readonly CheckBox _case = Toggle("Aa", "Maiuscole/minuscole");
    readonly CheckBox _word = Toggle("ab|", "Solo parole intere");
    readonly CheckBox _regex = Toggle(".*", "Espressione regolare (C++11/ECMAScript)");
    readonly CheckBox _mark = Toggle("Ev", "Evidenzia tutte le occorrenze");
    readonly CheckBox _inSel = Toggle("Sel", "Solo nella selezione");
    readonly Label _count = new() { AutoSize = true, Padding = new Padding(6, 6, 6, 0), MinimumSize = new Size(130, 0) };
    readonly FlowLayoutPanel _row2;
    readonly System.Windows.Forms.Timer _debounce = new() { Interval = 120 };
    readonly ToolTip _tips = new();
    int _anchor;
    Theme _theme = Theme.Light;

    public FindPanel(Func<Scintilla?> editor)
    {
        _editor = editor;
        Dock = DockStyle.Bottom;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(6, 4, 6, 4);
        Visible = false;

        var prev = Btn("▲", "Precedente (Maiusc+Invio)", () => FindNext(false));
        var next = Btn("▼", "Successivo (Invio)", () => FindNext(true));
        var close = Btn("✕", "Chiudi (Esc)", Close);

        var row1 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        row1.Controls.AddRange([Lbl("Trova:"), _find, prev, next, _case, _word, _regex, _mark, _inSel, _count, close]);
        _row2 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        _row2.Controls.AddRange([Lbl("Sostituisci:"), _replace,
            Btn("Sostituisci", "Sostituisci l'occorrenza selezionata e passa alla successiva", ReplaceOne, 90),
            Btn("Sostituisci tutto", "Sostituisci tutte le occorrenze (annullabile in un passo)", ReplaceAll, 120)]);
        var stack = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        stack.Controls.Add(row1, 0, 0);
        stack.Controls.Add(_row2, 0, 1);
        Controls.Add(stack);

        foreach (var cb in new[] { _case, _word, _regex, _inSel }) { cb.CheckedChanged += (_, _) => Live(); _tips.SetToolTip(cb, (string)cb.Tag!); }
        _tips.SetToolTip(_mark, (string)_mark.Tag!);
        _mark.CheckedChanged += (_, _) => { if (_mark.Checked) MarkAll(); else ClearMarks(); };
        _find.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Live(incremental: true); };
        _find.KeyDown += OnKey;
        _replace.KeyDown += OnKey;
    }

    static CheckBox Toggle(string text, string tip) => new()
    {
        Text = text, Tag = tip, Appearance = Appearance.Button, AutoSize = false, Size = new Size(34, 24),
        TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(2, 1, 2, 1),
    };

    Button Btn(string text, string tip, Action a, int width = 28)
    {
        var b = new Button { Text = text, Size = new Size(width, 24), Margin = new Padding(2, 1, 2, 1) };
        b.Click += (_, _) => a();
        _tips.SetToolTip(b, tip);
        return b;
    }

    static Label Lbl(string t) => new() { Text = t, AutoSize = true, Padding = new Padding(0, 6, 0, 0), MinimumSize = new Size(72, 0) };

    public bool HasSearch => !string.IsNullOrEmpty(_find.Text);

    public void SetTheme(Theme t)
    {
        _theme = t;
        BackColor = t.UiBack;
        ForeColor = t.UiFore;
        t.Paint(this);
        UpdateCountColor(false);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(_theme.UiBorder);
        e.Graphics.DrawLine(p, 0, 0, Width, 0);
    }

    public void Open(bool replace)
    {
        var sci = _editor();
        _row2.Visible = replace;
        Visible = true;
        if (sci != null)
        {
            var sel = sci.SelectedText;
            if (!string.IsNullOrEmpty(sel) && !sel.Contains('\n') && sel.Length < 300) _find.Text = sel;
            _anchor = sci.SelectionStart;
        }
        (replace && HasSearch ? _replace : _find).Focus();
        _find.SelectAll();
        Live();
    }

    public void Close()
    {
        Visible = false;
        _editor()?.Focus();
    }

    public void RefreshSearch()
    {
        if (!Visible) return;
        _anchor = _editor()?.SelectionStart ?? 0;
        Live();
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter when sender == _replace && !e.Shift: ReplaceOne(); break;
            case Keys.Enter: FindNext(!e.Shift); break;
            case Keys.Escape: Close(); break;
            case Keys.F3: FindNext(!e.Shift); break;
            default: return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }

    SearchFlags Flags =>
        (_case.Checked ? SearchFlags.MatchCase : SearchFlags.None) |
        (_word.Checked ? SearchFlags.WholeWord : SearchFlags.None) |
        (_regex.Checked ? SearchFlags.Regex | SearchFlags.Cxx11Regex : SearchFlags.None);

    (int Start, int End) Range(Scintilla sci) =>
        _inSel.Checked && sci.SelectionStart != sci.SelectionEnd ? (sci.SelectionStart, sci.SelectionEnd) : (0, sci.TextLength);

    /// <summary>Aggiorna conteggio ed evidenziazione; se incrementale seleziona la prima occorrenza dal punto di partenza.</summary>
    void Live(bool incremental = false)
    {
        var sci = _editor();
        if (sci is null) return;
        if (!HasSearch) { _count.Text = ""; ClearMarks(); return; }
        int n = Count(sci, out bool invalid);
        if (invalid) { _count.Text = "Regex non valida"; UpdateCountColor(true); return; }
        _count.Text = n == 0 ? "Nessun risultato" : n >= CountLimit ? $"{CountLimit:N0}+ risultati" : $"{n:N0} risultat{(n == 1 ? "o" : "i")}";
        UpdateCountColor(n == 0);
        if (_mark.Checked) MarkAll();
        if (incremental && n > 0 && !_inSel.Checked)
        {
            sci.SearchFlags = Flags;
            if (Search(sci, _anchor, sci.TextLength) < 0) Search(sci, 0, _anchor);
            if (sci.TargetEnd > sci.TargetStart) { sci.SetSelection(sci.TargetEnd, sci.TargetStart); sci.ScrollRange(sci.TargetStart, sci.TargetEnd); }
        }
    }

    void UpdateCountColor(bool bad) => _count.ForeColor = bad ? _theme.Dirty : _theme.UiFore;

    int Search(Scintilla sci, int start, int end)
    {
        sci.TargetStart = start;
        sci.TargetEnd = end;
        return sci.SearchInTarget(_find.Text);
    }

    int Count(Scintilla sci, out bool invalid)
    {
        invalid = false;
        sci.SearchFlags = Flags;
        var (start, end) = Range(sci);
        int n = 0;
        while (n < CountLimit && start <= end)
        {
            int r = Search(sci, start, end);
            if (r == -2) { invalid = true; return 0; }
            if (r < 0) break;
            n++;
            start = sci.TargetEnd > sci.TargetStart ? sci.TargetEnd : sci.TargetEnd + 1;
        }
        return n;
    }

    public void FindNext(bool forward)
    {
        var sci = _editor();
        if (sci is null) return;
        if (!HasSearch) { Open(false); return; }
        sci.SearchFlags = Flags;
        int from = forward ? sci.SelectionEnd : sci.SelectionStart;
        int pos = Search(sci, from, forward ? sci.TextLength : 0);
        if (pos >= 0 && sci.TargetStart == sci.TargetEnd && sci.TargetStart == from)
        {
            int step = forward ? from + 1 : from - 1;
            pos = step >= 0 && step <= sci.TextLength ? Search(sci, step, forward ? sci.TextLength : 0) : -1;
        }
        if (pos == -1) pos = Search(sci, forward ? 0 : sci.TextLength, from); // ricerca circolare
        if (pos < 0) { Live(); return; }
        sci.SetSelection(sci.TargetEnd, sci.TargetStart);
        sci.ScrollRange(sci.TargetStart, sci.TargetEnd);
        _anchor = sci.TargetStart;
    }

    void MarkAll()
    {
        var sci = _editor();
        if (sci is null) return;
        ClearMarks();
        if (!HasSearch) return;
        sci.IndicatorCurrent = MarkIndicator;
        var ind = sci.Indicators[MarkIndicator];
        ind.Style = IndicatorStyle.RoundBox;
        ind.Under = true;
        ind.Alpha = 110;
        ind.OutlineAlpha = 200;
        sci.SearchFlags = Flags;
        var (start, end) = Range(sci);
        int n = 0;
        while (n++ < CountLimit && start <= end && Search(sci, start, end) >= 0)
        {
            if (sci.TargetEnd > sci.TargetStart) sci.IndicatorFillRange(sci.TargetStart, sci.TargetEnd - sci.TargetStart);
            start = sci.TargetEnd > sci.TargetStart ? sci.TargetEnd : sci.TargetEnd + 1;
        }
    }

    public void ClearMarks()
    {
        var sci = _editor();
        if (sci is null) return;
        sci.IndicatorCurrent = MarkIndicator;
        sci.IndicatorClearRange(0, sci.TextLength);
    }

    void ReplaceOne()
    {
        var sci = _editor();
        if (sci is null || !HasSearch || sci.ReadOnly) return;
        sci.SearchFlags = Flags;
        if (sci.SelectionStart != sci.SelectionEnd && Search(sci, sci.SelectionStart, sci.SelectionEnd) == sci.SelectionStart && sci.TargetEnd == sci.SelectionEnd)
        {
            int len = _regex.Checked ? sci.ReplaceTargetRe(_replace.Text) : sci.ReplaceTarget(_replace.Text);
            sci.SetSelection(sci.TargetStart + len, sci.TargetStart + len);
        }
        FindNext(true);
        Live();
    }

    void ReplaceAll()
    {
        var sci = _editor();
        if (sci is null || !HasSearch) return;
        if (sci.ReadOnly) { _count.Text = "Sola lettura"; UpdateCountColor(true); return; }
        sci.SearchFlags = Flags;
        var (start, end) = Range(sci);
        int count = 0;
        sci.BeginUndoAction();
        try
        {
            while (start <= end && Search(sci, start, end) >= 0)
            {
                int matchLen = sci.TargetEnd - sci.TargetStart;
                int len = _regex.Checked ? sci.ReplaceTargetRe(_replace.Text) : sci.ReplaceTarget(_replace.Text);
                count++;
                end += len - matchLen;
                start = sci.TargetStart + len + (matchLen == 0 ? 1 : 0);
            }
        }
        finally { sci.EndUndoAction(); }
        Live();
        _count.Text = $"{count:N0} sostituit{(count == 1 ? "a" : "e")}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _debounce.Dispose(); _tips.Dispose(); }
        base.Dispose(disposing);
    }
}
