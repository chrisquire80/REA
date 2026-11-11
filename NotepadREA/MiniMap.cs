using System.Runtime.InteropServices;
using ScintillaNET;

namespace NotepadREA;

/// <summary>
/// Minimappa laterale ("Document Map"): una seconda vista Scintilla che condivide il documento dell'editor attivo,
/// rimpicciolita, con l'area visibile evidenziata. Clic/trascinamento/rotella fanno scorrere l'editor principale.
/// </summary>
public sealed class MiniMap : Scintilla
{
    const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_LBUTTONDBLCLK = 0x203,
        WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207, WM_MOUSEWHEEL = 0x20A, WM_SETFOCUS = 0x7;

    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

    Scintilla? _target;
    bool _dragging;
    Theme _theme = Theme.Light;

    public MiniMap()
    {
        Dock = DockStyle.Right;
        Width = 130;
        TabStop = false;
        BorderStyle = ScintillaNET.BorderStyle.None;
        HScrollBar = false;
        VScrollBar = false;
        CaretStyle = CaretStyle.Invisible;
        WrapMode = WrapMode.None;
        for (int i = 0; i < 5; i++) Margins[i].Width = 0;
        Margins.Left = 2;
        SelectionLayer = Layer.OverText;
        Cursor = Cursors.Hand;
    }

    public Scintilla? Target => _target;

    public void Attach(NotepadREA.Document? doc, Theme theme, string font)
    {
        if (_target != null) _target.UpdateUI -= OnTargetUpdate;
        _target = doc?.Editor;
        _theme = theme;
        if (doc is null || _target is null) return;

        Document = doc.Editor.Document; // stessa memoria di testo: nessuna copia
        Languages.Apply(this, doc.Language, theme, font, 10);
        CaretLineBackColor = Color.Transparent;
        Zoom = -8;
        Indicators[NotepadREA.Document.SmartIndicator].Style = IndicatorStyle.FullBox;
        Indicators[NotepadREA.Document.SmartIndicator].ForeColor = theme.SmartHighlight;
        Indicators[NotepadREA.Document.SmartIndicator].Alpha = 160;
        Indicators[FindPanel.MarkIndicator].Style = IndicatorStyle.FullBox;
        Indicators[FindPanel.MarkIndicator].ForeColor = Color.FromArgb(255, 200, 0);
        Indicators[FindPanel.MarkIndicator].Alpha = 200;
        var view = Color.FromArgb(theme.IsDark ? 55 : 45, theme.IsDark ? Color.White : theme.Accent);
        SelectionBackColor = view;
        SelectionInactiveBackColor = view;
        _target.UpdateUI += OnTargetUpdate;
        Sync();
    }

    void OnTargetUpdate(object? s, UpdateUIEventArgs e) => Sync();

    /// <summary>Allinea la minimappa alla porzione visibile dell'editor principale.</summary>
    public void Sync()
    {
        if (_target is null || !Visible) return;
        int firstDoc = _target.DocLineFromVisible(_target.FirstVisibleLine);
        int lastDoc = _target.DocLineFromVisible(_target.FirstVisibleLine + _target.LinesOnScreen - 1);
        lastDoc = Math.Min(lastDoc, Lines.Count - 1);
        if (firstDoc < 0 || lastDoc < 0) return;

        int total = Lines.Count, miniOn = Math.Max(1, LinesOnScreen), span = lastDoc - firstDoc + 1;
        int miniFirst = 0;
        if (total > miniOn)
        {
            double ratio = total - span <= 0 ? 0 : (double)firstDoc / (total - span);
            miniFirst = (int)Math.Round(ratio * (total - miniOn));
        }
        FirstVisibleLine = Math.Max(0, miniFirst);
        SetSelection(Lines[lastDoc].EndPosition, Lines[firstDoc].Position);
    }

    void ScrollTargetTo(int y)
    {
        if (_target is null) return;
        int pos = CharPositionFromPoint(10, Math.Clamp(y, 0, Height - 1));
        int line = LineFromPosition(pos);
        int display = _target.Lines[line].DisplayIndex;
        _target.FirstVisibleLine = Math.Max(0, display - _target.LinesOnScreen / 2);
        Sync();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_LBUTTONDOWN:
                _dragging = true;
                Capture = true;
                ScrollTargetTo(Y(m.LParam));
                return;
            case WM_MOUSEMOVE:
                if (_dragging) ScrollTargetTo(Y(m.LParam));
                return;
            case WM_LBUTTONUP:
                _dragging = false;
                Capture = false;
                _target?.Focus();
                return;
            case WM_MOUSEWHEEL:
                if (_target != null) SendMessage(_target.Handle, m.Msg, m.WParam, m.LParam);
                return;
            case WM_LBUTTONDBLCLK or WM_RBUTTONDOWN or WM_RBUTTONUP or WM_MBUTTONDOWN:
                return;
            case WM_SETFOCUS:
                _target?.Focus();
                return;
        }
        base.WndProc(ref m);
    }

    static int Y(IntPtr lParam) => (short)(((long)lParam >> 16) & 0xFFFF);
}
