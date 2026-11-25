using System.Diagnostics;
using ScintillaNET;

namespace NotepadREA;

public sealed partial class MainForm
{
    /// <summary>Elabora argomenti CLI o comandi ricevuti tramite IPC/NamedPipe.</summary>
    public async void HandleCommandLineArgs(string[] args)
    {
        if (WindowState == FormWindowState.Minimized)
            WindowState = _settings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;

        int gotoLine = -1;
        int gotoCol = -1;
        string? diffA = null;
        string? diffB = null;
        string? hexTarget = null;
        var filesToOpen = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].Trim();
            if (string.IsNullOrEmpty(a)) continue;

            if (a.Equals("--diff", StringComparison.OrdinalIgnoreCase) && i + 2 < args.Length)
            {
                diffA = args[++i];
                diffB = args[++i];
            }
            else if (a.Equals("--hex", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                hexTarget = args[++i];
            }
            else if (a.Equals("-n", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int l)) gotoLine = l;
            }
            else if (a.StartsWith("-n", StringComparison.OrdinalIgnoreCase) && int.TryParse(a[2..], out int l2))
            {
                gotoLine = l2;
            }
            else if (a.Equals("-c", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int c)) gotoCol = c;
            }
            else if (a.StartsWith("-c", StringComparison.OrdinalIgnoreCase) && int.TryParse(a[2..], out int c2))
            {
                gotoCol = c2;
            }
            else if (!a.StartsWith('-'))
            {
                filesToOpen.Add(a);
            }
        }

        if (diffA != null && diffB != null)
        {
            var diffForm = new DiffForm(Docs.ToList(), Current, CurrentTheme);
            diffForm.LoadFiles(diffA, diffB);
            diffForm.Show(this);
        }

        if (hexTarget != null)
        {
            await OpenHexViewForPathAsync(hexTarget);
        }

        Document? lastOpened = null;
        foreach (var f in filesToOpen)
        {
            var d = await OpenFileAsync(f);
            if (d != null) lastOpened = d;
        }

        if (lastOpened != null && gotoLine > 0)
        {
            int lineIdx = Math.Clamp(gotoLine - 1, 0, Math.Max(0, lastOpened.Editor.Lines.Count - 1));
            int pos = lastOpened.Editor.Lines[lineIdx].Position;
            if (gotoCol > 0) pos += Math.Max(0, gotoCol - 1);
            lastOpened.Editor.GotoPosition(pos);
            lastOpened.Editor.FirstVisibleLine = Math.Max(0, lineIdx - 4);
        }

        Activate();
        CheckDiskChanges();
        BringToFront();
    }

    /// <summary>Chiamato quando l'utente apre file mentre l'editor è già in esecuzione.</summary>
    public void OpenFromOtherInstance(string[] files) => HandleCommandLineArgs(files);

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
            _ = OpenSelectedFilesAsync(dlg.FileNames);
    }

    async Task OpenSelectedFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths) await OpenFileAsync(path);
    }

    public async Task<Document?> OpenFileAsync(string path)
    {
        Document? doc = null;
        bool createdDocument = false;
        bool ownsOpeningPath = false;
        bool previousReadOnly = false;
        bool lockedForLoading = false;
        string? normalizedPath = null;
        try
        {
            normalizedPath = Path.GetFullPath(path);
            var existing = Docs.FirstOrDefault(d => string.Equals(d.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { _strip.Select(existing); return existing; }
            if (!_openingFiles.Add(normalizedPath)) return null;
            ownsOpeningPath = true;
            path = normalizedPath;
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
            bool reuseCurrent = Current is { IsUntitledAndEmpty: true, IsReadOnly: false } && _strip.Documents.Count == 1;
            doc = reuseCurrent ? Current : AddDocument();
            createdDocument = !reuseCurrent;
            var openedDocument = doc ?? throw new InvalidOperationException("Impossibile creare la scheda del documento.");
            previousReadOnly = openedDocument.Editor.ReadOnly;
            openedDocument.Editor.ReadOnly = true;
            lockedForLoading = true;
            await openedDocument.LoadAsync(path, CurrentTheme, _settings.FontName, _settings.FontSize);
            if (openedDocument.IsDisposed) return null;
            lockedForLoading = false;
            ApplyViewSettings(openedDocument);
            _strip.Select(openedDocument);
            if (openedDocument.WasDetectedAsBinary)
            {
                MessageBox.Show(this, $"\"{Path.GetFileName(path)}\" contiene caratteri null o sequenze binarie.\nÈ stato aperto in modalità Sola Lettura per proteggere l'integrità del file.",
                    "File binario", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            _settings.AddRecent(path);
            OnActiveDocChanged();
            return openedDocument;
        }
        catch (Exception ex)
        {
            if (doc?.IsDisposed == true) return null;
            Log.Error($"Impossibile aprire {path}", ex);
            MessageBox.Show(this, $"Impossibile aprire \"{path}\":\n{ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (lockedForLoading && doc != null) doc.Editor.ReadOnly = previousReadOnly;
            if (createdDocument && doc != null)
            {
                _strip.Remove(doc);
                _host.Controls.Remove(doc);
                doc.Dispose();
            }
            return null;
        }
        finally
        {
            if (ownsOpeningPath && normalizedPath != null) _openingFiles.Remove(normalizedPath);
        }
    }

    bool Save(Document? doc)
    {
        if (doc is null) return false;
        if (doc.FilePath is null) return SaveAs(doc);
        try
        {
            try
            {
                doc.SaveTo(doc.FilePath, CurrentTheme, _settings.FontName, _settings.FontSize);
            }
            catch (ExternalFileChangedException)
            {
                var choice = MessageBox.Show(this,
                    $"\"{doc.FilePath}\" è stato modificato da un altro programma.\n\nSì: sovrascrivi le modifiche esterne.\nNo: salva con un altro nome.\nAnnulla: non salvare.",
                    "Conflitto di salvataggio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (choice == DialogResult.Cancel) return false;
                if (choice == DialogResult.No) return SaveAs(doc);
                doc.SaveTo(doc.FilePath, CurrentTheme, _settings.FontName, _settings.FontSize, overwriteExternalChanges: true);
            }
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
            doc.SaveTo(dlg.FileName, CurrentTheme, _settings.FontName, _settings.FontSize, overwriteExternalChanges: true);
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
            if (File.Exists(p)) { _ = OpenFileAsync(p); return; }
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
            _ = OpenSelectedFilesAsync(files.Where(File.Exists));
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

    async void RestoreAndOpenArgs()
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
                    else if (entry.Path != null && File.Exists(entry.Path)) d = await OpenFileAsync(entry.Path);
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
        HandleCommandLineArgs(_startupArgs);
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
}
