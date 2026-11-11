using System.Text.RegularExpressions;

namespace NotepadREA;

public sealed class FindInFilesResult
{
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string LineText { get; set; } = "";
    public int MatchIndex { get; set; }
    public int MatchLength { get; set; }
}

public sealed class FindInFilesForm : Form
{
    readonly ComboBox _cbFind = new() { Dock = DockStyle.Fill };
    readonly TextBox _tbDir = new() { Dock = DockStyle.Fill };
    readonly TextBox _tbFilters = new() { Dock = DockStyle.Fill, Text = "*.*" };
    readonly TextBox _tbReplace = new() { Dock = DockStyle.Fill };
    readonly CheckBox _chkSubdirs = new() { Text = "Includi sottocartelle", Checked = true, AutoSize = true };
    readonly CheckBox _chkCase = new() { Text = "Maiuscole/minuscole", AutoSize = true };
    readonly CheckBox _chkWord = new() { Text = "Parola intera", AutoSize = true };
    readonly CheckBox _chkRegex = new() { Text = "Espressione regolare", AutoSize = true };

    readonly Button _btnFindAll = new() { Text = "Trova nei file", AutoSize = true };
    readonly Button _btnReplaceAll = new() { Text = "Sostituisci nei file", AutoSize = true };
    readonly Button _btnCancel = new() { Text = "Annulla ricerca", Enabled = false, AutoSize = true };
    readonly Label _lblStatus = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    readonly ListView _lvResults = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };

    CancellationTokenSource? _cts;
    public event Action<FindInFilesResult>? ResultSelected;

    public FindInFilesForm(string initialDir, string? initialSearch = null)
    {
        Text = "Trova nei file";
        Size = new Size(780, 520);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        Icon = SystemIcons.Application;

        if (!string.IsNullOrEmpty(initialDir)) _tbDir.Text = initialDir;
        if (!string.IsNullOrEmpty(initialSearch)) _cbFind.Text = initialSearch;

        SetupLayout();
    }

    void SetupLayout()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 175, ColumnCount = 3, Padding = new Padding(10) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        top.Controls.Add(new Label { Text = "Trova:", Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_cbFind, 1, 0);

        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        _btnFindAll.Click += async (_, _) => await RunSearchAsync(isReplace: false);
        _btnReplaceAll.Click += async (_, _) => await RunSearchAsync(isReplace: true);
        _btnCancel.Click += (_, _) => _cts?.Cancel();
        btnPanel.Controls.AddRange([_btnFindAll, _btnReplaceAll, _btnCancel]);
        top.Controls.Add(btnPanel, 2, 0);
        top.SetRowSpan(btnPanel, 5);

        top.Controls.Add(new Label { Text = "Sostituisci:", Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_tbReplace, 1, 1);

        top.Controls.Add(new Label { Text = "Cartella:", Anchor = AnchorStyles.Left }, 0, 2);
        var dirPanel = new Panel { Dock = DockStyle.Fill };
        var btnBrowse = new Button { Text = "...", Dock = DockStyle.Right, Width = 35 };
        btnBrowse.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _tbDir.Text };
            if (fbd.ShowDialog(this) == DialogResult.OK) _tbDir.Text = fbd.SelectedPath;
        };
        dirPanel.Controls.Add(_tbDir);
        dirPanel.Controls.Add(btnBrowse);
        top.Controls.Add(dirPanel, 1, 2);

        top.Controls.Add(new Label { Text = "Filtri:", Anchor = AnchorStyles.Left }, 0, 3);
        top.Controls.Add(_tbFilters, 1, 3);

        var options = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };
        options.Controls.AddRange([_chkSubdirs, _chkCase, _chkWord, _chkRegex]);
        top.Controls.Add(options, 1, 4);

        _lvResults.Columns.Add("File", 240);
        _lvResults.Columns.Add("Riga", 55);
        _lvResults.Columns.Add("Testo", 440);
        _lvResults.DoubleClick += (_, _) =>
        {
            if (_lvResults.SelectedItems.Count > 0 && _lvResults.SelectedItems[0].Tag is FindInFilesResult res)
            {
                ResultSelected?.Invoke(res);
            }
        };

        Controls.Add(_lvResults);
        Controls.Add(_lblStatus);
        Controls.Add(top);
    }

    async Task RunSearchAsync(bool isReplace)
    {
        string dir = _tbDir.Text.Trim();
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, "Cartella non valida.", "Trova nei file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string pattern = _cbFind.Text;
        if (string.IsNullOrEmpty(pattern)) return;

        if (isReplace)
        {
            if (MessageBox.Show(this, $"Confermi la sostituzione nei file della cartella?\n{dir}", "Conferma sostituzione nei file",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        _cts = new CancellationTokenSource();
        _btnFindAll.Enabled = false;
        _btnReplaceAll.Enabled = false;
        _btnCancel.Enabled = true;
        _lvResults.Items.Clear();
        _lblStatus.Text = "Ricerca in corso...";

        var opt = SearchOption.TopDirectoryOnly;
        if (_chkSubdirs.Checked) opt = SearchOption.AllDirectories;

        var patterns = _tbFilters.Text.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (patterns.Length == 0) patterns = ["*.*"];

        bool matchCase = _chkCase.Checked;
        bool wholeWord = _chkWord.Checked;
        bool isRegex = _chkRegex.Checked;
        string replaceWith = _tbReplace.Text;

        int totalMatches = 0;
        int filesMatched = 0;

        try
        {
            var results = await Task.Run(() =>
            {
                var list = new List<FindInFilesResult>();
                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in patterns)
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(dir, p, opt)) files.Add(f);
                    }
                    catch { }
                }

                Regex? regex = null;
                if (isRegex)
                {
                    var ro = RegexOptions.Multiline;
                    if (!matchCase) ro |= RegexOptions.IgnoreCase;
                    regex = new Regex(pattern, ro);
                }

                foreach (var file in files)
                {
                    if (_cts.IsCancellationRequested) break;

                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.Length > 50 * 1024 * 1024) continue; // Salta file > 50 MB
                        byte[] bytes = File.ReadAllBytes(file);
                        if (TextCodec.LooksBinary(bytes)) continue; // Salta file binari

                        var (text, enc) = TextCodec.Decode(bytes);
                        bool modified = false;

                        if (isRegex && regex != null)
                        {
                            var matches = regex.Matches(text);
                            if (matches.Count > 0)
                            {
                                filesMatched++;
                                foreach (Match m in matches)
                                {
                                    int line = GetLineNumber(text, m.Index);
                                    string lineText = GetLineText(text, m.Index);
                                    list.Add(new FindInFilesResult
                                    {
                                        FilePath = file,
                                        LineNumber = line,
                                        LineText = lineText,
                                        MatchIndex = m.Index,
                                        MatchLength = m.Length
                                    });
                                }
                                if (isReplace)
                                {
                                    text = regex.Replace(text, replaceWith);
                                    modified = true;
                                }
                            }
                        }
                        else
                        {
                            var sc = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                            int idx = 0;
                            bool fileHadMatch = false;

                            while ((idx = text.IndexOf(pattern, idx, sc)) >= 0)
                            {
                                if (_cts.IsCancellationRequested) break;

                                bool ok = true;
                                if (wholeWord)
                                {
                                    int end = idx + pattern.Length;
                                    bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                                    bool endOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                                    ok = startOk && endOk;
                                }

                                if (ok)
                                {
                                    fileHadMatch = true;
                                    int line = GetLineNumber(text, idx);
                                    string lineText = GetLineText(text, idx);
                                    list.Add(new FindInFilesResult
                                    {
                                        FilePath = file,
                                        LineNumber = line,
                                        LineText = lineText,
                                        MatchIndex = idx,
                                        MatchLength = pattern.Length
                                    });
                                }
                                idx += pattern.Length;
                            }

                            if (fileHadMatch)
                            {
                                filesMatched++;
                                if (isReplace)
                                {
                                    text = ReplaceString(text, pattern, replaceWith, sc, wholeWord);
                                    modified = true;
                                }
                            }
                        }

                        if (isReplace && modified)
                        {
                            string tmp = file + ".tmp";
                            File.WriteAllBytes(tmp, TextCodec.Encode(text, enc));
                            File.Move(tmp, file, overwrite: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Errore ricerca nel file {file}", ex);
                    }
                }

                return list;
            }, _cts.Token);

            totalMatches = results.Count;
            _lvResults.BeginUpdate();
            foreach (var r in results)
            {
                var item = new ListViewItem(r.FilePath);
                item.SubItems.Add(r.LineNumber.ToString());
                item.SubItems.Add(r.LineText.Trim());
                item.Tag = r;
                _lvResults.Items.Add(item);
            }
            _lvResults.EndUpdate();

            _lblStatus.Text = isReplace
                ? $"Sostituzioni completate: {totalMatches} occorrenze in {filesMatched} file."
                : $"Trovate {totalMatches} occorrenze in {filesMatched} file.";
        }
        catch (OperationCanceledException)
        {
            _lblStatus.Text = "Ricerca annullata.";
        }
        finally
        {
            _btnFindAll.Enabled = true;
            _btnReplaceAll.Enabled = true;
            _btnCancel.Enabled = false;
        }
    }

    static int GetLineNumber(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < Math.Min(index, text.Length); i++)
        {
            if (text[i] == '\n') line++;
        }
        return line;
    }

    static string GetLineText(string text, int index)
    {
        int start = index;
        while (start > 0 && text[start - 1] != '\n' && text[start - 1] != '\r') start--;
        int end = index;
        while (end < text.Length && text[end] != '\n' && text[end] != '\r') end++;
        return text[start..end];
    }

    static string ReplaceString(string text, string search, string replace, StringComparison comp, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search)) return text;
        var sb = new System.Text.StringBuilder();
        int idx = 0;
        int last = 0;
        while ((idx = text.IndexOf(search, last, comp)) >= 0)
        {
            bool ok = true;
            if (wholeWord)
            {
                int end = idx + search.Length;
                bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                bool endOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                ok = startOk && endOk;
            }

            if (ok)
            {
                sb.Append(text[last..idx]);
                sb.Append(replace);
                last = idx + search.Length;
            }
            else
            {
                sb.Append(text[last..(idx + 1)]);
                last = idx + 1;
            }
        }
        sb.Append(text[last..]);
        return sb.ToString();
    }
}
