using NoreaDesign;

namespace WinSCPREA;

internal sealed class SiteDialog : Form
{
    private readonly WinScpSettings _settings;
    private readonly ComboBox _savedSites = new();
    private readonly TextBox _name = new();
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new();
    private readonly TextBox _username = new();
    private readonly TextBox _remoteDirectory = new();
    private readonly ComboBox _authentication = new();
    private readonly TextBox _password = new();
    private readonly TextBox _keyPath = new();
    private readonly TextBox _keyPassphrase = new();
    private readonly CheckBox _saveSite = new();
    private readonly NoreaPalette _palette = NoreaDesign.NoreaStyles.CurrentPalette;

    public ConnectionRequest? Request { get; private set; }

    public SiteDialog(WinScpSettings settings)
    {
        _settings = settings;
        Text = "Nuova connessione SFTP";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 570);
        NoreaDesign.NoreaStyles.ApplyForm(this, _palette);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 11 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 9; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _savedSites.DropDownStyle = ComboBoxStyle.DropDownList;
        _savedSites.Items.Add("— Nuovo sito —");
        foreach (var site in _settings.Sites.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            _savedSites.Items.Add(site.Name);
        _savedSites.SelectedIndex = 0;
        _savedSites.SelectedIndexChanged += (_, _) => LoadSelectedSite();

        _name.PlaceholderText = "Es. Server produzione";
        _host.PlaceholderText = "sftp.esempio.it";
        _port.Minimum = 1;
        _port.Maximum = 65535;
        _port.Value = 22;
        _username.PlaceholderText = "Nome utente";
        _remoteDirectory.Text = "/";
        _authentication.DropDownStyle = ComboBoxStyle.DropDownList;
        _authentication.Items.AddRange(["Password", "Chiave privata SSH"]);
        _authentication.SelectedIndex = 0;
        _authentication.SelectedIndexChanged += (_, _) => UpdateAuthenticationFields();
        _password.UseSystemPasswordChar = true;
        _password.PlaceholderText = "La password resta solo in memoria";
        _keyPath.PlaceholderText = "Percorso file chiave privata";
        _keyPassphrase.UseSystemPasswordChar = true;
        _keyPassphrase.PlaceholderText = "Passphrase della chiave (se richiesta)";
        _saveSite.Text = "Salva questo sito (senza password)";
        _saveSite.AutoSize = true;

        AddRow(root, 0, "Sito salvato", _savedSites);
        AddRow(root, 1, "Nome sito", _name);
        AddRow(root, 2, "Protocollo", MakeReadOnlyValue("SFTP (SSH File Transfer Protocol)"));
        AddRow(root, 3, "Server", _host);
        AddRow(root, 4, "Porta", _port);
        AddRow(root, 5, "Nome utente", _username);
        AddRow(root, 6, "Cartella remota", _remoteDirectory);
        AddRow(root, 7, "Autenticazione", _authentication);
        AddRow(root, 8, "Password", _password);

        var keyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        keyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        keyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        keyPanel.Controls.Add(_keyPath, 0, 0);
        var browse = new Button { Text = "Sfoglia…", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 0, 3) };
        NoreaDesign.NoreaStyles.StyleButton(browse, _palette);
        browse.Click += (_, _) => BrowseKey();
        keyPanel.Controls.Add(browse, 1, 0);
        keyPanel.Controls.Add(_keyPassphrase, 0, 1);
        keyPanel.SetColumnSpan(_keyPassphrase, 2);
        AddRow(root, 9, "Chiave SSH", keyPanel);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        footer.Controls.Add(_saveSite, 0, 0);
        footer.SetColumnSpan(_saveSite, 2);
        var cancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right, Width = 100 };
        var connect = new Button { Text = "Connetti", Anchor = AnchorStyles.Right, Width = 100 };
        NoreaDesign.NoreaStyles.StyleButton(cancel, _palette);
        NoreaDesign.NoreaStyles.StyleButton(connect, _palette, NoreaButtonStyle.Primary);
        connect.Click += (_, _) => ValidateAndAccept();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(connect);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(buttons, 0, 1);
        footer.SetColumnSpan(buttons, 2);
        AddRow(root, 10, "", footer);

        AcceptButton = connect;
        CancelButton = cancel;
        foreach (var input in new Control[] { _savedSites, _name, _host, _port, _username, _remoteDirectory, _authentication, _password, _keyPath, _keyPassphrase })
            NoreaDesign.NoreaStyles.StyleInput(input, _palette, monospace: input == _host || input == _remoteDirectory || input == _keyPath);
        _saveSite.ForeColor = _palette.TextSecondary;
        _saveSite.BackColor = Color.Transparent;
        _saveSite.Font = NoreaDesign.NoreaStyles.CreateUiFont();
        UpdateAuthenticationFields();
    }

    private static Control MakeReadOnlyValue(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = NoreaDesign.NoreaStyles.CurrentPalette.TextMuted
    };

    private void AddRow(TableLayoutPanel root, int row, string label, Control control)
    {
        var caption = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        NoreaDesign.NoreaStyles.StyleLabel(caption, _palette);
        root.Controls.Add(caption, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 3, 0, 3);
        root.Controls.Add(control, 1, row);
    }

    private void LoadSelectedSite()
    {
        if (_savedSites.SelectedIndex <= 0) return;
        var site = _settings.Sites.FirstOrDefault(x => x.Name == _savedSites.SelectedItem?.ToString());
        if (site is null) return;
        _name.Text = site.Name;
        _host.Text = site.Host;
        _port.Value = Math.Clamp(site.Port, 1, 65535);
        _username.Text = site.Username;
        _remoteDirectory.Text = string.IsNullOrWhiteSpace(site.RemoteDirectory) ? "/" : site.RemoteDirectory;
        _authentication.SelectedIndex = site.Authentication.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _keyPath.Text = site.PrivateKeyPath;
        _password.Clear();
        _keyPassphrase.Clear();
        _saveSite.Checked = true;
        UpdateAuthenticationFields();
    }

    private void UpdateAuthenticationFields()
    {
        bool useKey = _authentication.SelectedIndex == 1;
        _password.Enabled = !useKey;
        _keyPath.Enabled = useKey;
        _keyPassphrase.Enabled = useKey;
    }

    private void BrowseKey()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Seleziona la chiave privata SSH",
            Filter = "File chiave privata|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) == DialogResult.OK) _keyPath.Text = picker.FileName;
    }

    private void ValidateAndAccept()
    {
        bool useKey = _authentication.SelectedIndex == 1;
        var validation = SftpConnectionFlow.Validate(
            _name.Text,
            _host.Text,
            decimal.ToInt32(_port.Value),
            _username.Text,
            _remoteDirectory.Text,
            useKey,
            _password.Text,
            _keyPath.Text,
            _keyPassphrase.Text);
        if (!validation.Accepted || validation.Request is null)
        {
            MessageBox.Show(this, validation.ErrorMessage ?? "", validation.ErrorTitle ?? "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_saveSite.Checked)
        {
            SftpConnectionFlow.UpsertSavedSite(_settings.Sites, validation.Request.Site);
            _settings.Save();
        }

        Request = validation.Request;
        DialogResult = DialogResult.OK;
        Close();
    }
}
