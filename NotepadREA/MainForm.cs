using System.Diagnostics;
using ScintillaNET;

namespace NotepadREA;

/// <summary>
/// Finestra principale. Struttura menu e scorciatoie ricavate dall'analisi di Notepad++ 8.9.8.1
/// (localization/english.xml, shortcuts.xml, risorse RT_MENU/RT_DIALOG) e reimplementate da zero.
/// </summary>
public sealed class MainForm : Form
{
    readonly Settings _settings = Settings.Load();
    readonly TabStrip _strip = new();
    readonly Panel _host = new() { Dock = DockStyle.Fill };
    readonly SplitContainer _mainSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, Panel2Collapsed = true };
    readonly TerminalPanel _terminal = new();
    readonly MiniMap _mini = new();
    readonly FindPanel _findPanel;
    readonly MenuStrip _menu = new() { RenderMode = ToolStripRenderMode.ManagerRenderMode, Padding = new Padding(4, 2, 0, 2) };
    readonly ToolStrip _toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.ManagerRenderMode, Padding = new Padding(6, 3, 6, 3) };
    readonly StatusStrip _status = new() { RenderMode = ToolStripRenderMode.ManagerRenderMode, SizingGrip = false };
    readonly ToolStripStatusLabel _stLang = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripStatusLabel _stLen = new();
    readonly ToolStripStatusLabel _stPos = new();
    readonly ToolStripStatusLabel _stEol = new();
    readonly ToolStripStatusLabel _stEnc = new();
    readonly ToolStripStatusLabel _stIns = new();
    readonly ToolStripMenuItem _recentMenu = new("File &recenti");
    readonly ToolStripMenuItem _langMenu = new("&Linguaggio");
    readonly ToolStripMenuItem _encMenu = new("Codi&fica");
    readonly ToolStripMenuItem _themeMenu = new("Tema");
    readonly Stack<string> _closedFiles = new();
    readonly System.Windows.Forms.Timer _snapshotTimer = new();
    readonly string[] _startupArgs;

    ToolStripMenuItem _miWrap = null!, _miWs = null!, _miEol = null!, _miGuides = null!, _miLineNo = null!, _miTop = null!,
        _miReadOnly = null!, _miMini = null!, _miSmart = null!, _miRestore = null!, _miSnapshot = null!;
    bool _checkingDisk, _closing;
    FormWindowState _preFullScreenState;

    Theme CurrentTheme => Theme.ByName(_settings.ThemeName);
    Document? Current => _strip.Selected;
    IEnumerable<Document> Docs => _strip.Documents;
    bool SnapshotActive => _settings.Snapshot && _settings.RestoreSession;

    public MainForm(string[] args)
    {
        _startupArgs = args;
        Text = "NotepadREA";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AllowDrop = true;
        KeyPreview = true;
        Size = new Size(1150, 760);
        StartPosition = FormStartPosition.CenterScreen;
        if (_settings.Bounds is { } b && SystemInformation.VirtualScreen.IntersectsWith(b)) { StartPosition = FormStartPosition.Manual; Bounds = b; }
        if (_settings.Maximized) WindowState = FormWindowState.Maximized;

        _findPanel = new FindPanel(() => Current?.Editor);

        BuildMenu();
        BuildToolbar();
        _status.Items.AddRange([_stLang, _stLen, _stPos, _stEol, _stEnc, _stIns]);
        foreach (ToolStripStatusLabel l in _status.Items) l.Padding = new Padding(8, 0, 8, 0);

        _mainSplit.Panel1.Controls.Add(_host);
        _mainSplit.Panel2.Controls.Add(_terminal);
        _terminal.CloseRequested += (_, _) => ToggleTerminal();

        // Ordine di aggiunta = ordine inverso di aggancio (l'ultimo aggiunto è il più esterno)
        Controls.Add(_mainSplit);
        Controls.Add(_mini);
        Controls.Add(_findPanel);
        Controls.Add(_strip);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_status);
        MainMenuStrip = _menu;

        _strip.SelectedChanged += (_, _) => OnActiveDocChanged();
        _strip.CloseRequested += (_, d) => CloseDocument(d);
        _strip.ContextRequested += (_, a) => ShowTabMenu(a.Doc, a.Location);
        _strip.NewRequested += (_, _) => NewDocument();
        _strip.AllowDrop = true;
        foreach (var c in new Control[] { this, _strip, _host })
        {
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
        }

        _snapshotTimer.Tick += (_, _) => { SnapshotNow(); if (ActiveForm == this) CheckDiskChanges(); };
        Activated += (_, _) => CheckDiskChanges();
        FormClosing += OnFormClosing;
        Load += (_, _) => RestoreAndOpenArgs();
        HandleCreated += (_, _) => CurrentTheme.ApplyTitleBar(this);
        DpiChanged += (_, _) => RefreshToolbarIcons();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F12 || (e.Control && (e.KeyCode == Keys.Oemtilde || e.KeyCode == Keys.Oem5 || (int)e.KeyCode == 192)))
            {
                ToggleTerminal();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
    }

    // =====================================================================
    // Menu
    // =====================================================================
    static ToolStripMenuItem Item(string text, Action onClick, Keys keys = Keys.None, string? keyText = null)
    {
        var mi = new ToolStripMenuItem(text, null, (_, _) => onClick());
        if (keys != Keys.None) mi.ShortcutKeys = keys;
        if (keyText != null) mi.ShortcutKeyDisplayString = keyText;
        return mi;
    }

    static ToolStripMenuItem Sub(string text, params ToolStripItem[] items)
    {
        var m = new ToolStripMenuItem(text);
        m.DropDownItems.AddRange(items);
        return m;
    }

    static ToolStripSeparator Sep() => new();

    void Toggle(Action flip) { flip(); ApplyViewSettings(); }

    void BuildMenu()
    {
        _recentMenu.DropDownOpening += (_, _) => RebuildRecentMenu();
        RebuildRecentMenu();

        var file = Sub("&File",
            Item("&Nuovo", NewDocument, Keys.Control | Keys.N),
            Item("&Apri...", OpenDialog, Keys.Control | Keys.O),
            Item("Apertura rapida...", QuickOpen, Keys.Control | Keys.P),
            Item("Apri cartella contenitore in Esplora risorse", () => RevealInExplorer(Current)),
            Item("Ricarica dal disco", ReloadCurrent, Keys.Control | Keys.R),
            Sep(),
            Item("&Salva", () => Save(Current), Keys.Control | Keys.S),
            Item("Salva &con nome...", () => SaveAs(Current), Keys.Control | Keys.Alt | Keys.S),
            Item("Salva una copia con nome...", SaveCopyAs),
            Item("Salva t&utto", SaveAll, Keys.Control | Keys.Shift | Keys.S),
            Item("Rinomina...", () => Rename(Current)),
            Sep(),
            Item("Stampa...", () => { if (Current != null) PrintHelper.Print(Current, this, false); }, Keys.Control | Keys.Alt | Keys.P),
            Item("Anteprima di stampa...", () => { if (Current != null) PrintHelper.Print(Current, this, true); }),
            Sep(),
            Item("C&hiudi", () => CloseDocument(Current), Keys.Control | Keys.W),
            Item("Chiudi tutto", () => CloseMany(Docs.ToList()), Keys.Control | Keys.Shift | Keys.W),
            Item("Chiudi tutto tranne il documento corrente", () => CloseMany(Docs.Where(d => d != Current).ToList())),
            Item("Chiudi tutti i documenti non modificati", () => CloseMany(Docs.Where(d => !d.IsModified).ToList())),
            Item("Ripristina ultimo file chiuso", RestoreClosed, Keys.Control | Keys.Shift | Keys.T),
            Sep(),
            _recentMenu,
            Sep(),
            Item("&Esci", Close, Keys.Alt | Keys.F4));

        var lineOps = Sub("Operazioni sulle righe",
            Item("Duplica riga corrente", () => Exec(Command.LineDuplicate), Keys.Control | Keys.D),
            Item("Elimina riga corrente", () => Exec(Command.LineDelete), Keys.Control | Keys.Shift | Keys.L),
            Item("Sposta su riga corrente", () => Exec(Command.MoveSelectedLinesUp), Keys.None, "Alt+Su"),
            Item("Sposta giù riga corrente", () => Exec(Command.MoveSelectedLinesDown), Keys.None, "Alt+Giù"),
            Item("Unisci righe", JoinLines, Keys.Control | Keys.J),
            Sep(),
            Item("Rimuovi righe duplicate", () => TransformLines(l => l.Distinct().ToList())),
            Item("Rimuovi righe duplicate consecutive", () => TransformLines(RemoveConsecutiveDuplicates)),
            Item("Rimuovi righe vuote", () => TransformLines(l => l.Where(x => x.Trim().Length > 0).ToList())),
            Sep(),
            Item("Ordina righe (A→Z)", () => TransformLines(l => l.OrderBy(x => x, StringComparer.Ordinal).ToList())),
            Item("Ordina righe (Z→A)", () => TransformLines(l => l.OrderByDescending(x => x, StringComparer.Ordinal).ToList())),
            Item("Ordina righe ignorando maiuscole (A→Z)", () => TransformLines(l => l.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList())),
            Item("Ordina righe come interi (crescente)", () => TransformLines(l => l.OrderBy(x => long.TryParse(x.Trim(), out var n) ? n : long.MaxValue).ToList())),
            Item("Ordina righe per lunghezza (crescente)", () => TransformLines(l => l.OrderBy(x => x.Length).ToList())),
            Item("Inverti ordine righe", () => TransformLines(l => { l.Reverse(); return l; })),
            Item("Ordine casuale", () => TransformLines(l => l.OrderBy(_ => Random.Shared.Next()).ToList())));

        var caseOps = Sub("Converti maiuscole/minuscole",
            Item("MAIUSCOLO", () => Exec(Command.Uppercase), Keys.Control | Keys.Shift | Keys.U),
            Item("minuscolo", () => Exec(Command.Lowercase), Keys.Control | Keys.U),
            Item("Iniziali Maiuscole", () => TransformSelection(s => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower()))),
            Item("Frase con maiuscola iniziale", () => TransformSelection(SentenceCase)),
            Item("iNVERTI mAIUSCOLE", () => TransformSelection(s => new string(s.Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).ToArray()))));

        var blankOps = Sub("Operazioni sugli spazi",
            Item("Elimina spazi finali", () => TransformLines(l => l.Select(x => x.TrimEnd()).ToList())),
            Item("Elimina spazi iniziali", () => TransformLines(l => l.Select(x => x.TrimStart()).ToList())),
            Item("Elimina spazi iniziali e finali", () => TransformLines(l => l.Select(x => x.Trim()).ToList())),
            Item("Tab → spazi", () => TransformSelection(s => s.Replace("\t", new string(' ', Current!.Editor.TabWidth)))),
            Item("Spazi → tab (iniziali)", () => TransformLines(l => l.Select(LeadingSpacesToTabs).ToList())));

        var comment = Sub("Commenta/Decommenta",
            Item("Commento di riga (alterna)", ToggleLineComment, Keys.Control | Keys.Q),
            Item("Commento di blocco", BlockComment, Keys.Control | Keys.Shift | Keys.Q));

        var edit = Sub("&Modifica",
            Item("Annulla", () => Current?.Editor.Undo(), Keys.Control | Keys.Z),
            Item("Ripeti", () => Current?.Editor.Redo(), Keys.Control | Keys.Y),
            Sep(),
            Item("Taglia", () => Current?.Editor.Cut(), Keys.Control | Keys.X),
            Item("Copia", () => Current?.Editor.Copy(), Keys.Control | Keys.C),
            Item("Incolla", () => Current?.Editor.Paste(), Keys.Control | Keys.V),
            Item("Elimina", () => Current?.Editor.Clear(), Keys.None, "Canc"),
            Item("Seleziona tutto", () => Current?.Editor.SelectAll(), Keys.Control | Keys.A),
            Sep(),
            Sub("Copia negli appunti",
                Item("Copia percorso completo", () => CopyToClipboard(Current?.FilePath)),
                Item("Copia nome file", () => CopyToClipboard(Current?.FilePath is { } p ? Path.GetFileName(p) : Current?.DisplayName)),
                Item("Copia cartella", () => CopyToClipboard(Current?.FilePath is { } p ? Path.GetDirectoryName(p) : null))),
            Sub("Indentazione",
                Item("Aumenta rientro", () => Exec(Command.Tab), Keys.None, "Tab"),
                Item("Riduci rientro", () => Exec(Command.BackTab), Keys.None, "Maiusc+Tab")),
            caseOps, lineOps, comment, blankOps,
            Sub("Selezione multipla",
                Disabled(Item("Aggiungi cursore: Ctrl+clic", () => { })),
                Disabled(Item("Selezione a colonna: Alt+trascina / Alt+Maiusc+frecce", () => { })),
                Item("Aggiungi occorrenza successiva", AddNextOccurrence, Keys.Control | Keys.Shift | Keys.D)),
            Sep(),
            Sub("Inserisci data e ora",
                Item("Breve", () => InsertText(DateTime.Now.ToString("t") + " " + DateTime.Now.ToString("d"))),
                Item("Estesa", () => InsertText(DateTime.Now.ToString("F")))),
            Sep(),
            _miReadOnly = Item("Sola lettura", () => ToggleReadOnly(Current)));

        var search = Sub("&Cerca",
            Item("&Trova...", () => _findPanel.Open(false), Keys.Control | Keys.F),
            Item("Trova successivo", () => _findPanel.FindNext(true), Keys.F3),
            Item("Trova precedente", () => _findPanel.FindNext(false), Keys.Shift | Keys.F3),
            Item("Trova (volatile) successivo", () => QuickFind(true), Keys.Control | Keys.F3),
            Item("Trova (volatile) precedente", () => QuickFind(false), Keys.Control | Keys.Shift | Keys.F3),
            Item("&Sostituisci...", () => _findPanel.Open(true), Keys.Control | Keys.H),
            Item("Trova nei file...", OpenFindInFiles, Keys.Control | Keys.Shift | Keys.F),
            Item("Cancella evidenziazioni", _findPanel.ClearMarks),
            Sep(),
            Item("&Vai alla riga...", GotoLineDialog, Keys.Control | Keys.G),
            Item("Apertura rapida...", QuickOpen, Keys.None, "Ctrl+P"),
            Item("Vai alla parentesi corrispondente", GotoMatchingBrace, Keys.Control | Keys.B),
            Sep(),
            Sub("Segnalibri",
                Item("Attiva/disattiva segnalibro", () => Current?.ToggleBookmark(Current.Editor.CurrentLine), Keys.Control | Keys.F2),
                Item("Segnalibro successivo", () => Current?.GotoBookmark(true), Keys.F2),
                Item("Segnalibro precedente", () => Current?.GotoBookmark(false), Keys.Shift | Keys.F2),
                Item("Cancella tutti i segnalibri", () => Current?.ClearBookmarks())));

        foreach (var t in Theme.All)
        {
            var th = t;
            _themeMenu.DropDownItems.Add(new ToolStripMenuItem(th.Name, null, (_, _) => { _settings.ThemeName = th.Name; ApplyTheme(); }) { Tag = th });
        }
        _themeMenu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripMenuItem mi in _themeMenu.DropDownItems) mi.Checked = Equals(mi.Tag, CurrentTheme);
        };

        var view = Sub("&Visualizza",
            _miTop = Item("Sempre in primo piano", () => { TopMost = !TopMost; _miTop.Checked = TopMost; }),
            Item("Schermo intero", ToggleFullScreen, Keys.F11),
            Sep(),
            _themeMenu,
            Item("Tema successivo", CycleTheme, Keys.Control | Keys.Shift | Keys.K),
            Item("Carattere...", ChooseFont),
            Sep(),
            _miWrap = Item("A capo automatico", () => Toggle(() => _settings.WordWrap = !_settings.WordWrap), Keys.Alt | Keys.Z),
            Sub("Mostra simboli",
                _miWs = Item("Mostra spazi e tab", () => Toggle(() => _settings.ShowWhitespace = !_settings.ShowWhitespace)),
                _miEol = Item("Mostra fine riga", () => Toggle(() => _settings.ShowEol = !_settings.ShowEol)),
                _miGuides = Item("Mostra guide di rientro", () => Toggle(() => _settings.ShowIndentGuides = !_settings.ShowIndentGuides))),
            _miLineNo = Item("Numeri di riga", () => Toggle(() => _settings.ShowLineNumbers = !_settings.ShowLineNumbers)),
            _miMini = Item("Minimappa", () => Toggle(() => _settings.ShowMinimap = !_settings.ShowMinimap), Keys.Control | Keys.Shift | Keys.M),
            _miSmart = Item("Evidenzia occorrenze della parola selezionata", () => Toggle(() => _settings.SmartHighlight = !_settings.SmartHighlight)),
            Sub("Zoom",
                Item("Aumenta", () => SetZoom(_settings.Zoom + 1), Keys.Control | Keys.Add, "Ctrl+Num +"),
                Item("Riduci", () => SetZoom(_settings.Zoom - 1), Keys.Control | Keys.Subtract, "Ctrl+Num -"),
                Item("Ripristina", () => SetZoom(0), Keys.Control | Keys.Divide, "Ctrl+Num /")),
            Sep(),
            Item("Comprimi tutti i livelli", () => Current?.Editor.FoldAll(FoldAction.Contract), Keys.Alt | Keys.D0),
            Item("Espandi tutti i livelli", () => Current?.Editor.FoldAll(FoldAction.Expand), Keys.Alt | Keys.Shift | Keys.D0),
            Sep(),
            Item("Terminale incorporato", ToggleTerminal, Keys.F12, "F12 / Ctrl+`"),
            Sep(),
            Item("Scheda successiva", () => CycleTab(1), Keys.Control | Keys.Tab),
            Item("Scheda precedente", () => CycleTab(-1), Keys.Control | Keys.Shift | Keys.Tab));

        BuildEncodingMenu();
        BuildLanguageMenu();

        _miRestore = Item("Ripristina sessione all'avvio", () => { _settings.RestoreSession = !_settings.RestoreSession; UpdateSnapshotTimer(); });
        _miSnapshot = Item("Salvataggio automatico dei file non salvati (recupero sessione)", () => { _settings.Snapshot = !_settings.Snapshot; UpdateSnapshotTimer(); });
        var settings = Sub("Impos&tazioni",
            _miRestore, _miSnapshot,
            Sep(),
            Item("Apri cartella impostazioni e backup", () => { Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", Settings.Dir); }));
        settings.DropDownOpening += (_, _) =>
        {
            _miRestore.Checked = _settings.RestoreSession;
            _miSnapshot.Checked = _settings.Snapshot;
            _miSnapshot.Enabled = _settings.RestoreSession;
        };

        var macro = Sub("&Macro",
            Item("Avvia registrazione", () => MacroManager.Instance.StartRecording(Current), Keys.Control | Keys.Shift | Keys.R),
            Item("Ferma registrazione", () => MacroManager.Instance.StopRecording(Current)),
            Item("Riproduci macro", () => MacroManager.Instance.Play(Current, 1), Keys.Control | Keys.Shift | Keys.P),
            Item("Esegui macro N volte...", RunMacroMultipleTimes));

        var tools = Sub("Strume&nti",
            Item("Confronta file (Diff Side-by-Side)...", OpenDiffDialog, Keys.Control | Keys.Alt | Keys.D),
            Sep(),
            Sub("Formattazione e Minificazione",
                Item("Formatta JSON", () => TransformSelectionOrAll(s => TextTransformTools.FormatJson(s, false)), Keys.Control | Keys.Shift | Keys.J),
                Item("Minifica JSON", () => TransformSelectionOrAll(s => TextTransformTools.FormatJson(s, true))),
                Item("Formatta XML", () => TransformSelectionOrAll(s => TextTransformTools.FormatXml(s, false))),
                Item("Minifica XML", () => TransformSelectionOrAll(s => TextTransformTools.FormatXml(s, true)))),
            Sub("Codifica / Decodifica",
                Item("Base64 Encode", () => TransformSelectionOrAll(TextTransformTools.Base64Encode), Keys.Control | Keys.Shift | Keys.B),
                Item("Base64 Decode", () => TransformSelectionOrAll(TextTransformTools.Base64Decode)),
                Item("URL Encode", () => TransformSelectionOrAll(TextTransformTools.UrlEncode)),
                Item("URL Decode", () => TransformSelectionOrAll(TextTransformTools.UrlDecode))),
            Sub("Manipolazione Righe",
                Item("Ordina righe (A -> Z)", () => TransformSelectionOrAll(s => TextTransformTools.SortLines(s, false))),
                Item("Ordina righe (Z -> A)", () => TransformSelectionOrAll(s => TextTransformTools.SortLines(s, true))),
                Item("Rimuovi righe duplicate", () => TransformSelectionOrAll(TextTransformTools.RemoveDuplicateLines)),
                Item("Rimuovi spazi a fine riga (Trim)", () => TransformSelectionOrAll(TextTransformTools.TrimTrailingSpaces))),
            Sub("Maiuscole / Minuscole",
                Item("Iniziali Maiuscole (Title Case)", () => TransformSelectionOrAll(TextTransformTools.ToTitleCase))),
            Sep(),
            Item("Ispezione Esadecimale (Hex View)...", OpenHexView, Keys.Control | Keys.Alt | Keys.H),
            Item("Hash MD5 / SHA-256 della selezione o del documento", ShowHashes),
            Item("Statistiche documento", ShowSummary));

        var window = Sub("Fi&nestra");
        window.DropDownOpening += (_, _) =>
        {
            window.DropDownItems.Clear();
            int i = 0;
            foreach (var d in Docs)
            {
                var doc = d;
                window.DropDownItems.Add(new ToolStripMenuItem($"&{++i} {(d.IsModified ? "● " : "")}{d.DisplayName}", null, (_, _) => _strip.Select(doc)) { Checked = d == Current });
            }
        };

        var help = Sub("&?",
            Item("Informazioni su NotepadREA...", () => MessageBox.Show(this,
                "NotepadREA 2.0\n\nEditor di testo ricostruito da zero partendo dall'analisi statica (REA) di Notepad++ 8.9.8.1 portable.\n" +
                "Motore di editing: Scintilla + Lexilla (gli stessi componenti identificati nel binario originale).\n\n" +
                "Non contiene codice di Notepad++.", "Informazioni", MessageBoxButtons.OK, MessageBoxIcon.Information), Keys.F1));

        _menu.Items.AddRange([file, edit, search, view, _encMenu, _langMenu, settings, macro, tools, window, help]);
        edit.DropDownOpening += (_, _) => _miReadOnly.Checked = Current?.IsReadOnly == true;
    }

    static ToolStripMenuItem Disabled(ToolStripMenuItem mi) { mi.Enabled = false; return mi; }

    void BuildToolbar()
    {
        ToolStripButton B(char glyph, string tip, Action a) => new(null, null, (_, _) => a())
        {
            ToolTipText = tip, Tag = glyph, DisplayStyle = ToolStripItemDisplayStyle.Image, Margin = new Padding(1), Padding = new Padding(3),
        };
        _toolbar.Items.AddRange([
            B(Icons.New, "Nuovo (Ctrl+N)", NewDocument),
            B(Icons.Open, "Apri (Ctrl+O)", OpenDialog),
            B(Icons.QuickOpen, "Apertura rapida (Ctrl+P)", QuickOpen),
            B(Icons.Save, "Salva (Ctrl+S)", () => Save(Current)),
            B(Icons.SaveAll, "Salva tutto (Ctrl+Maiusc+S)", SaveAll),
            B(Icons.Close, "Chiudi (Ctrl+W)", () => CloseDocument(Current)),
            new ToolStripSeparator(),
            B(Icons.Cut, "Taglia (Ctrl+X)", () => Current?.Editor.Cut()),
            B(Icons.Copy, "Copia (Ctrl+C)", () => Current?.Editor.Copy()),
            B(Icons.Paste, "Incolla (Ctrl+V)", () => Current?.Editor.Paste()),
            new ToolStripSeparator(),
            B(Icons.Undo, "Annulla (Ctrl+Z)", () => Current?.Editor.Undo()),
            B(Icons.Redo, "Ripeti (Ctrl+Y)", () => Current?.Editor.Redo()),
            new ToolStripSeparator(),
            B(Icons.Find, "Trova (Ctrl+F)", () => _findPanel.Open(false)),
            B(Icons.Replace, "Sostituisci (Ctrl+H)", () => _findPanel.Open(true)),
            new ToolStripSeparator(),
            B(Icons.ZoomIn, "Zoom avanti (Ctrl+Num +)", () => SetZoom(_settings.Zoom + 1)),
            B(Icons.ZoomOut, "Zoom indietro (Ctrl+Num -)", () => SetZoom(_settings.Zoom - 1)),
            new ToolStripSeparator(),
            B(Icons.Wrap, "A capo automatico (Alt+Z)", () => Toggle(() => _settings.WordWrap = !_settings.WordWrap)),
            B(Icons.Symbols, "Mostra tutti i caratteri", () => Toggle(() => { bool v = !(_settings.ShowWhitespace && _settings.ShowEol); _settings.ShowWhitespace = _settings.ShowEol = v; })),
            B(Icons.Minimap, "Minimappa (Ctrl+Maiusc+M)", () => Toggle(() => _settings.ShowMinimap = !_settings.ShowMinimap)),
            B(Icons.Theme, "Tema successivo (Ctrl+Maiusc+K)", CycleTheme),
        ]);
    }

    void RefreshToolbarIcons()
    {
        int px = (int)Math.Round(18 * DeviceDpi / 96.0);
        _toolbar.ImageScalingSize = new Size(px, px);
        var color = CurrentTheme.UiFore;
        foreach (var b in _toolbar.Items.OfType<ToolStripButton>())
        {
            var old = b.Image;
            b.Image = Icons.Render((char)b.Tag!, color, px);
            old?.Dispose();
        }
        // stato "attivo" dei pulsanti interruttore
        foreach (var b in _toolbar.Items.OfType<ToolStripButton>())
            b.Checked = (char)b.Tag! switch
            {
                Icons.Wrap => _settings.WordWrap,
                Icons.Symbols => _settings.ShowWhitespace && _settings.ShowEol,
                Icons.Minimap => _settings.ShowMinimap,
                _ => false,
            };
    }

    void BuildEncodingMenu()
    {
        foreach (var enc in Enum.GetValues<FileEncoding>())
        {
            var e = enc;
            _encMenu.DropDownItems.Add(new ToolStripMenuItem("Converti in " + Document.EncodingLabel(e), null, (_, _) => SetEncoding(e)) { Tag = e });
        }
        _encMenu.DropDownItems.Add(Sep());
        _encMenu.DropDownItems.Add(Item("Reinterpreta file come ANSI (ricarica)", () => ReinterpretAs(FileEncoding.Ansi)));
        _encMenu.DropDownItems.Add(Item("Reinterpreta file come UTF-8 (ricarica)", () => ReinterpretAs(FileEncoding.Utf8)));
        _encMenu.DropDownItems.Add(Sep());
        _encMenu.DropDownItems.Add(Sub("Converti fine riga",
            Item("Windows (CR LF)", () => SetEol(Eol.CrLf)),
            Item("Unix (LF)", () => SetEol(Eol.Lf)),
            Item("Macintosh (CR)", () => SetEol(Eol.Cr))));
        _encMenu.DropDownOpening += (_, _) =>
        {
            foreach (var mi in _encMenu.DropDownItems.OfType<ToolStripMenuItem>())
                if (mi.Tag is FileEncoding fe) mi.Checked = Current?.Encoding == fe;
        };
    }

    void BuildLanguageMenu()
    {
        foreach (var lang in Languages.All)
        {
            var l = lang;
            _langMenu.DropDownItems.Add(new ToolStripMenuItem(l.Name, null, (_, _) => SetLanguage(Current, l)) { Tag = l });
        }
        _langMenu.DropDownOpening += (_, _) =>
        {
            foreach (var mi in _langMenu.DropDownItems.OfType<ToolStripMenuItem>()) mi.Checked = mi.Tag == Current?.Language;
        };
    }

    void SetLanguage(Document? d, Language l)
    {
        if (d is null) return;
        d.SetLanguage(l, CurrentTheme, _settings.FontName, _settings.FontSize);
        if (d == Current) AttachMinimap();
    }

    void RebuildRecentMenu()
    {
        _recentMenu.DropDownItems.Clear();
        int i = 0;
        foreach (var path in _settings.RecentFiles)
        {
            var p = path;
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem($"&{(++i) % 10} {p}", null, (_, _) => OpenFile(p)));
        }
        if (i == 0) _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(vuoto)") { Enabled = false });
        else
        {
            _recentMenu.DropDownItems.Add(Sep());
            _recentMenu.DropDownItems.Add(Item("Svuota elenco", () => _settings.RecentFiles.Clear()));
        }
    }

    /// <summary>Chiamato quando l'utente apre file mentre l'editor è già in esecuzione.</summary>
    public void OpenFromOtherInstance(string[] files)
    {
        if (WindowState == FormWindowState.Minimized) WindowState = _settings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
        foreach (var f in files) OpenFile(f);
        Activate();
        CheckDiskChanges();
        BringToFront();
    }

    // =====================================================================
    // Documenti
    // =====================================================================
    Document AddDocument()
    {
        var doc = new Document { Visible = false };
        doc.SetLanguage(Languages.Normal, CurrentTheme, _settings.FontName, _settings.FontSize);
        ApplyViewSettings(doc);
        doc.Editor.UpdateUI += (_, _) => { if (doc == Current) UpdateStatus(); };
        doc.Editor.AllowDrop = true;
        doc.Editor.DragEnter += OnDragEnter;
        doc.Editor.DragDrop += OnDragDrop;
        doc.Editor.ZoomChanged += (_, _) => { if (doc == Current && doc.Editor.Zoom != _settings.Zoom) SetZoom(doc.Editor.Zoom); };
        doc.StateChanged += (_, _) => { if (doc == Current) { UpdateStatus(); UpdateTitle(); } };
        _host.Controls.Add(doc);
        _strip.Add(doc);
        _strip.Select(doc);
        return doc;
    }

    void NewDocument() { AddDocument(); Current?.Editor.Focus(); }

    void OpenDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Tutti i file (*.*)|*.*|File di testo (*.txt;*.log)|*.txt;*.log|Codice sorgente|*.c;*.cpp;*.h;*.cs;*.java;*.js;*.ts;*.py;*.ps1;*.bat;*.sql|Web|*.html;*.htm;*.css;*.xml;*.json;*.md",
            InitialDirectory = Current?.FilePath is { } p ? Path.GetDirectoryName(p) : null,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            foreach (var f in dlg.FileNames) OpenFile(f);
    }

    public Document? OpenFile(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = Docs.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { _strip.Select(existing); return existing; }
            if (!File.Exists(path))
            {
                if (MessageBox.Show(this, $"\"{path}\" non esiste. Crearlo?", "NotepadREA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
                File.WriteAllBytes(path, []);
            }
            var info = new FileInfo(path);
            if (info.Length > 200L * 1024 * 1024 &&
                MessageBox.Show(this, $"Il file è grande ({info.Length / 1024 / 1024} MB). Aprirlo comunque?", "NotepadREA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return null;

            // Riusa la scheda "nuovo 1" vuota, come fa Notepad++
            var doc = Current is { IsUntitledAndEmpty: true } && _strip.Documents.Count == 1 ? Current : AddDocument();
            doc.Load(path, CurrentTheme, _settings.FontName, _settings.FontSize);
            ApplyViewSettings(doc);
            _strip.Select(doc);
            if (doc.WasDetectedAsBinary)
            {
                MessageBox.Show(this, $"\"{Path.GetFileName(path)}\" contiene caratteri null o sequenze binarie.\nÈ stato aperto in modalità Sola Lettura per proteggere l'integrità del file.",
                    "File binario", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            _settings.AddRecent(path);
            OnActiveDocChanged();
            return doc;
        }
        catch (Exception ex)
        {
            Log.Error($"Impossibile aprire {path}", ex);
            MessageBox.Show(this, $"Impossibile aprire \"{path}\":\n{ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }

    bool Save(Document? doc)
    {
        if (doc is null) return false;
        if (doc.FilePath is null) return SaveAs(doc);
        try
        {
            doc.SaveTo(doc.FilePath, CurrentTheme, _settings.FontName, _settings.FontSize);
            DeleteBackup(doc);
            return true;
        }
        catch (UnauthorizedAccessException uEx)
        {
            Log.Warn($"Permessi insufficienti per {doc.FilePath}", uEx);
            if (MessageBox.Show(this,
                    $"Permessi insufficienti per salvare \"{doc.FilePath}\".\nVuoi riprovare eseguendo il salvataggio con privilegi di amministratore?",
                    "Accesso negato - Salva come Amministratore", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (SaveElevated(doc))
                {
                    DeleteBackup(doc);
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Impossibile salvare {doc.FilePath}", ex);
            MessageBox.Show(this, $"Impossibile salvare \"{doc.FilePath}\":\n{ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    bool SaveElevated(Document doc)
    {
        try
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"NotepadREA_elev_{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(tmp, TextCodec.Encode(doc.Editor.Text, doc.Encoding));
            var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, $"--elevated-save \"{tmp}\" \"{doc.FilePath}\"")
            {
                Verb = "runas",
                UseShellExecute = true,
            };
            var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0)
            {
                doc.AcknowledgeDiskState();
                doc.Editor.SetSavePoint();
                doc.RaiseState();
                return true;
            }
        }
        catch (System.ComponentModel.Win32Exception wEx) when (wEx.NativeErrorCode == 1223)
        {
            // Richiesta UAC annullata dall'utente
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("Errore salvataggio elevato", ex);
            MessageBox.Show(this, $"Errore durante il salvataggio con privilegi elevati:\n{ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return false;
    }

    bool SaveAs(Document? doc)
    {
        if (doc is null) return false;
        using var dlg = new SaveFileDialog
        {
            FileName = doc.FilePath is { } p ? Path.GetFileName(p) : doc.DisplayName + ".txt",
            InitialDirectory = doc.FilePath is { } q ? Path.GetDirectoryName(q) : null,
            Filter = "Tutti i file (*.*)|*.*|File di testo (*.txt)|*.txt",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            doc.SaveTo(dlg.FileName, CurrentTheme, _settings.FontName, _settings.FontSize);
            DeleteBackup(doc);
            ApplyViewSettings(doc);
            _settings.AddRecent(doc.FilePath!);
            if (doc == Current) AttachMinimap();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Impossibile salvare:\n{ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    void SaveCopyAs()
    {
        if (Current is not { } doc) return;
        using var dlg = new SaveFileDialog { FileName = doc.DisplayName, Filter = "Tutti i file (*.*)|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(dlg.FileName, Document.Encode(doc.Editor.Text, doc.Encoding));
    }

    void SaveAll()
    {
        foreach (var d in Docs.Where(d => d.IsModified).ToList())
        {
            _strip.Select(d);
            if (!Save(d)) return;
        }
    }

    void Rename(Document? doc)
    {
        if (doc is null) return;
        if (doc.FilePath is null) { SaveAs(doc); return; }
        var newName = Prompt("Rinomina", "Nuovo nome file:", Path.GetFileName(doc.FilePath));
        if (string.IsNullOrWhiteSpace(newName) || newName == Path.GetFileName(doc.FilePath)) return;
        var target = Path.Combine(Path.GetDirectoryName(doc.FilePath)!, newName);
        try
        {
            File.Move(doc.FilePath, target);
            doc.SaveTo(target, CurrentTheme, _settings.FontName, _settings.FontSize);
            ApplyViewSettings(doc);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ReloadCurrent()
    {
        if (Current is not { FilePath: not null } doc || doc.MissingOnDisk) return;
        if (doc.IsModified && MessageBox.Show(this, "Ricaricare il file perdendo le modifiche non salvate?", "Ricarica", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        doc.Reload(CurrentTheme, _settings.FontName, _settings.FontSize);
        ApplyViewSettings(doc);
        DeleteBackup(doc);
    }

    /// <returns>false se l'utente annulla.</returns>
    bool ConfirmClose(Document doc)
    {
        if (!doc.IsModified) return true;
        _strip.Select(doc);
        var r = MessageBox.Show(this, $"Salvare le modifiche a \"{doc.FilePath ?? doc.DisplayName}\"?", "Salva", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return r switch { DialogResult.Yes => Save(doc), DialogResult.No => true, _ => false };
    }

    bool CloseDocument(Document? doc, bool ask = true)
    {
        if (doc is null) return true;
        if (ask && !ConfirmClose(doc)) return false;
        if (doc.FilePath != null) _closedFiles.Push(doc.FilePath);
        DeleteBackup(doc);
        _strip.Remove(doc);
        _host.Controls.Remove(doc);
        doc.Dispose();
        if (_strip.Documents.Count == 0 && !_closing) AddDocument();
        return true;
    }

    void CloseMany(List<Document> docs)
    {
        foreach (var d in docs) if (!CloseDocument(d)) return;
    }

    void RestoreClosed()
    {
        while (_closedFiles.Count > 0)
        {
            var p = _closedFiles.Pop();
            if (File.Exists(p)) { OpenFile(p); return; }
        }
    }

    void ShowTabMenu(Document doc, Point location)
    {
        var cm = new ContextMenuStrip { RenderMode = ToolStripRenderMode.ManagerRenderMode };
        cm.Items.AddRange([
            Item("Chiudi", () => CloseDocument(doc)),
            Item("Chiudi le altre", () => CloseMany(Docs.Where(d => d != doc).ToList())),
            Item("Chiudi tutte a sinistra", () => CloseMany(Docs.TakeWhile(d => d != doc).ToList())),
            Item("Chiudi tutte a destra", () => CloseMany(Docs.SkipWhile(d => d != doc).Skip(1).ToList())),
            Item("Chiudi le non modificate", () => CloseMany(Docs.Where(d => !d.IsModified).ToList())),
            Sep(),
            Item("Salva", () => Save(doc)),
            Item("Salva con nome...", () => SaveAs(doc)),
            Item("Rinomina...", () => Rename(doc)),
            Item("Ricarica dal disco", ReloadCurrent),
            Sep(),
            Item("Copia percorso completo", () => CopyToClipboard(doc.FilePath)),
            Item("Copia nome file", () => CopyToClipboard(doc.FilePath is { } p ? Path.GetFileName(p) : doc.DisplayName)),
            Item("Apri cartella contenitore", () => RevealInExplorer(doc)),
            Sep(),
            Item(doc.IsReadOnly ? "Rimuovi sola lettura" : "Sola lettura", () => ToggleReadOnly(doc)),
        ]);
        cm.Closed += (_, _) => BeginInvoke(cm.Dispose);
        cm.Show(_strip, location);
    }

    static void RevealInExplorer(Document? d)
    {
        if (d?.FilePath is { } p && File.Exists(p)) Process.Start("explorer.exe", $"/select,\"{p}\"");
    }

    void CycleTab(int delta)
    {
        int n = _strip.Documents.Count;
        if (n < 2) return;
        _strip.SelectedIndex = (_strip.SelectedIndex + delta + n) % n;
    }

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var f in files.Where(File.Exists)) OpenFile(f);
    }

    /// <summary>Controlla, quando la finestra torna attiva, se i file aperti sono stati modificati o eliminati da altri programmi.</summary>
    void CheckDiskChanges()
    {
        if (_checkingDisk || _closing) return;
        _checkingDisk = true;
        try
        {
            foreach (var d in Docs.ToList())
            {
                if (d.FilePath is null) continue;
                if (d.MissingOnDisk)
                {
                    if (d.MissingAcknowledged) continue;
                    d.MissingAcknowledged = true;
                    _strip.Select(d);
                    var r = MessageBox.Show(this, $"{d.FilePath}\n\nIl file non esiste più (eliminato, spostato o rinominato da un altro programma).\nMantenerlo aperto nell'editor?",
                        "File eliminato", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.Yes) d.MarkDirty();
                    else CloseDocument(d, ask: false);
                    continue;
                }
                if (!d.ChangedOnDisk) continue;
                _strip.Select(d);
                var msg = $"{d.FilePath}\n\nIl file è stato modificato da un altro programma.\nRicaricarlo?" + (d.IsModified ? "\n(Le modifiche non salvate andranno perse.)" : "");
                if (MessageBox.Show(this, msg, "Ricarica", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    d.Reload(CurrentTheme, _settings.FontName, _settings.FontSize);
                    ApplyViewSettings(d);
                    DeleteBackup(d);
                }
                else d.AcknowledgeDiskState();
            }
        }
        finally { _checkingDisk = false; }
    }

    // =====================================================================
    // Sessione, salvataggio automatico e recupero
    // =====================================================================
    void UpdateSnapshotTimer()
    {
        _snapshotTimer.Interval = Math.Clamp(_settings.SnapshotSeconds, 2, 300) * 1000;
        _snapshotTimer.Enabled = true; // anche senza snapshot: controllo periodico dei file su disco
    }

    static string BackupPath(string name) => Path.Combine(Settings.BackupDir, name);

    void DeleteBackup(Document d)
    {
        if (d.BackupFile is null) return;
        try { File.Delete(BackupPath(d.BackupFile)); } catch { }
        d.BackupFile = null;
    }

    /// <summary>Scrive i backup dei buffer modificati e la sessione corrente in modo asincrono.</summary>
    void SnapshotNow()
    {
        if (!SnapshotActive) return;
        var toWrite = new List<(string path, string text)>();
        try
        {
            Directory.CreateDirectory(Settings.BackupDir);
            foreach (var d in Docs)
            {
                if (d.IsModified)
                {
                    if (d.BackupFile is null || d.DirtySinceBackup || !File.Exists(BackupPath(d.BackupFile)))
                    {
                        d.BackupFile ??= $"{(d.FilePath is { } p ? Path.GetFileName(p) : d.DisplayName)}@{Guid.NewGuid():N}.bak";
                        toWrite.Add((BackupPath(d.BackupFile), d.Editor.Text));
                        d.DirtySinceBackup = false;
                    }
                }
                else DeleteBackup(d);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Errore preparazione snapshot", ex);
        }

        if (toWrite.Count > 0)
        {
            Task.Run(() =>
            {
                foreach (var (path, text) in toWrite)
                {
                    try
                    {
                        string tmp = path + ".tmp";
                        File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
                        File.Move(tmp, path, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Errore scrittura backup {path}", ex);
                    }
                }
            });
        }
        StoreSession(includeBackups: true);
    }

    void StoreSession(bool includeBackups)
    {
        _settings.Session = Docs
            .Where(d => d.FilePath != null || (includeBackups && d.IsModified))
            .Select(d => new SessionEntry
            {
                Path = d.FilePath,
                Backup = includeBackups && d.IsModified ? d.BackupFile : null,
                Title = d.FilePath is null ? d.DisplayName : null,
                Encoding = d.Encoding,
                Language = d.Language.Name,
                Caret = d.Editor.CurrentPosition,
                FirstLine = d.Editor.FirstVisibleLine,
            }).ToList();
        _settings.ActiveIndex = Current is { } c ? _settings.Session.FindIndex(e => e.Path == c.FilePath && (e.Path != null || e.Title == c.DisplayName)) : 0;
        _settings.Save();
    }

    void RestoreAndOpenArgs()
    {
        ApplyTheme();
        if (_settings.RestoreSession)
        {
            foreach (var entry in _settings.Session)
            {
                try
                {
                    Document? d = null;
                    var bak = entry.Backup is { } b ? BackupPath(b) : null;
                    if (SnapshotActive && bak != null && File.Exists(bak))
                    {
                        d = AddDocument();
                        d.BackupFile = entry.Backup;
                        d.RestoreFromBackup(entry.Path, File.ReadAllText(bak), entry.Encoding, CurrentTheme, _settings.FontName, _settings.FontSize);
                        if (entry.Title != null) d.SetUntitledName(entry.Title);
                        ApplyViewSettings(d);
                    }
                    else if (entry.Path != null && File.Exists(entry.Path)) d = OpenFile(entry.Path);
                    if (d is null) continue;
                    if (Languages.All.FirstOrDefault(l => l.Name == entry.Language) is { } lang && lang != d.Language)
                        d.SetLanguage(lang, CurrentTheme, _settings.FontName, _settings.FontSize);
                    d.Editor.GotoPosition(Math.Min(entry.Caret, d.Editor.TextLength));
                    d.Editor.FirstVisibleLine = entry.FirstLine;
                }
                catch { /* voce di sessione corrotta: ignora */ }
            }
            if (_settings.ActiveIndex >= 0 && _settings.ActiveIndex < _strip.Documents.Count) _strip.SelectedIndex = _settings.ActiveIndex;
        }
        CleanOrphanBackups();
        foreach (var a in _startupArgs) OpenFile(a);
        if (_strip.Documents.Count == 0) AddDocument();
        ApplyViewSettings();
        OnActiveDocChanged();
        UpdateSnapshotTimer();
    }

    void CleanOrphanBackups()
    {
        try
        {
            if (!Directory.Exists(Settings.BackupDir)) return;
            var used = Docs.Select(d => d.BackupFile).Where(b => b != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(Settings.BackupDir, "*.bak"))
                if (!used.Contains(Path.GetFileName(f))) File.Delete(f);
        }
        catch { }
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (SnapshotActive)
        {
            // Nessuna domanda: i buffer non salvati restano nei backup e tornano al prossimo avvio.
            SnapshotNow();
        }
        else
        {
            foreach (var d in Docs.Where(d => d.IsModified).ToList())
                if (!ConfirmClose(d)) { e.Cancel = true; return; }
            foreach (var d in Docs) DeleteBackup(d);
            StoreSession(includeBackups: false);
        }
        _closing = true;
        _snapshotTimer.Stop();
        _settings.Maximized = WindowState == FormWindowState.Maximized;
        _settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Save();
    }

    // =====================================================================
    // Vista / tema
    // =====================================================================
    void ApplyViewSettings()
    {
        foreach (var d in Docs) ApplyViewSettings(d);
        _miWrap.Checked = _settings.WordWrap;
        _miWs.Checked = _settings.ShowWhitespace;
        _miEol.Checked = _settings.ShowEol;
        _miGuides.Checked = _settings.ShowIndentGuides;
        _miLineNo.Checked = _settings.ShowLineNumbers;
        _miMini.Checked = _settings.ShowMinimap;
        _miSmart.Checked = _settings.SmartHighlight;
        _mini.Visible = _settings.ShowMinimap;
        if (_mini.Visible) AttachMinimap();
        RefreshToolbarIcons();
    }

    void ApplyViewSettings(Document d)
    {
        var e = d.Editor;
        e.WrapMode = _settings.WordWrap ? WrapMode.Word : WrapMode.None;
        e.ViewWhitespace = _settings.ShowWhitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
        e.ViewEol = _settings.ShowEol;
        e.IndentationGuides = _settings.ShowIndentGuides ? IndentView.LookBoth : IndentView.None;
        e.Zoom = _settings.Zoom;
        d.SmartHighlightEnabled = _settings.SmartHighlight;
        d.ShowLineNumbers(_settings.ShowLineNumbers);
    }

    void ApplyTheme()
    {
        var t = CurrentTheme;
        ToolStripManager.Renderer = new ThemedRenderer(t);
        BackColor = t.UiBack;
        _host.BackColor = t.Back;
        foreach (var ts in new ToolStrip[] { _menu, _toolbar, _status }) { ts.BackColor = t.UiBack; ts.ForeColor = t.UiFore; }
        _strip.SetTheme(t);
        _findPanel.SetTheme(t);
        t.ApplyTitleBar(this);
        foreach (var d in Docs) { d.SetLanguage(d.Language, t, _settings.FontName, _settings.FontSize); ApplyViewSettings(d); }
        AttachMinimap();
        RefreshToolbarIcons();
        _terminal.ApplyTheme(t);
        _mainSplit.BackColor = t.UiBorder;
        _menu.Invalidate(true);
        _status.Invalidate(true);
    }

    void CycleTheme()
    {
        int i = Array.IndexOf(Theme.All, CurrentTheme);
        _settings.ThemeName = Theme.All[(i + 1) % Theme.All.Length].Name;
        ApplyTheme();
    }

    void AttachMinimap()
    {
        if (!_settings.ShowMinimap) { _mini.Attach(null, CurrentTheme, _settings.FontName); return; }
        _mini.Attach(Current, CurrentTheme, _settings.FontName);
    }

    void SetZoom(int z)
    {
        _settings.Zoom = Math.Clamp(z, -10, 20);
        foreach (var d in Docs) { d.Editor.Zoom = _settings.Zoom; d.ShowLineNumbers(_settings.ShowLineNumbers); }
        _mini.Sync();
        UpdateStatus();
    }

    void ChooseFont()
    {
        using var dlg = new FontDialog { Font = new Font(_settings.FontName, _settings.FontSize), ShowEffects = false };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _settings.FontName = dlg.Font.Name;
        _settings.FontSize = (int)Math.Round(dlg.Font.SizeInPoints);
        ApplyTheme();
    }

    void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = _preFullScreenState;
            _toolbar.Visible = _status.Visible = true;
        }
        else
        {
            _preFullScreenState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
            _toolbar.Visible = _status.Visible = false;
        }
    }

    void OnActiveDocChanged()
    {
        var cur = Current;
        foreach (var d in Docs) d.Visible = d == cur;
        AttachMinimap();
        _findPanel.RefreshSearch();
        UpdateTitle();
        UpdateStatus();
        cur?.Editor.Focus();

        if (!_mainSplit.Panel2Collapsed && cur?.FilePath != null)
        {
            _terminal.SetWorkingDirectory(Path.GetDirectoryName(cur.FilePath)!);
        }
        if (cur?.FilePath != null)
        {
            _ = GitGutter.UpdateGutterAsync(cur.Editor, cur.FilePath);
        }
    }

    void UpdateTitle()
    {
        var d = Current;
        Text = d is null ? "NotepadREA" : $"{(d.IsModified ? "*" : "")}{d.FilePath ?? d.DisplayName} - NotepadREA";
    }

    void UpdateStatus()
    {
        if (Current is not { } d) return;
        var e = d.Editor;
        int pos = e.CurrentPosition;
        int line = e.LineFromPosition(pos);
        int col = e.GetColumn(pos);
        int sel = Math.Abs(e.SelectionEnd - e.SelectionStart);
        _stLang.Text = d.Language.Name;
        _stLen.Text = $"lunghezza: {e.TextLength:N0}   righe: {e.Lines.Count:N0}";
        _stPos.Text = $"Rg {line + 1:N0}, Col {col + 1:N0}" + (sel > 0 ? $"  ({sel:N0} sel.)" : "") + (e.Selections.Count > 1 ? $"  [{e.Selections.Count} cursori]" : "");
        _stEol.Text = (e.EolMode switch { Eol.CrLf => "Windows (CR LF)", Eol.Lf => "Unix (LF)", _ => "Macintosh (CR)" }) + (d.HasMixedEol ? " (misti)" : "");
        _stEnc.Text = Document.EncodingLabel(d.Encoding);
        _stIns.Text = e.Overtype ? "SSC" : "INS";
    }

    void OpenFindInFiles()
    {
        string dir = Current?.FilePath != null ? Path.GetDirectoryName(Current.FilePath)! : Directory.GetCurrentDirectory();
        string? text = Current?.Editor.SelectedText;
        var form = new FindInFilesForm(dir, string.IsNullOrWhiteSpace(text) ? null : text);
        form.ResultSelected += (res) =>
        {
            var doc = OpenFile(res.FilePath);
            if (doc != null)
            {
                doc.GotoLine(res.LineNumber - 1);
                if (res.MatchIndex >= 0 && res.MatchLength > 0)
                {
                    doc.Editor.SetSelection(res.MatchIndex + res.MatchLength, res.MatchIndex);
                }
            }
        };
        form.Show(this);
    }

    void RunMacroMultipleTimes()
    {
        if (Current == null || MacroManager.Instance.Steps.Count == 0)
        {
            MessageBox.Show(this, "Nessuna macro registrata da eseguire.", "Macro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var input = Microsoft.VisualBasic.Interaction.InputBox("Quante volte eseguire la macro?", "Esegui macro", "5");
        if (int.TryParse(input, out int times) && times > 0)
        {
            MacroManager.Instance.Play(Current, times);
        }
    }

    // =====================================================================
    // Comandi di modifica
    // =====================================================================
    void Exec(Command c) => Current?.Editor.ExecuteCmd(c);

    void InsertText(string text) => Current?.Editor.ReplaceSelection(text);

    static void CopyToClipboard(string? text)
    {
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    void ToggleReadOnly(Document? d)
    {
        if (d is null) return;
        d.Editor.ReadOnly = !d.Editor.ReadOnly;
        d.RaiseState();
    }

    /// <summary>Ctrl+Maiusc+D: aggiunge un cursore sull'occorrenza successiva della selezione (multi-cursore).</summary>
    void AddNextOccurrence()
    {
        if (Current is not { } d) return;
        var e = d.Editor;
        if (e.SelectionStart == e.SelectionEnd)
        {
            int s = e.WordStartPosition(e.CurrentPosition, true), en = e.WordEndPosition(e.CurrentPosition, true);
            if (en > s) e.SetSelection(en, s);
            return;
        }
        e.TargetWholeDocument();
        e.SearchFlags = SearchFlags.MatchCase;
        e.DirectMessage(2688 /* SCI_MULTIPLESELECTADDNEXT */);
        e.ScrollCaret();
    }

    /// <summary>Applica una trasformazione alle righe selezionate (o a tutto il documento senza selezione).</summary>
    void TransformLines(Func<List<string>, List<string>> f)
    {
        if (Current is not { } d || d.Editor.ReadOnly) return;
        var e = d.Editor;
        bool hasSel = e.SelectionStart != e.SelectionEnd;
        int firstLine = hasSel ? e.LineFromPosition(e.SelectionStart) : 0;
        int lastLine = hasSel ? e.LineFromPosition(e.SelectionEnd) : e.Lines.Count - 1;
        if (hasSel && lastLine > firstLine && e.SelectionEnd == e.Lines[lastLine].Position) lastLine--;
        int start = e.Lines[firstLine].Position;
        int end = e.Lines[lastLine].EndPosition;
        string eol = e.EolMode switch { Eol.CrLf => "\r\n", Eol.Lf => "\n", _ => "\r" };

        var block = e.GetTextRange(start, end - start);
        bool trailingEol = block.EndsWith('\n') || block.EndsWith('\r');
        var lines = block.Split(["\r\n", "\n", "\r"], StringSplitOptions.None).ToList();
        if (trailingEol) lines.RemoveAt(lines.Count - 1);
        var result = string.Join(eol, f(lines)) + (trailingEol ? eol : "");
        if (result == block) return;

        e.TargetStart = start;
        e.TargetEnd = end;
        e.ReplaceTarget(result);
        if (hasSel) e.SetSelection(start + result.Length, start);
    }

    static List<string> RemoveConsecutiveDuplicates(List<string> l)
    {
        var r = new List<string>();
        foreach (var x in l) if (r.Count == 0 || r[^1] != x) r.Add(x);
        return r;
    }

    string LeadingSpacesToTabs(string s)
    {
        int tw = Current?.Editor.TabWidth ?? 4;
        int n = s.TakeWhile(c => c == ' ').Count();
        return new string('\t', n / tw) + new string(' ', n % tw) + s[n..];
    }

    static string SentenceCase(string s)
    {
        var chars = s.ToLower().ToCharArray();
        bool start = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (start && char.IsLetter(chars[i])) { chars[i] = char.ToUpper(chars[i]); start = false; }
            else if (chars[i] is '.' or '!' or '?') start = true;
        }
        return new string(chars);
    }

    void TransformSelection(Func<string, string> f)
    {
        if (Current is not { } d || d.Editor.ReadOnly) return;
        var e = d.Editor;
        if (e.SelectionStart == e.SelectionEnd) return;
        int a = e.AnchorPosition, c = e.CurrentPosition;
        e.ReplaceSelection(f(e.SelectedText));
        e.SetSelection(c, a);
    }

    void TransformSelectionOrAll(Func<string, string> f)
    {
        if (Current is not { } d || d.Editor.ReadOnly) return;
        var e = d.Editor;
        e.BeginUndoAction();
        try
        {
            if (e.SelectionStart != e.SelectionEnd)
            {
                int a = e.AnchorPosition, c = e.CurrentPosition;
                e.ReplaceSelection(f(e.SelectedText));
                e.SetSelection(c, a);
            }
            else
            {
                int cur = e.CurrentPosition;
                e.Text = f(e.Text);
                if (cur <= e.TextLength) e.CurrentPosition = cur;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Errore durante l'operazione: " + ex.Message, "Strumenti", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            e.EndUndoAction();
        }
    }

    void OpenDiffDialog()
    {
        using var diff = new DiffForm(Docs.ToList(), Current, CurrentTheme);
        diff.ShowDialog(this);
    }

    void ToggleTerminal()
    {
        _mainSplit.Panel2Collapsed = !_mainSplit.Panel2Collapsed;
        if (!_mainSplit.Panel2Collapsed)
        {
            if (_mainSplit.SplitterDistance > _mainSplit.Height - 120 || _mainSplit.SplitterDistance < 100)
            {
                _mainSplit.SplitterDistance = Math.Max(150, (int)(_mainSplit.Height * 0.68));
            }
            string dir = Current?.FilePath != null ? Path.GetDirectoryName(Current.FilePath)! : Directory.GetCurrentDirectory();
            _terminal.StartShell(dir);
            _terminal.SetWorkingDirectory(dir);
            _terminal.FocusInput();
        }
        else
        {
            Current?.Editor.Focus();
        }
    }

    void OpenHexView()
    {
        if (Current == null) return;
        byte[] data;
        string title = Current.DisplayName;
        if (!string.IsNullOrEmpty(Current.FilePath) && File.Exists(Current.FilePath) && !Current.IsModified)
        {
            try { data = File.ReadAllBytes(Current.FilePath); }
            catch { data = TextCodec.Encode(Current.Editor.Text, Current.Encoding); }
        }
        else
        {
            data = TextCodec.Encode(Current.Editor.Text, Current.Encoding);
        }
        using var hexForm = new HexForm(title, data, CurrentTheme);
        hexForm.ShowDialog(this);
    }

    void JoinLines()
    {
        if (Current is not { } d) return;
        var e = d.Editor;
        if (e.SelectionStart == e.SelectionEnd) return;
        e.TargetFromSelection();
        e.DirectMessage(2288 /* SCI_LINESJOIN */);
    }

    void ToggleLineComment()
    {
        if (Current is not { } d || d.Language.LineComment is not { } tok)
        {
            if (Current?.Language.BlockComment is not null) BlockComment();
            return;
        }
        var e = d.Editor;
        int first = e.LineFromPosition(e.SelectionStart);
        int last = e.LineFromPosition(e.SelectionEnd);
        if (last > first && e.SelectionEnd == e.Lines[last].Position) last--;
        var trimmedTok = tok.TrimEnd();
        bool allCommented = Enumerable.Range(first, last - first + 1)
            .Select(i => e.Lines[i].Text.TrimStart())
            .Where(t => t.Trim().Length > 0)
            .All(t => t.StartsWith(trimmedTok, StringComparison.OrdinalIgnoreCase));
        e.BeginUndoAction();
        for (int i = first; i <= last; i++)
        {
            var line = e.Lines[i];
            var text = line.Text;
            if (text.Trim().Length == 0) continue;
            int indent = text.Length - text.TrimStart().Length;
            int pos = line.Position + indent;
            if (allCommented)
            {
                int len = text[indent..].StartsWith(tok, StringComparison.OrdinalIgnoreCase) ? tok.Length : trimmedTok.Length;
                e.DeleteRange(pos, len);
            }
            else e.InsertText(pos, tok);
        }
        e.EndUndoAction();
    }

    void BlockComment()
    {
        if (Current is not { } d || d.Language.BlockComment is not { } bc) return;
        var e = d.Editor;
        if (e.SelectionStart == e.SelectionEnd) return;
        var text = e.SelectedText;
        e.ReplaceSelection(text.StartsWith(bc.Open) && text.EndsWith(bc.Close)
            ? text[bc.Open.Length..^bc.Close.Length]
            : bc.Open + text + bc.Close);
    }

    void SetEncoding(FileEncoding enc)
    {
        if (Current is not { } d || d.Encoding == enc) return;
        d.Encoding = enc;
        d.MarkDirty(); // cambiare codifica rende il documento "da salvare"
        UpdateStatus();
    }

    void ReinterpretAs(FileEncoding enc)
    {
        if (Current is not { FilePath: not null } d || d.MissingOnDisk) return;
        if (d.IsModified && MessageBox.Show(this, "Le modifiche non salvate andranno perse. Continuare?", "Codifica", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        d.Reinterpret(enc);
        DeleteBackup(d);
        UpdateStatus();
    }

    void SetEol(Eol eol)
    {
        if (Current is not { } d) return;
        d.Editor.EolMode = eol;
        d.Editor.ConvertEols(eol);
        UpdateStatus();
    }

    // =====================================================================
    // Ricerca / navigazione
    // =====================================================================
    void QuickFind(bool forward)
    {
        if (Current is not { } d) return;
        var e = d.Editor;
        string word = e.SelectedText;
        if (string.IsNullOrEmpty(word))
        {
            int s = e.WordStartPosition(e.CurrentPosition, true), en = e.WordEndPosition(e.CurrentPosition, true);
            word = e.GetTextRange(s, en - s);
            e.SetSelection(en, s);
        }
        if (string.IsNullOrEmpty(word)) return;
        e.SearchFlags = SearchFlags.MatchCase;
        e.TargetStart = forward ? e.SelectionEnd : e.SelectionStart;
        e.TargetEnd = forward ? e.TextLength : 0;
        if (e.SearchInTarget(word) < 0)
        {
            e.TargetStart = forward ? 0 : e.TextLength;
            e.TargetEnd = forward ? e.SelectionStart : e.SelectionEnd;
            if (e.SearchInTarget(word) < 0) return;
        }
        e.SetSelection(e.TargetEnd, e.TargetStart);
        e.ScrollCaret();
    }

    void GotoLineDialog()
    {
        if (Current is not { } d) return;
        var input = Prompt("Vai alla riga", $"Numero di riga (1 - {d.Editor.Lines.Count:N0}):", (d.Editor.CurrentLine + 1).ToString());
        if (int.TryParse(input, out int n)) d.GotoLine(n - 1);
    }

    void QuickOpen()
    {
        var entries = new List<QuickOpenForm.Entry>();
        entries.AddRange(Docs.Where(d => d.FilePath != null).Select(d => new QuickOpenForm.Entry(d.FilePath!, "• aperto")));
        entries.AddRange(_settings.RecentFiles.Where(File.Exists).Select(p => new QuickOpenForm.Entry(p, "recente")));
        var dir = Current?.FilePath is { } cp ? Path.GetDirectoryName(cp) : null;
        if (dir != null)
        {
            try
            {
                entries.AddRange(Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true })
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
                    .Take(3000)
                    .Select(f => new QuickOpenForm.Entry(f, "cartella")));
            }
            catch { }
        }
        using var qo = new QuickOpenForm(entries, CurrentTheme);
        qo.Location = PointToScreen(new Point(Math.Max(0, (ClientSize.Width - qo.Width) / 2), _strip.Bottom + 4));
        if (qo.ShowDialog(this) != DialogResult.OK) return;
        if (qo.GotoLine is int line) Current?.GotoLine(line - 1);
        else if (qo.SelectedPath != null) OpenFile(qo.SelectedPath);
    }

    void GotoMatchingBrace()
    {
        if (Current is not { } d) return;
        var e = d.Editor;
        int pos = e.CurrentPosition;
        int m = e.BraceMatch(pos);
        if (m < 0 && pos > 0) m = e.BraceMatch(pos - 1);
        if (m >= 0) e.GotoPosition(m);
    }

    // =====================================================================
    // Strumenti
    // =====================================================================
    void ShowHashes()
    {
        if (Current is not { } d) return;
        var text = d.Editor.SelectedText is { Length: > 0 } s ? s : d.Editor.Text;
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var md5 = Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(bytes));
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var r = MessageBox.Show(this, $"MD5:     {md5}\nSHA-256: {sha}\n\n(testo codificato UTF-8)\n\nCopiare negli appunti?", "Hash", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r == DialogResult.Yes) Clipboard.SetText($"MD5: {md5}\r\nSHA-256: {sha}");
    }

    void ShowSummary()
    {
        if (Current is not { } d) return;
        var t = d.Editor.Text;
        int words = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var bytes = Document.Encode(t, d.Encoding).Length;
        MessageBox.Show(this,
            $"Percorso: {d.FilePath ?? "(non salvato)"}\n\nCaratteri: {t.Length:N0}\nParole: {words:N0}\nRighe: {d.Editor.Lines.Count:N0}\nDimensione su disco ({Document.EncodingLabel(d.Encoding)}): {bytes:N0} byte",
            "Statistiche", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    string? Prompt(string title, string label, string initial)
    {
        var t = CurrentTheme;
        using var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(340, 110), BackColor = t.UiBack, ForeColor = t.UiFore };
        var lbl = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };
        var tb = new TextBox { Text = initial, Left = 12, Top = 36, Width = 316 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 172, Top = 72, Width = 75 };
        var cancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Left = 253, Top = 72, Width = 75 };
        f.Controls.AddRange([lbl, tb, ok, cancel]);
        t.Paint(f);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        f.HandleCreated += (_, _) => t.ApplyTitleBar(f);
        tb.SelectAll();
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _snapshotTimer.Dispose();
        base.Dispose(disposing);
    }
}
