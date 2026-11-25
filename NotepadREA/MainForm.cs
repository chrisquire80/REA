using System.Diagnostics;
using ScintillaNET;

namespace NotepadREA;

/// <summary>
/// Finestra principale. Struttura menu e scorciatoie ricavate dall'analisi di Notepad++ 8.9.8.1
/// (localization/english.xml, shortcuts.xml, risorse RT_MENU/RT_DIALOG) e reimplementate da zero.
/// </summary>
public sealed partial class MainForm : Form
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
    readonly HashSet<string> _openingFiles = new(StringComparer.OrdinalIgnoreCase);
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
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem($"&{(++i) % 10} {p}", null, async (_, _) => await OpenFileAsync(p)));
        }
        if (i == 0) _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(vuoto)") { Enabled = false });
        else
        {
            _recentMenu.DropDownItems.Add(Sep());
            _recentMenu.DropDownItems.Add(Item("Svuota elenco", () => _settings.RecentFiles.Clear()));
        }
    }
}
