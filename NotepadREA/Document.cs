using System.Text;
using ScintillaNET;

namespace NotepadREA;

/// <summary>Un documento aperto: il suo editor Scintilla e i metadati del file.</summary>
public sealed class Document : UserControl
{
    public const int BookmarkMarker = 24;
    public const int SmartIndicator = 9;
    const int BookmarkMargin = 1, FoldMargin = 2, SeparatorMargin = 3;

    static int _untitledCounter;

    public Scintilla Editor { get; }
    public string? FilePath { get; private set; }
    public FileEncoding Encoding { get; set; } = FileEncoding.Utf8;
    public Language Language { get; private set; } = Languages.Normal;
    public DateTime LastWriteUtc { get; private set; }
    public string DisplayName { get; private set; }
    public bool HasMixedEol { get; private set; }
    public bool WasDetectedAsBinary { get; private set; }

    /// <summary>File di backup (sessione/recupero) associato a questo buffer.</summary>
    public string? BackupFile { get; set; }
    /// <summary>Il contenuto è cambiato dopo l'ultimo backup.</summary>
    public bool DirtySinceBackup { get; set; }
    /// <summary>L'utente ha già risposto all'avviso "file eliminato".</summary>
    public bool MissingAcknowledged { get; set; }

    bool _forcedDirty;
    Theme _theme = Theme.Light;

    public event EventHandler? StateChanged;

    public Document()
    {
        DisplayName = $"nuovo {++_untitledCounter}";
        Dock = DockStyle.Fill;
        Editor = new Scintilla { Dock = DockStyle.Fill, BorderStyle = ScintillaNET.BorderStyle.None };
        Controls.Add(Editor);
        ConfigureEditor();
        Editor.SavePointLeft += (_, _) => RaiseState();
        Editor.SavePointReached += (_, _) => RaiseState();
        Editor.Insert += (_, _) => DirtySinceBackup = true;
        Editor.Delete += (_, _) => DirtySinceBackup = true;
    }

    public bool IsModified => Editor.Modified || _forcedDirty;
    public bool IsReadOnly => Editor.ReadOnly;
    public bool IsUntitledAndEmpty => FilePath is null && !IsModified && Editor.TextLength == 0;
    public string DisplayTitle => DisplayName + (Editor.ReadOnly ? " 🔒" : "");
    public string ToolTip => FilePath ?? DisplayName;

    public void SetUntitledName(string name)
    {
        DisplayName = name;
        if (name.StartsWith("nuovo ") && int.TryParse(name[6..], out int n)) _untitledCounter = Math.Max(_untitledCounter, n);
        RaiseState();
    }

    /// <summary>Segna il buffer come "da salvare" senza toccare il testo (es. file eliminato dal disco).</summary>
    public void MarkDirty() { _forcedDirty = true; RaiseState(); }

    public void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

