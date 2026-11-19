using System.Diagnostics;
using System.Globalization;
using NoreaDesign;

namespace WinSCPREA;

internal sealed class MainForm : Form
{
    private readonly WinScpSettings _settings = WinScpSettings.Load();
    private readonly SftpSessionService _session = new();
    private readonly TextBox _localPath = new();
    private readonly TextBox _remotePath = new();
    private readonly DataGridView _localGrid = new();
    private readonly DataGridView _remoteGrid = new();
    private readonly NoreaPalette _palette = NoreaDesign.NoreaStyles.CurrentPalette;
    private readonly Label _connectionBadge = new();
    private readonly ToolStripStatusLabel _status = new("Pronto");
    private readonly ToolStripProgressBar _progress = new() { Visible = false, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Width = 130 };
    private readonly ToolStripButton _disconnectButton = new("Disconnetti") { Enabled = false };
    private string _localDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _remoteDirectory = "/";
    private bool _busy;
    private bool _activeRemotePane;
    private SiteProfile? _activeSite;

    public MainForm()
    {
        Text = "WinSCPREA · File manager SFTP";
        MinimumSize = new Size(960, 620);
        Size = new Size(1280, 790);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        NoreaDesign.NoreaStyles.ApplyForm(this, _palette);
        KeyPreview = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = Padding.Empty, BackColor = _palette.Bg };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);
        root.Controls.Add(CreateBrandHeader(), 0, 0);

        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Dock = DockStyle.Fill,
            ImageScalingSize = new Size(18, 18),
            Padding = new Padding(14, 4, 14, 4)
        };
        var newSiteButton = new ToolStripButton("Nuovo sito", null, (_, _) => Connect()) { ToolTipText = "Apri una connessione SFTP" };
        var uploadButton = new ToolStripButton("Carica", null, async (_, _) => await UploadSelectedAsync()) { ToolTipText = "Carica i file selezionati · Ctrl+U" };
        var downloadButton = new ToolStripButton("Scarica", null, async (_, _) => await DownloadSelectedAsync()) { ToolTipText = "Scarica i file selezionati · Ctrl+D" };
        var newFolderButton = new ToolStripButton("Nuova cartella", null, async (_, _) => await CreateFolderAsync()) { ToolTipText = "Crea una cartella nel pannello attivo" };
        var renameButton = new ToolStripButton("Rinomina", null, async (_, _) => await RenameSelectedAsync());
        var deleteButton = new ToolStripButton("Elimina", null, async (_, _) => await DeleteSelectedAsync());
        var refreshButton = new ToolStripButton("Aggiorna", null, async (_, _) => await RefreshBothAsync()) { ToolTipText = "Aggiorna i pannelli · F5" };
        toolbar.Items.Add(newSiteButton);
        toolbar.Items.Add(_disconnectButton);
        _disconnectButton.Click += (_, _) => Disconnect();
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(uploadButton);
        toolbar.Items.Add(downloadButton);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(newFolderButton);
        toolbar.Items.Add(renameButton);
        toolbar.Items.Add(deleteButton);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(refreshButton);
        NoreaDesign.NoreaStyles.StyleToolStrip(toolbar, _palette);
        NoreaDesign.NoreaStyles.StyleToolStripButton(newSiteButton, _palette, NoreaButtonStyle.Primary);
        NoreaDesign.NoreaStyles.StyleToolStripButton(uploadButton, _palette, NoreaButtonStyle.Secondary);
        NoreaDesign.NoreaStyles.StyleToolStripButton(downloadButton, _palette, NoreaButtonStyle.Secondary);
        NoreaDesign.NoreaStyles.StyleToolStripButton(deleteButton, _palette, NoreaButtonStyle.Danger);
        root.Controls.Add(toolbar, 0, 1);

        ConfigureGrid(_localGrid);
        ConfigureGrid(_remoteGrid);
        NoreaDesign.NoreaStyles.StyleGrid(_localGrid, _palette);
        NoreaDesign.NoreaStyles.StyleGrid(_remoteGrid, _palette);
        _localGrid.Enter += (_, _) => _activeRemotePane = false;
        _remoteGrid.Enter += (_, _) => _activeRemotePane = true;
        _localGrid.MouseDown += (_, _) => _activeRemotePane = false;
        _remoteGrid.MouseDown += (_, _) => _activeRemotePane = true;
        _localGrid.CellDoubleClick += async (_, e) => await LocalDoubleClickAsync(e.RowIndex);
        _remoteGrid.CellDoubleClick += async (_, e) => await RemoteDoubleClickAsync(e.RowIndex);

        var splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = _palette.Border
        };
        splitter.Panel1.BackColor = _palette.CardBg;
        splitter.Panel2.BackColor = _palette.CardBg;
        splitter.Panel1.Controls.Add(CreatePane("Computer locale", _localPath, _localGrid, NavigateLocalFromBox, () => NavigateLocal(Directory.GetParent(_localDirectory)?.FullName ?? _localDirectory)));
        splitter.Panel2.Controls.Add(CreatePane("Server SFTP", _remotePath, _remoteGrid, NavigateRemoteFromBox, () => NavigateRemote(SftpSessionService.ParentRemote(_remoteDirectory))));
        root.Controls.Add(splitter, 0, 2);

        var statusStrip = new StatusStrip { SizingGrip = false, Dock = DockStyle.Fill };
        statusStrip.Items.Add(_status);
        statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
        statusStrip.Items.Add(_progress);
        statusStrip.Items.Add(new ToolStripStatusLabel("SFTP  ·  SSH.NET  ·  .NET 10") { ForeColor = _palette.TextMuted });
        NoreaDesign.NoreaStyles.StyleStatusStrip(statusStrip, _palette);
        root.Controls.Add(statusStrip, 0, 3);

        Shown += async (_, _) =>
        {
            // At construction time the SplitContainer still has its small default width.
            // Apply minimums and the preferred split only after the form has laid it out.
            splitter.Panel1MinSize = 360;
            splitter.Panel2MinSize = 360;
            int maxDistance = splitter.ClientSize.Width - splitter.Panel2MinSize - splitter.SplitterWidth;
            splitter.SplitterDistance = Math.Clamp(620, splitter.Panel1MinSize, maxDistance);
            await RefreshLocalAsync();
        };
        FormClosing += (_, _) => _session.Dispose();
        KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.F5) { e.Handled = true; await RefreshBothAsync(); }
            else if (e.Control && e.KeyCode == Keys.U) { e.Handled = true; await UploadSelectedAsync(); }
            else if (e.Control && e.KeyCode == Keys.D) { e.Handled = true; await DownloadSelectedAsync(); }
            else if (e.KeyCode == Keys.Delete) { e.Handled = true; await DeleteSelectedAsync(); }
        };
    }

    private Panel CreateBrandHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = _palette.HeaderBg };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(_palette.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var mark = new Panel { Location = new Point(18, 11), Size = new Size(38, 38), BackColor = _palette.BadgeBg };
        mark.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var folder = new SolidBrush(_palette.TextPrimary);
            using var blue = new Pen(_palette.BrandBlue, 2.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
            using var coral = new Pen(_palette.BrandRed, 2.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
            e.Graphics.FillRectangle(folder, 9, 11, 20, 16);
            e.Graphics.FillRectangle(folder, 11, 8, 9, 4);
            e.Graphics.DrawLine(blue, 11, 18, 25, 18);
            e.Graphics.DrawLine(coral, 25, 23, 11, 23);
        };
        header.Controls.Add(mark);

        var title = new Label { Text = "WinSCPREA", AutoSize = true, Location = new Point(70, 9) };
        NoreaDesign.NoreaStyles.StyleLabel(title, _palette, heading: true);
        title.Font = NoreaDesign.NoreaStyles.CreateUiFont(15f, FontStyle.Bold);
        header.Controls.Add(title);

        var subtitle = new Label { Text = "Trasferimento file sicuro · SFTP / SSH", AutoSize = true, Location = new Point(71, 35) };
        NoreaDesign.NoreaStyles.StyleLabel(subtitle, _palette, muted: true);
        subtitle.Font = NoreaDesign.NoreaStyles.CreateUiFont(8.8f);
        header.Controls.Add(subtitle);

        _connectionBadge.AutoSize = false;
        _connectionBadge.Size = new Size(250, 30);
        _connectionBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _connectionBadge.AutoEllipsis = true;
        _connectionBadge.AccessibleName = "Stato connessione";
        _connectionBadge.Location = new Point(header.ClientSize.Width - _connectionBadge.Width - 16, 15);
        _connectionBadge.TextAlign = ContentAlignment.MiddleCenter;
        _connectionBadge.BorderStyle = BorderStyle.FixedSingle;
        header.Controls.Add(_connectionBadge);
        header.Resize += (_, _) => _connectionBadge.Left = header.ClientSize.Width - _connectionBadge.Width - 16;
        SetConnectionBadge(connected: false);
        return header;
    }

    private void SetConnectionBadge(bool connected, string? host = null)
    {
        _connectionBadge.Text = connected ? $"Connesso  ·  {host}" : "Non connesso";
        _connectionBadge.BackColor = connected ? _palette.BadgeBg : _palette.CardBg;
        _connectionBadge.ForeColor = connected ? _palette.StatusGreen : _palette.TextMuted;
        _connectionBadge.Font = NoreaDesign.NoreaStyles.CreateUiFont(9f, FontStyle.Bold);
    }

    private Control CreatePane(string title, TextBox pathBox, DataGridView grid, Action go, Action up)
    {
        var pane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(14, 12, 14, 12), BackColor = _palette.CardBg };
        pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        NoreaDesign.NoreaStyles.StyleLabel(heading, _palette, heading: true);
        heading.Font = NoreaDesign.NoreaStyles.CreateUiFont(10.5f, FontStyle.Bold);
        pane.Controls.Add(heading, 0, 0);
        var address = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 2, 0, 8), BackColor = _palette.CardBg };
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var upButton = new Button { Text = "↑", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), AccessibleName = "Cartella superiore" };
        NoreaDesign.NoreaStyles.StyleButton(upButton, _palette);
        upButton.Click += (_, _) => up();
        address.Controls.Add(upButton, 0, 0);
        pathBox.Dock = DockStyle.Fill;
        pathBox.Margin = new Padding(0, 0, 8, 0);
        pathBox.Height = NoreaDesign.NoreaStyles.ControlHeight;
        NoreaDesign.NoreaStyles.StyleInput(pathBox, _palette, monospace: true);
        address.Controls.Add(pathBox, 1, 0);
        var goButton = new Button { Text = "Vai", Dock = DockStyle.Fill, Margin = Padding.Empty };
        NoreaDesign.NoreaStyles.StyleButton(goButton, _palette, NoreaButtonStyle.Secondary);
        goButton.Click += (_, _) => go();
        pathBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; go(); } };
        address.Controls.Add(goButton, 2, 0);
        pane.Controls.Add(address, 0, 1);
        pane.Controls.Add(grid, 0, 2);
        return pane;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowTemplate.Height = 34;
        grid.ColumnHeadersHeight = 38;
        grid.EnableHeadersVisualStyles = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Nome", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Dimensione", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modified", HeaderText = "Ultima modifica", Width = 145, DefaultCellStyle = new DataGridViewCellStyle { Format = "g" } });
    }

    private async void Connect()
    {
        if (_busy) return;
        using var dialog = new SiteDialog(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null) return;

        var request = dialog.Request;
        _activeSite = request.Site;
        _busy = true;
        UpdateConnectionControls();
        SetBusy("Connessione a " + request.Site.Host + "…");
        try
        {
            await _session.ConnectAsync(request, AcceptHostKey);
            _remoteDirectory = SftpSessionService.NormalizeRemote(request.Site.RemoteDirectory);
            _settings.Save();
            _disconnectButton.Enabled = true;
            SetConnectionBadge(connected: true, request.Site.Host);
            await RefreshRemoteAsync();
            SetStatus($"Connesso a {request.Site.Username}@{request.Site.Host}:{request.Site.Port} · {_session.ServerVersion}");
        }
        catch (Exception ex)
        {
            SetConnectionBadge(connected: false);
            SetStatus("Connessione non riuscita");
            MessageBox.Show(this, ex.Message, "Connessione SFTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            UpdateConnectionControls();
            HideProgress();
        }
    }

    private bool AcceptHostKey(string host, string fingerprint)
    {
        bool accepted = false;
        if (InvokeRequired)
        {
            Invoke(new Action(() => accepted = AskToTrustHostKey(host, fingerprint)));
        }
        else
        {
            accepted = AskToTrustHostKey(host, fingerprint);
        }

        if (accepted)
        {
            var requestKey = WinScpSettings.HostKeyId(host, CurrentPortFor(host));
            _settings.KnownHosts[requestKey] = fingerprint;
            _settings.Save();
        }
        return accepted;
    }

    private int CurrentPortFor(string host)
    {
        // Host fingerprints are indexed by host and port; the active login has already been saved in the profile list.
        if (_activeSite is not null && _activeSite.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
            return _activeSite.Port;
        var match = _settings.Sites.LastOrDefault(site => site.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
        return match?.Port ?? 22;
    }

    private bool AskToTrustHostKey(string host, string fingerprint)
    {
        string key = WinScpSettings.HostKeyId(host, CurrentPortFor(host));
        if (_settings.KnownHosts.TryGetValue(key, out var known) && known == fingerprint) return true;

        string message = known is null
            ? $"Il server {host} presenta questa chiave host SHA-256:\n\nSHA256:{fingerprint}\n\nAccetti la chiave e ti fidi di questo server?"
            : $"La chiave host di {host} è cambiata.\n\nRegistrata: SHA256:{known}\nRicevuta:   SHA256:{fingerprint}\n\nConferma solo se hai verificato la nuova impronta con l'amministratore del server.";
        return MessageBox.Show(this, message, "Verifica chiave host SFTP", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void Disconnect()
    {
        if (_busy) return;
        _session.Disconnect();
        _remoteDirectory = "/";
        _remotePath.Text = "";
        _remoteGrid.Rows.Clear();
        _disconnectButton.Enabled = false;
        SetConnectionBadge(connected: false);
        SetStatus("Disconnesso");
    }

    private async Task RefreshBothAsync()
    {
        await RefreshLocalAsync();
        if (_session.IsConnected) await RefreshRemoteAsync();
    }

    private async Task RefreshLocalAsync()
    {
        if (!Directory.Exists(_localDirectory)) _localDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _localPath.Text = _localDirectory;
        _localGrid.Rows.Clear();
        string? parent = Directory.GetParent(_localDirectory)?.FullName;
        if (parent is not null) AddLocalRow(new FileEntry("..", parent, true, 0, DateTime.MinValue), isParent: true);
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(_localDirectory).OrderBy(x => Path.GetFileName(x), StringComparer.CurrentCultureIgnoreCase))
            {
                var info = new DirectoryInfo(directory);
                AddLocalRow(new FileEntry(info.Name, info.FullName, true, 0, info.LastWriteTime));
            }
            foreach (string file in Directory.EnumerateFiles(_localDirectory).OrderBy(x => Path.GetFileName(x), StringComparer.CurrentCultureIgnoreCase))
            {
                var info = new FileInfo(file);
                AddLocalRow(new FileEntry(info.Name, info.FullName, false, info.Length, info.LastWriteTime));
            }
            SetStatus($"Cartella locale: {_localDirectory}");
        }
        catch (Exception ex)
        {
            SetStatus("Impossibile leggere la cartella locale");
            MessageBox.Show(this, ex.Message, "Cartella locale", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        await Task.CompletedTask;
    }

    private async Task RefreshRemoteAsync()
    {
        if (!_session.IsConnected) return;
        try
        {
            var entries = await _session.ListDirectoryAsync(_remoteDirectory);
            _remoteGrid.Rows.Clear();
            if (_remoteDirectory != "/")
                AddRemoteRow(new RemoteFileEntry("..", SftpSessionService.ParentRemote(_remoteDirectory), true, 0, DateTime.MinValue), isParent: true);
            foreach (var entry in entries) AddRemoteRow(entry);
            _remotePath.Text = _remoteDirectory;
            SetStatus($"Cartella remota: {_remoteDirectory} · {entries.Count} elementi");
        }
        catch (Exception ex)
        {
            SetStatus("Impossibile leggere la cartella remota");
            MessageBox.Show(this, ex.Message, "Cartella remota SFTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddLocalRow(FileEntry entry, bool isParent = false)
    {
        int index = _localGrid.Rows.Add(entry.IsDirectory ? "▰  " + entry.Name : "   " + entry.Name,
            entry.IsDirectory ? "" : FormatSize(entry.Length), entry.Modified == DateTime.MinValue ? "" : entry.Modified);
        _localGrid.Rows[index].Tag = entry;
        if (isParent) _localGrid.Rows[index].DefaultCellStyle.ForeColor = _palette.TextMuted;
    }

    private void AddRemoteRow(RemoteFileEntry entry, bool isParent = false)
    {
        int index = _remoteGrid.Rows.Add(entry.IsDirectory ? "▰  " + entry.Name : "   " + entry.Name,
            entry.IsDirectory ? "" : FormatSize(entry.Length), entry.Modified == DateTime.MinValue ? "" : entry.Modified);
        _remoteGrid.Rows[index].Tag = entry;
        if (isParent) _remoteGrid.Rows[index].DefaultCellStyle.ForeColor = _palette.TextMuted;
    }

    private void NavigateLocalFromBox()
    {
        string target = Environment.ExpandEnvironmentVariables(_localPath.Text.Trim().Trim('"'));
        NavigateLocal(target);
    }

    private void NavigateLocal(string path)
    {
        try
        {
            string target = Path.GetFullPath(path);
            if (!Directory.Exists(target)) throw new DirectoryNotFoundException($"La cartella non esiste: {target}");
            _localDirectory = target;
            _ = RefreshLocalAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cartella locale", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void NavigateRemoteFromBox() => NavigateRemote(_remotePath.Text);

    private void NavigateRemote(string path)
    {
        if (!_session.IsConnected) return;
        _remoteDirectory = SftpSessionService.NormalizeRemote(path);
        _ = RefreshRemoteAsync();
    }

    private async Task LocalDoubleClickAsync(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _localGrid.Rows.Count) return;
        if (_localGrid.Rows[rowIndex].Tag is not FileEntry entry) return;
        if (entry.IsDirectory) NavigateLocal(entry.FullPath);
        else
        {
            try { Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Apri file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        await Task.CompletedTask;
    }

    private async Task RemoteDoubleClickAsync(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _remoteGrid.Rows.Count) return;
        if (_remoteGrid.Rows[rowIndex].Tag is not RemoteFileEntry entry) return;
        if (entry.IsDirectory) NavigateRemote(entry.FullPath);
        else await TransferAsync(() => _session.DownloadAsync([entry], _localDirectory, ReportProgress), "Scaricamento completato");
    }

    private async Task UploadSelectedAsync()
    {
        if (!_session.IsConnected) { SetStatus("Connettiti a un server SFTP prima di caricare file."); return; }
        var selected = _localGrid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.Tag as FileEntry).Where(entry => entry is not null && entry.Name != "..")
            .Select(entry => entry!.FullPath).ToArray();
        if (selected.Length == 0) return;
        await TransferAsync(() => _session.UploadAsync(selected, _remoteDirectory, ReportProgress), "Caricamento completato");
        await RefreshBothAsync();
    }

    private async Task DownloadSelectedAsync()
    {
        if (!_session.IsConnected) { SetStatus("Connettiti a un server SFTP prima di scaricare file."); return; }
        var selected = _remoteGrid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.Tag as RemoteFileEntry).Where(entry => entry is not null && entry.Name != "..")
            .Cast<RemoteFileEntry>().ToArray();
        if (selected.Length == 0) return;
        await TransferAsync(() => _session.DownloadAsync(selected, _localDirectory, ReportProgress), "Scaricamento completato");
        await RefreshBothAsync();
    }

    private async Task TransferAsync(Func<Task> action, string successMessage)
    {
        if (_busy) return;
        _busy = true;
        UpdateConnectionControls();
        SetBusy("Trasferimento in corso…");
        try
        {
            await action();
            SetStatus(successMessage);
        }
        catch (Exception ex)
        {
            SetStatus("Trasferimento non riuscito");
            MessageBox.Show(this, ex.Message, "Trasferimento SFTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            UpdateConnectionControls();
            HideProgress();
        }
    }

    private async Task CreateFolderAsync()
    {
        using var dialog = new TextPromptDialog("Nuova cartella", "Nome cartella:");
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Value)) return;
        string name = dialog.Value.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
        {
            MessageBox.Show(this, "Il nome della cartella non è valido.", "Nuova cartella", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_activeRemotePane)
        {
            if (!_session.IsConnected) return;
            await TransferAsync(() => _session.CreateDirectoryAsync(SftpSessionService.CombineRemote(_remoteDirectory, name)), "Cartella remota creata");
            await RefreshRemoteAsync();
        }
        else
        {
            try { Directory.CreateDirectory(Path.Combine(_localDirectory, name)); await RefreshLocalAsync(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Nuova cartella", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private async Task RenameSelectedAsync()
    {
        if (_activeRemotePane)
        {
            if (!_session.IsConnected || _remoteGrid.SelectedRows.Count != 1 || _remoteGrid.SelectedRows[0].Tag is not RemoteFileEntry remote || remote.Name == "..") return;
            using var prompt = new TextPromptDialog("Rinomina elemento remoto", "Nuovo nome:", remote.Name);
            if (prompt.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(prompt.Value)) return;
            if (!IsSimpleName(prompt.Value)) { ShowInvalidName(); return; }
            string target = SftpSessionService.CombineRemote(SftpSessionService.ParentRemote(remote.FullPath), prompt.Value.Trim());
            await TransferAsync(() => _session.RenameAsync(remote.FullPath, target), "Elemento remoto rinominato");
            await RefreshRemoteAsync();
        }
        else
        {
            if (_localGrid.SelectedRows.Count != 1 || _localGrid.SelectedRows[0].Tag is not FileEntry local || local.Name == "..") return;
            using var prompt = new TextPromptDialog("Rinomina elemento locale", "Nuovo nome:", local.Name);
            if (prompt.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(prompt.Value)) return;
            if (!IsSimpleName(prompt.Value)) { ShowInvalidName(); return; }
            string target = Path.Combine(_localDirectory, prompt.Value.Trim());
            try
            {
                if (local.IsDirectory) Directory.Move(local.FullPath, target);
                else File.Move(local.FullPath, target);
                await RefreshLocalAsync();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Rinomina elemento", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_activeRemotePane)
        {
            if (!_session.IsConnected) return;
            var entries = _remoteGrid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag as RemoteFileEntry)
                .Where(entry => entry is not null && entry.Name != "..").Cast<RemoteFileEntry>().ToArray();
            if (entries.Length == 0 || !ConfirmDelete(entries.Length, "remoto")) return;
            await TransferAsync(async () => { foreach (var entry in entries) await _session.DeleteAsync(entry); }, "Elementi remoti eliminati");
            await RefreshRemoteAsync();
        }
        else
        {
            var entries = _localGrid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag as FileEntry)
                .Where(entry => entry is not null && entry.Name != "..").Cast<FileEntry>().ToArray();
            if (entries.Length == 0 || !ConfirmDelete(entries.Length, "locale")) return;
            try
            {
                foreach (var entry in entries)
                {
                    if (entry.IsDirectory) Directory.Delete(entry.FullPath, recursive: true);
                    else File.Delete(entry.FullPath);
                }
                await RefreshLocalAsync();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Elimina elementi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private bool ConfirmDelete(int count, string side) => MessageBox.Show(this,
        $"Vuoi eliminare {count} {(count == 1 ? "elemento" : "elementi")} {side}?",
        "Conferma eliminazione", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private static bool IsSimpleName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim() is not "." and not ".." &&
        !name.Contains('/') && !name.Contains('\\') && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private void ShowInvalidName() => MessageBox.Show(this,
        "Inserisci un nome semplice, senza separatori di percorso.", "Nome non valido", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ReportProgress(string file, ulong transferred)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() => _status.Text = $"{file} · {FormatSize(transferred > long.MaxValue ? long.MaxValue : (long)transferred)} trasferiti"));
    }

    private void SetBusy(string text)
    {
        SetStatus(text);
        _progress.Visible = true;
    }

    private void HideProgress() => _progress.Visible = false;
    private void SetStatus(string text) => _status.Text = text;

    private void UpdateConnectionControls()
    {
        _disconnectButton.Enabled = _session.IsConnected && !_busy;
        UseWaitCursor = _busy;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = Math.Max(0, bytes);
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{(long)size:N0} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }
}
