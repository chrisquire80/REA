using System.Diagnostics;
using ScintillaNET;

namespace NotepadREA;

public sealed partial class MainForm
{
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
        form.ResultSelected += async (res) =>
        {
            var doc = await OpenFileAsync(res.FilePath);
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

    public async Task OpenHexViewForPathAsync(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);
                var hexForm = new HexForm(Path.GetFileName(path), data, CurrentTheme);
                hexForm.Show(this);
                return;
            }
            catch (Exception ex) { Log.Warn($"Impossibile leggere {path} per la vista esadecimale.", ex); }
        }
        var doc = await OpenFileAsync(path);
        if (doc != null) OpenHexView();
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

    async void QuickOpen()
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
        else if (qo.SelectedPath != null) await OpenFileAsync(qo.SelectedPath);
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
