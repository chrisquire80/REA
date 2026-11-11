using System.Diagnostics;
using System.Text;
using ScintillaNET;

namespace NotepadREA;

public sealed class TerminalPanel : UserControl
{
    private Process? _process;
    private StreamWriter? _inputWriter;
    private readonly Scintilla _output = new() { Dock = DockStyle.Fill, BorderStyle = ScintillaNET.BorderStyle.None };
    private readonly TextBox _inputBox = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5f) };
    private readonly Label _promptLabel = new() { Text = " PS > ", AutoSize = true, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Panel _bottomBar = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(4, 2, 4, 2) };
    private readonly Panel _headerBar = new() { Dock = DockStyle.Top, Height = 26, Padding = new Padding(6, 3, 6, 3) };
    private readonly Label _titleLabel = new() { Text = "TERMINALE (PowerShell)", AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold) };
    private readonly FlowLayoutPanel _headerActions = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };

    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private Theme _theme = Theme.Dark;
    private string _currentDirectory = "";

    public event EventHandler? CloseRequested;

    public TerminalPanel()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        SetupUI();
    }

    private void SetupUI()
    {
        // Header
        var btnCd = MakeHeaderBtn("⮑ Cartella File", () => CdToCurrentFile());
        var btnClear = MakeHeaderBtn("⟲ Pulisci", () => ClearOutput());
        var btnRestart = MakeHeaderBtn("⚡ Riavvia Shell", () => RestartShell(_currentDirectory));
        var btnClose = MakeHeaderBtn("✕", () => CloseRequested?.Invoke(this, EventArgs.Empty));

        _headerActions.Controls.AddRange([btnCd, btnClear, btnRestart, btnClose]);
        _headerBar.Controls.Add(_titleLabel);
        _headerBar.Controls.Add(_headerActions);

        // Editor console
        _output.ReadOnly = true;
        _output.Margins[0].Width = 0;
        _output.Margins[1].Width = 0;
        _output.Margins[2].Width = 0;
        _output.CaretLineBackColor = Color.Transparent;
        _output.ScrollWidthTracking = true;
        _output.ScrollWidth = 1;

        // Input bar
        _inputBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _bottomBar.Controls.Add(_inputBox);
        _bottomBar.Controls.Add(_promptLabel);

        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ExecuteCommand(_inputBox.Text);
                _inputBox.Clear();
            }
            else if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                NavigateHistory(1);
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                NavigateHistory(-1);
            }
        };

        Controls.Add(_output);
        Controls.Add(_bottomBar);
        Controls.Add(_headerBar);
    }

    private Button MakeHeaderBtn(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 22,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 0, 0, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    public void FocusInput()
    {
        if (_inputBox.CanFocus) _inputBox.Focus();
    }

    public void SetWorkingDirectory(string dir)
    {
        if (Directory.Exists(dir) && !string.Equals(_currentDirectory, dir, StringComparison.OrdinalIgnoreCase))
        {
            _currentDirectory = dir;
            if (_process != null && !_process.HasExited)
            {
                _inputWriter?.WriteLine($"Set-Location -LiteralPath \"{dir}\"");
            }
        }
    }

    public void StartShell(string workingDir)
    {
        if (_process != null && !_process.HasExited) return;

        _currentDirectory = Directory.Exists(workingDir) ? workingDir : Directory.GetCurrentDirectory();

        try
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoLogo -NoExit -ExecutionPolicy Bypass")
            {
                WorkingDirectory = _currentDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) AppendOutput(e.Data + Environment.NewLine);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) AppendOutput("[ERR] " + e.Data + Environment.NewLine);
            };

            _process.Exited += (_, _) =>
            {
                AppendOutput(Environment.NewLine + "[Shell terminata]" + Environment.NewLine);
            };

            _process.Start();
            _inputWriter = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            AppendOutput($"NOREA PowerShell Terminal — Attivo in {_currentDirectory}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            AppendOutput($"[Errore avvio terminale]: {ex.Message}{Environment.NewLine}");
        }
    }

    public void ExecuteCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return;

        _history.Add(cmd);
        _historyIndex = _history.Count;

        if (cmd.Trim().Equals("cls", StringComparison.OrdinalIgnoreCase) || cmd.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearOutput();
            return;
        }

        AppendOutput($"PS {_currentDirectory}> {cmd}{Environment.NewLine}");

        try
        {
            if (_process != null && !_process.HasExited && _inputWriter != null)
            {
                _inputWriter.WriteLine(cmd);
            }
            else
            {
                StartShell(_currentDirectory);
                _inputWriter?.WriteLine(cmd);
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"[Errore esecuzione]: {ex.Message}{Environment.NewLine}");
        }
    }

    private void NavigateHistory(int dir)
    {
        if (_history.Count == 0) return;
        _historyIndex -= dir;
        if (_historyIndex < 0) _historyIndex = 0;
        if (_historyIndex >= _history.Count)
        {
            _historyIndex = _history.Count;
            _inputBox.Text = "";
            return;
        }
        _inputBox.Text = _history[_historyIndex];
        _inputBox.SelectionStart = _inputBox.Text.Length;
    }

    private void AppendOutput(string text)
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            BeginInvoke(() =>
            {
                _output.ReadOnly = false;
                _output.AppendText(text);
                _output.ReadOnly = true;
                _output.GotoPosition(_output.TextLength);
            });
        }
        catch { }
    }

    public void ClearOutput()
    {
        _output.ReadOnly = false;
        _output.Text = "";
        _output.ReadOnly = true;
    }

    public void CdToCurrentFile()
    {
        if (!string.IsNullOrEmpty(_currentDirectory) && Directory.Exists(_currentDirectory))
        {
            ExecuteCommand($"Set-Location -LiteralPath \"{_currentDirectory}\"");
        }
    }

    public void RestartShell(string? dir = null)
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        catch { }

        ClearOutput();
        StartShell(dir ?? _currentDirectory);
    }

    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.UiBack;
        _headerBar.BackColor = theme.UiBack;
        _titleLabel.ForeColor = theme.UiFore;
        _bottomBar.BackColor = theme.UiBack;
        _promptLabel.ForeColor = theme.Accent;

        _inputBox.BackColor = theme.Back;
        _inputBox.ForeColor = theme.Fore;

        foreach (Control c in _headerActions.Controls)
        {
            if (c is Button b)
            {
                b.BackColor = theme.UiBack;
                b.ForeColor = theme.UiFore;
            }
        }

        Languages.Apply(_output, Languages.Normal, theme, "Consolas", 9);
        _output.Styles[Style.Default].BackColor = theme.Back;
        _output.Styles[Style.Default].ForeColor = theme.Fore;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (_process != null && !_process.HasExited) _process.Kill();
                _process?.Dispose();
            }
            catch { }
        }
        base.Dispose(disposing);
    }
}