    protected override void WndProc(ref Message m)
    {
        const int WM_NOTIFY = 0x004E;
        if (m.Msg == WM_NOTIFY && m.LParam != IntPtr.Zero)
        {
            try
            {
                int code = System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam, IntPtr.Size * 2);
                if (code == 2009) // SCN_MACRORECORD
                {
                    int msgOffset = IntPtr.Size == 8 ? 56 : 28;
                    int msg = System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam, msgOffset);
                    UIntPtr w = (UIntPtr)(ulong)System.Runtime.InteropServices.Marshal.ReadInt64(m.LParam, msgOffset + IntPtr.Size);
                    IntPtr l = System.Runtime.InteropServices.Marshal.ReadIntPtr(m.LParam, msgOffset + IntPtr.Size * 2);
                    MacroManager.Instance.RecordStep(msg, w, l);
                }
            }
            catch { }
        }
        base.WndProc(ref m);
    }

    void ConfigureEditor()
    {
        var e = Editor;
        e.EolMode = Eol.CrLf;
        e.TabWidth = 4;
        e.UseTabs = true;
        e.IndentationGuides = IndentView.LookBoth;
        e.MultipleSelection = true;              // Ctrl+clic = cursori multipli
        e.AdditionalSelectionTyping = true;
        e.MouseSelectionRectangularSwitch = true; // Alt+trascina = selezione a colonna
        e.VirtualSpaceOptions = VirtualSpace.RectangularSelection;
        e.ScrollWidthTracking = true;
        e.ScrollWidth = 1;
        e.CaretLineVisibleAlways = true;
        e.CaretWidth = 2;

        // Alt+Su/Giù: sposta le righe selezionate
        e.AssignCmdKey(Keys.Alt | Keys.Up, Command.MoveSelectedLinesUp);
        e.AssignCmdKey(Keys.Alt | Keys.Down, Command.MoveSelectedLinesDown);

        // Margine 0: numeri di riga
        e.Margins[0].Type = MarginType.Number;
        e.Margins[0].Width = 40;

        // Margine 1: segnalibri
        var bm = e.Margins[BookmarkMargin];
        bm.Type = MarginType.Symbol;
        bm.Width = 14;
        bm.Sensitive = true;
        bm.Mask = 1u << BookmarkMarker;
        var marker = e.Markers[BookmarkMarker];
        marker.Symbol = MarkerSymbol.Bookmark;
        marker.SetBackColor(Color.FromArgb(0, 120, 215));
        marker.SetForeColor(Color.FromArgb(0, 80, 160));

        // Margine 2: piegatura del codice (+ / -)
        var fm = e.Margins[FoldMargin];
        fm.Type = MarginType.Symbol;
        fm.Mask = Marker.MaskFolders;
        fm.Sensitive = true;
        fm.Width = 14;
        e.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
        e.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
        e.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
        e.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
        e.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
        e.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
        e.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;
        e.AutomaticFold = AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change;

        // Margine 3: sottile linea verticale tra gutter e testo
        var sep = e.Margins[SeparatorMargin];
        sep.Type = MarginType.Color;
        sep.Width = 1;
        sep.Mask = 0;
        e.Margins.Left = 6; // respiro tra separatore e testo

        // Evidenziazione automatica delle occorrenze della parola selezionata
        var ind = e.Indicators[SmartIndicator];
        ind.Style = IndicatorStyle.RoundBox;
        ind.Under = true;
        ind.Alpha = 90;
        ind.OutlineAlpha = 160;

        e.MarginClick += (_, a) =>
        {
            if (a.Margin == BookmarkMargin) ToggleBookmark(e.LineFromPosition(a.Position));
        };
        e.TextChanged += (_, _) => { _lastSmart = null; UpdateLineNumberWidth(); };
        e.UpdateUI += (_, a) =>
        {
            if ((a.Change & UpdateChange.Selection) == 0) return;
            HighlightBraces();
            SmartHighlight();
        };
    }

    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        var e = Editor;
        e.Margins[SeparatorMargin].BackColor = theme.UiBorder;
        e.Indicators[SmartIndicator].ForeColor = theme.SmartHighlight;
        e.Indicators[FindPanel.MarkIndicator].ForeColor = theme.IsDark ? Color.FromArgb(255, 170, 0) : Color.FromArgb(255, 200, 0);
    }

    void UpdateLineNumberWidth()
    {
        if (Editor.Margins[0].Width == 0) return;
        int digits = Math.Max(3, Editor.Lines.Count.ToString().Length);
        int w = Editor.TextWidth(Style.LineNumber, new string('9', digits + 1));
        if (Editor.Margins[0].Width != w) Editor.Margins[0].Width = w;
    }

    public void ShowLineNumbers(bool show)
    {
        Editor.Margins[0].Width = show ? 1 : 0;
        if (show) UpdateLineNumberWidth();
    }

    void HighlightBraces()
    {
        int pos = Editor.CurrentPosition;
        int brace = -1;
        if (pos > 0 && IsBrace(Editor.GetCharAt(pos - 1))) brace = pos - 1;
        else if (IsBrace(Editor.GetCharAt(pos))) brace = pos;
        if (brace < 0) { Editor.BraceHighlight(Scintilla.InvalidPosition, Scintilla.InvalidPosition); return; }
        int match = Editor.BraceMatch(brace);
        if (match == Scintilla.InvalidPosition) Editor.BraceBadLight(brace);
        else Editor.BraceHighlight(brace, match);
    }

    static bool IsBrace(int c) => c is '(' or ')' or '[' or ']' or '{' or '}';

    bool _smartHighlight = true;
    string? _lastSmart;
    int _lastSmartPos = -1;

    public bool SmartHighlightEnabled
    {
        get => _smartHighlight;
        set
        {
            if (_smartHighlight == value) return;
            _smartHighlight = value;
            _lastSmart = null;
            _lastSmartPos = -1;
            SmartHighlight();
        }
    }

    /// <summary>Se la selezione è una parola intera, evidenzia tutte le sue occorrenze (come "Smart Highlighting").</summary>
    void SmartHighlight()
    {
        var e = Editor;
        if (!_smartHighlight)
        {
            if (_lastSmart != null)
            {
                e.IndicatorCurrent = SmartIndicator;
                e.IndicatorClearRange(0, e.TextLength);
                _lastSmart = null;
                _lastSmartPos = -1;
            }
            return;
        }

        string? word = null;
        int s = e.SelectionStart, en = e.SelectionEnd;
        if (en > s && en - s <= 100 && e.Selections.Count == 1 && e.TextLength < 2_000_000)
        {
            var text = e.SelectedText;
            if (text.Length > 0 && text.All(c => char.IsLetterOrDigit(c) || c == '_')
                && e.WordStartPosition(s, true) == s && e.WordEndPosition(en, true) == en)
                word = text;
        }

        // Fast path: se non c'era smart highlight e non c'è parola selezionata, esci subito a costo zero
        if (word == null && _lastSmart == null) return;
        if (word == _lastSmart && s == _lastSmartPos) return;

        bool hadSmart = _lastSmart != null;
        _lastSmart = word;
        _lastSmartPos = s;

        if (hadSmart || word != null)
        {
            e.IndicatorCurrent = SmartIndicator;
            e.IndicatorClearRange(0, e.TextLength);
        }

        if (word is null) return;

        e.SearchFlags = SearchFlags.MatchCase | SearchFlags.WholeWord;
        int start = 0, count = 0;
        while (count < 1000)
        {
            e.TargetStart = start;
            e.TargetEnd = e.TextLength;
            if (e.SearchInTarget(word) < 0) break;
            if (e.TargetStart != s) e.IndicatorFillRange(e.TargetStart, e.TargetEnd - e.TargetStart);
            start = e.TargetEnd;
            count++;
        }
    }

    public void SetLanguage(Language lang, Theme theme, string font, int size)
    {
        Language = lang;
        Languages.Apply(Editor, lang, theme, font, size);
        ApplyTheme(theme);
        UpdateLineNumberWidth();
        RaiseState();
    }

    // ---------- Segnalibri ----------
    public void ToggleBookmark(int line)
    {
        var l = Editor.Lines[line];
        if ((l.MarkerGet() & (1u << BookmarkMarker)) != 0) l.MarkerDelete(BookmarkMarker);
        else l.MarkerAdd(BookmarkMarker);
    }

    public void GotoBookmark(bool forward)
    {
        uint mask = 1u << BookmarkMarker;
        int cur = Editor.CurrentLine;
        int found = forward ? Editor.Lines[Math.Min(cur + 1, Editor.Lines.Count - 1)].MarkerNext(mask)
                            : (cur > 0 ? Editor.Lines[cur - 1].MarkerPrevious(mask) : -1);
        if (found < 0 || (forward && found <= cur))
            found = forward ? Editor.Lines[0].MarkerNext(mask) : Editor.Lines[Editor.Lines.Count - 1].MarkerPrevious(mask);
        if (found >= 0) GotoLine(found);
    }

    public void ClearBookmarks() => Editor.MarkerDeleteAll(BookmarkMarker);

    public void GotoLine(int line)
    {
        line = Math.Clamp(line, 0, Editor.Lines.Count - 1);
        Editor.Lines[line].EnsureVisible();
        Editor.Lines[line].Goto();
        Editor.FirstVisibleLine = Math.Max(0, Editor.Lines[line].DisplayIndex - Editor.LinesOnScreen / 3);
        Editor.Focus();
    }

    // ---------- I/O file ----------
    public new void Load(string path, Theme theme, string font, int size)
    {
        var bytes = File.ReadAllBytes(path);
        WasDetectedAsBinary = TextCodec.LooksBinary(bytes);
        (string text, FileEncoding enc) = TextCodec.Decode(bytes);
        Encoding = enc;
        HasMixedEol = TextCodec.HasMixedEol(text);
        FilePath = Path.GetFullPath(path);
        DisplayName = Path.GetFileName(path);
        LastWriteUtc = File.GetLastWriteTimeUtc(path);
        _forcedDirty = false;
        MissingAcknowledged = false;

        bool isLarge = bytes.Length > 2_000_000;
        Editor.ScrollWidthTracking = !isLarge;
        Editor.AutomaticFold = isLarge ? 0 : (AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change);

        Editor.ReadOnly = WasDetectedAsBinary;
        Editor.Text = text;
        Editor.EolMode = TextCodec.DetectEol(text);
        Editor.EmptyUndoBuffer();
        Editor.SetSavePoint();
        DirtySinceBackup = false;
        SetLanguage(isLarge ? Languages.Normal : Languages.FromPath(path), theme, font, size);
    }

    public void Reinterpret(FileEncoding enc)
    {
        if (FilePath is null || MissingOnDisk) return;
        var bytes = File.ReadAllBytes(FilePath);
        string text = enc switch
        {
            FileEncoding.Ansi => TextCodec.SystemAnsi.GetString(bytes),
            FileEncoding.Utf8 => new System.Text.UTF8Encoding(false).GetString(bytes),
            FileEncoding.Utf8Bom => new System.Text.UTF8Encoding(false).GetString(bytes.Length >= 3 ? bytes[3..] : bytes),
            FileEncoding.Utf16Le or FileEncoding.Utf16LeNoBom => System.Text.Encoding.Unicode.GetString(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? bytes[2..] : bytes),
            FileEncoding.Utf16Be or FileEncoding.Utf16BeNoBom => System.Text.Encoding.BigEndianUnicode.GetString(bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF ? bytes[2..] : bytes),
            _ => new System.Text.UTF8Encoding(false).GetString(bytes),
        };

        Editor.Text = text;
        Encoding = enc;
        HasMixedEol = TextCodec.HasMixedEol(text);
        _forcedDirty = false;
        DirtySinceBackup = false;
        Editor.EmptyUndoBuffer();
        Editor.SetSavePoint();
        RaiseState();
    }

    /// <summary>Ripristina il contenuto da un backup di sessione: il buffer risulta modificato rispetto al file.</summary>
    public void RestoreFromBackup(string? path, string backupText, FileEncoding enc, Theme theme, string font, int size)
    {
        if (path != null && File.Exists(path)) Load(path, theme, font, size);
        else if (path != null)
        {
            FilePath = Path.GetFullPath(path);
            DisplayName = Path.GetFileName(path);
            MissingAcknowledged = true;
            _forcedDirty = true;
            SetLanguage(Languages.FromPath(path), theme, font, size);
        }
        Encoding = enc;
        Editor.Text = backupText;
        Editor.EolMode = TextCodec.DetectEol(backupText);
        HasMixedEol = TextCodec.HasMixedEol(backupText);
        Editor.EmptyUndoBuffer(); // azzera anche il punto di salvataggio di Scintilla...
        DirtySinceBackup = false;
        MarkDirty();              // ...quindi il buffer ripristinato va segnato esplicitamente come non salvato
    }

    public void Reload(Theme theme, string font, int size)
    {
        if (FilePath is null) return;
        int pos = Editor.CurrentPosition, first = Editor.FirstVisibleLine;
        Load(FilePath, theme, font, size);
        Editor.GotoPosition(Math.Min(pos, Editor.TextLength));
        Editor.FirstVisibleLine = first;
    }

    public void SaveTo(string path, Theme theme, string font, int size)
    {
        var bytes = TextCodec.Encode(Editor.Text, Encoding);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string tmp = path + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(tmp, bytes);

        if (File.Exists(path))
        {
            try
            {
                File.Replace(tmp, path, null);
            }
            catch
            {
                File.Move(tmp, path, overwrite: true);
            }
        }
        else
        {
            File.Move(tmp, path);
        }

        bool langChange = FilePath is null || !string.Equals(Path.GetExtension(FilePath), Path.GetExtension(path), StringComparison.OrdinalIgnoreCase);
        FilePath = Path.GetFullPath(path);
        DisplayName = Path.GetFileName(path);
        LastWriteUtc = File.GetLastWriteTimeUtc(path);
        _forcedDirty = false;
        MissingAcknowledged = false;
        HasMixedEol = TextCodec.HasMixedEol(Editor.Text);
        Editor.SetSavePoint();
        if (langChange) SetLanguage(Languages.FromPath(path), theme, font, size);
        RaiseState();
    }

    public bool ChangedOnDisk => FilePath is not null && File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) != LastWriteUtc;
    public bool MissingOnDisk => FilePath is not null && !File.Exists(FilePath);
    public void AcknowledgeDiskState() { if (FilePath is not null && File.Exists(FilePath)) LastWriteUtc = File.GetLastWriteTimeUtc(FilePath); }

    public static (string, FileEncoding) Decode(byte[] b) => TextCodec.Decode(b);
    public static byte[] Encode(string text, FileEncoding enc) => TextCodec.Encode(text, enc);
    public static string EncodingLabel(FileEncoding e) => TextCodec.EncodingLabel(e);
}
