using System.Text;
using ScintillaNET;

namespace NotepadREA;

public sealed class HexForm : Form
{
    private readonly Scintilla _sci = new() { Dock = DockStyle.Fill, BorderStyle = ScintillaNET.BorderStyle.None };
    private readonly Panel _topPanel = new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 6) };
    private readonly StatusStrip _status = new() { SizingGrip = false };
    private readonly ToolStripStatusLabel _lblSize = new() { Text = "Dimensione: 0 byte" };
    private readonly ToolStripStatusLabel _lblEntropy = new() { Text = "Entropia: 0.00 bits/byte" };
    private readonly ToolStripStatusLabel _lblAscii = new() { Text = "ASCII: 0%" };

    private readonly TextBox _tbSearch = new() { Width = 220, Font = new Font("Segoe UI", 9f) };
    private readonly Button _btnFindNext = new() { Text = "Trova", AutoSize = true };
    private readonly CheckBox _chkHexSearch = new() { Text = "Hex (es. 4D 5A)", AutoSize = true, Checked = true };
    private readonly Button _btnExport = new() { Text = "Esporta dump...", AutoSize = true };

    private byte[] _bytes = [];
    private readonly Theme _theme;

    public HexForm(string title, byte[] data, Theme theme)
    {
        _bytes = data;
        _theme = theme;

        Text = $"Ispezione Esadecimale (Hex View) — {title}";
        Size = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterParent;
        Icon = SystemIcons.Application;

        SetupUI();
        ApplyTheme();
        RenderHex();
        ComputeStatistics();
    }

    public static HexForm FromFile(string filePath, Theme theme)
    {
        byte[] data = File.Exists(filePath) ? File.ReadAllBytes(filePath) : [];
        return new HexForm(Path.GetFileName(filePath), data, theme);
    }

    private void SetupUI()
    {
        // Top search & action bar
        var lblFind = new Label { Text = "Cerca:", AutoSize = true, Margin = new Padding(0, 4, 4, 0) };
        _btnFindNext.Click += (_, _) => FindNext();
        _tbSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; FindNext(); } };

        _btnExport.Click += (_, _) => ExportDump();

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        flow.Controls.AddRange([lblFind, _tbSearch, _chkHexSearch, _btnFindNext, _btnExport]);
        _topPanel.Controls.Add(flow);

        // Status bar
        _status.Items.AddRange([_lblSize, new ToolStripSeparator(), _lblEntropy, new ToolStripSeparator(), _lblAscii]);

        // Scintilla viewer
        _sci.ReadOnly = true;
        _sci.Margins[0].Type = MarginType.Number;
        _sci.Margins[0].Width = 0;
        _sci.Margins[1].Width = 0;
        _sci.CaretLineVisibleAlways = true;
        _sci.ScrollWidthTracking = true;

        Controls.Add(_sci);
        Controls.Add(_topPanel);
        Controls.Add(_status);
    }

    private void RenderHex()
    {
        if (_bytes.Length == 0)
        {
            _sci.ReadOnly = false;
            _sci.Text = "(File vuoto o privo di dati binari)";
            _sci.ReadOnly = true;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("OFFSET     00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F   DECODIFICA ASCII");
        sb.AppendLine("--------------------------------------------------------------------------------");

        int maxLen = Math.Min(_bytes.Length, 1024 * 1024 * 4); // Limite rendering a 4MB per fluidità immediata

        for (int i = 0; i < maxLen; i += 16)
        {
            // Offset
            sb.AppendFormat("{0:X8}   ", i);

            // Byte Hex
            for (int j = 0; j < 16; j++)
            {
                if (i + j < maxLen)
                    sb.AppendFormat("{0:X2} ", _bytes[i + j]);
                else
                    sb.Append("   ");

                if (j == 7) sb.Append(" ");
            }

            sb.Append("  |");

            // ASCII printable
            for (int j = 0; j < 16 && (i + j) < maxLen; j++)
            {
                byte b = _bytes[i + j];
                char c = (b >= 32 && b <= 126) ? (char)b : '.';
                sb.Append(c);
            }

            sb.AppendLine("|");
        }

        if (_bytes.Length > maxLen)
        {
            sb.AppendLine();
            sb.AppendLine($"... [Troncato per visualizzazione rapida: visualizzati {maxLen / 1024} KB su {_bytes.Length / 1024} KB totali]");
        }

        _sci.ReadOnly = false;
        _sci.Text = sb.ToString();
        _sci.ReadOnly = true;
    }

    private void ComputeStatistics()
    {
        if (_bytes.Length == 0) return;

        _lblSize.Text = $"Dimensione: {_bytes.Length:N0} byte ({_bytes.Length / 1024.0:F1} KB)";

        // Shannon Entropy: -sum(p * log2(p))
        int[] freq = new int[256];
        int asciiCount = 0;
        int nullCount = 0;

        foreach (byte b in _bytes)
        {
            freq[b]++;
            if (b >= 32 && b <= 126) asciiCount++;
            if (b == 0) nullCount++;
        }

        double entropy = 0.0;
        double len = _bytes.Length;

        for (int i = 0; i < 256; i++)
        {
            if (freq[i] > 0)
            {
                double p = freq[i] / len;
                entropy -= p * Math.Log2(p);
            }
        }

        string entropyEval = entropy > 7.2 ? " (Alta / Compresso o Cifrato)" : " (Bassa / Strutturato)";
        _lblEntropy.Text = $"Entropia: {entropy:F2} bits/byte{entropyEval}";

        double asciiPct = (asciiCount / len) * 100.0;
        double nullPct = (nullCount / len) * 100.0;
        _lblAscii.Text = $"ASCII: {asciiPct:F1}%   Null (0x00): {nullPct:F1}%";
    }

    private void FindNext()
    {
        string term = _tbSearch.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;

        int startPos = _sci.CurrentPosition + 1;
        if (startPos >= _sci.TextLength) startPos = 0;

        _sci.TargetStart = startPos;
        _sci.TargetEnd = _sci.TextLength;
        _sci.SearchFlags = SearchFlags.None;

        int pos = _sci.SearchInTarget(term);
        if (pos == -1 && startPos > 0)
        {
            _sci.TargetStart = 0;
            _sci.TargetEnd = startPos;
            pos = _sci.SearchInTarget(term);
        }

        if (pos != -1)
        {
            _sci.SetSelection(pos + term.Length, pos);
            _sci.ScrollCaret();
        }
        else
        {
            MessageBox.Show(this, $"Sequenza '{term}' non trovata nel dump esadecimale.", "Ricerca Hex", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportDump()
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Esporta dump esadecimale",
            Filter = "File di testo (*.txt)|*.txt|Tutti i file (*.*)|*.*",
            FileName = "hexdump.txt"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(sfd.FileName, _sci.Text, Encoding.UTF8);
            MessageBox.Show(this, "Dump esadecimale esportato con successo!", "Esportazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ApplyTheme()
    {
        BackColor = _theme.UiBack;
        _topPanel.BackColor = _theme.UiBack;
        _status.BackColor = _theme.UiBack;
        _status.ForeColor = _theme.UiFore;

        _tbSearch.BackColor = _theme.Back;
        _tbSearch.ForeColor = _theme.Fore;

        Button[] buttons = [_btnFindNext, _btnExport];
        foreach (var btn in buttons)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = _theme.UiBorder;
            btn.BackColor = _theme.UiBack;
            btn.ForeColor = _theme.UiFore;
        }

        Languages.Apply(_sci, Languages.Normal, _theme, "Consolas", 10);
    }
}
