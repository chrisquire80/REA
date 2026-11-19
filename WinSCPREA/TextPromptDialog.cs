using NoreaDesign;

namespace WinSCPREA;

internal sealed class TextPromptDialog : Form
{
    private readonly TextBox _text = new();
    private readonly NoreaPalette _palette = NoreaDesign.NoreaStyles.CurrentPalette;
    public string Value => _text.Text;

    public TextPromptDialog(string title, string prompt, string initialValue = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 166);
        NoreaDesign.NoreaStyles.ApplyForm(this, _palette);

        var label = new Label { Text = prompt, Left = 20, Top = 16, Width = 390, Height = 24 };
        NoreaDesign.NoreaStyles.StyleLabel(label, _palette, heading: true);
        _text.SetBounds(20, 48, 390, NoreaDesign.NoreaStyles.ControlHeight);
        NoreaDesign.NoreaStyles.StyleInput(_text, _palette);
        _text.Text = initialValue;
        var ok = new Button { Text = "Conferma", DialogResult = DialogResult.OK, Left = 210, Top = 104, Width = 100 };
        var cancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Left = 318, Top = 104, Width = 92 };
        NoreaDesign.NoreaStyles.StyleButton(ok, _palette, NoreaButtonStyle.Primary);
        NoreaDesign.NoreaStyles.StyleButton(cancel, _palette);
        Controls.AddRange([label, _text, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => { _text.Focus(); _text.SelectAll(); };
    }
}
