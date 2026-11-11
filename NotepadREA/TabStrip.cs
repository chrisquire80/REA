using System.Drawing.Drawing2D;

namespace NotepadREA;

/// <summary>
/// Barra delle schede disegnata a mano: pulsante × su ogni scheda, pallino per i documenti modificati,
/// riordino con trascinamento, chiusura con clic centrale, scorrimento con la rotella, colori del tema.
/// </summary>
public sealed class TabStrip : Control
{
    readonly List<Document> _docs = [];
    readonly List<Rectangle> _rects = [];
    readonly ToolTip _tip = new();
    Theme _theme = Theme.Light;
    int _selected = -1, _hover = -1, _scroll, _dragIndex = -1;
    bool _hoverClose, _dragging;
    Point _dragStart;
    string? _lastTip;

    public event EventHandler? SelectedChanged;
    public event EventHandler<Document>? CloseRequested;
    public event EventHandler<(Document Doc, Point Location)>? ContextRequested;
    public event EventHandler? NewRequested;
    public event EventHandler? OrderChanged;

    public TabStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Top;
        Font = new Font("Segoe UI", 9f);
        Height = Font.Height + 14;
    }

    public IReadOnlyList<Document> Documents => _docs;
    public Document? Selected => _selected >= 0 && _selected < _docs.Count ? _docs[_selected] : null;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            value = _docs.Count == 0 ? -1 : Math.Clamp(value, 0, _docs.Count - 1);
            if (value == _selected) return;
            _selected = value;
            EnsureVisible(value);
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetTheme(Theme t) { _theme = t; Invalidate(); }

    public void Add(Document d)
    {
        _docs.Add(d);
        d.StateChanged += OnDocStateChanged;
        Invalidate();
    }

    public void Remove(Document d)
    {
        int i = _docs.IndexOf(d);
        if (i < 0) return;
        d.StateChanged -= OnDocStateChanged;
        _docs.RemoveAt(i);
        if (_docs.Count == 0) _selected = -1;
        else if (i < _selected || _selected >= _docs.Count) _selected = Math.Max(0, _selected - 1);
        else if (i == _selected) _selected = Math.Min(i, _docs.Count - 1);
        _hover = -1;
        Invalidate();
        SelectedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(Document d) => SelectedIndex = _docs.IndexOf(d);

    void OnDocStateChanged(object? s, EventArgs e) => Invalidate();

    // ---------- Layout ----------
    const int Pad = 12, CloseSize = 16, MinW = 90, MaxW = 240;

    void Layout_()
    {
        _rects.Clear();
        int x = -_scroll;
        foreach (var d in _docs)
        {
            int w = TextRenderer.MeasureText(d.DisplayTitle, Font).Width + Pad * 2 + CloseSize + 4;
            w = Math.Clamp(w, MinW, MaxW);
            _rects.Add(new Rectangle(x, 0, w, Height - 1));
            x += w;
        }
    }

    int TotalWidth => _rects.Count == 0 ? 0 : _rects[^1].Right + _scroll;

    Rectangle CloseRect(Rectangle r) => new(r.Right - Pad / 2 - CloseSize, r.Y + (r.Height - CloseSize) / 2, CloseSize, CloseSize);

    void EnsureVisible(int i)
    {
        Layout_();
        if (i < 0 || i >= _rects.Count) return;
        var r = _rects[i];
        if (r.Left < 0) _scroll += r.Left;
        else if (r.Right > Width) _scroll += r.Right - Width;
        _scroll = Math.Max(0, _scroll);
    }

    int HitTest(Point p)
    {
        for (int i = 0; i < _rects.Count; i++) if (_rects[i].Contains(p)) return i;
        return -1;
    }

    // ---------- Disegno ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        Layout_();
        var g = e.Graphics;
        g.Clear(_theme.UiBack);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(_theme.UiBorder);

        for (int i = 0; i < _docs.Count; i++)
        {
            var r = _rects[i];
            if (r.Right < 0 || r.Left > Width) continue;
            var d = _docs[i];
            bool sel = i == _selected, hov = i == _hover;
            using (var bg = new SolidBrush(sel ? _theme.TabActive : hov ? _theme.UiHover : _theme.TabInactive)) g.FillRectangle(bg, r);
            g.DrawLine(border, r.Right - 1, r.Top + 4, r.Right - 1, r.Bottom - 4);
            if (sel) using (var acc = new SolidBrush(_theme.Accent)) g.FillRectangle(acc, r.X, r.Y, r.Width, 2);

            var textRect = new Rectangle(r.X + Pad, r.Y, r.Width - Pad * 2 - CloseSize, r.Height);
            var fore = sel ? _theme.UiFore : Color.FromArgb(200, _theme.UiFore);
            if (d.IsReadOnly) fore = Color.FromArgb(150, _theme.UiFore);
            TextRenderer.DrawText(g, d.DisplayTitle, Font, textRect, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            var cr = CloseRect(r);
            bool showClose = sel || hov;
            if (d.IsModified && !(hov && _hoverClose))
            {
                // Pallino "modificato"
                using var dot = new SolidBrush(_theme.Dirty);
                g.FillEllipse(dot, cr.X + 4, cr.Y + 4, cr.Width - 8, cr.Height - 8);
            }
            else if (showClose)
            {
                if (hov && _hoverClose) using (var hb = new SolidBrush(Color.FromArgb(60, _theme.UiFore))) g.FillRectangle(hb, cr);
                using var pen = new Pen(fore, 1.4f);
                int m = 5;
                g.DrawLine(pen, cr.Left + m, cr.Top + m, cr.Right - m, cr.Bottom - m);
                g.DrawLine(pen, cr.Right - m, cr.Top + m, cr.Left + m, cr.Bottom - m);
            }
        }
        g.DrawLine(border, 0, Height - 1, Width, Height - 1);
        if (_selected >= 0 && _selected < _rects.Count)
            using (var p = new Pen(_theme.TabActive)) g.DrawLine(p, _rects[_selected].Left, Height - 1, _rects[_selected].Right - 2, Height - 1);
    }

    // ---------- Mouse ----------
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragIndex >= 0 && e.Button == MouseButtons.Left)
        {
            if (!_dragging && Math.Abs(e.X - _dragStart.X) > 6) _dragging = true;
            if (_dragging)
            {
                int target = HitTest(new Point(e.X, Height / 2));
                if (target >= 0 && target != _dragIndex)
                {
                    var d = _docs[_dragIndex];
                    _docs.RemoveAt(_dragIndex);
                    _docs.Insert(target, d);
                    _selected = target;
                    _dragIndex = target;
                    Invalidate();
                    OrderChanged?.Invoke(this, EventArgs.Empty);
                }
                Cursor = Cursors.SizeWE;
                return;
            }
        }
        int h = HitTest(e.Location);
        bool hc = h >= 0 && CloseRect(_rects[h]).Contains(e.Location);
        if (h != _hover || hc != _hoverClose) { _hover = h; _hoverClose = hc; Invalidate(); }
        var tip = h >= 0 ? _docs[h].ToolTip : null;
        if (tip != _lastTip) { _lastTip = tip; _tip.SetToolTip(this, tip); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1; _hoverClose = false; Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int h = HitTest(e.Location);
        if (h < 0) return;
        if (e.Button == MouseButtons.Left)
        {
            if (CloseRect(_rects[h]).Contains(e.Location)) return; // gestito al rilascio
            SelectedIndex = h;
            _dragIndex = h;
            _dragStart = e.Location;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool wasDragging = _dragging;
        _dragIndex = -1; _dragging = false; Cursor = Cursors.Default;
        if (wasDragging) return;
        int h = HitTest(e.Location);
        if (h < 0) return;
        var d = _docs[h];
        if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && CloseRect(_rects[h]).Contains(e.Location)))
            CloseRequested?.Invoke(this, d);
        else if (e.Button == MouseButtons.Right)
        {
            SelectedIndex = h;
            ContextRequested?.Invoke(this, (d, e.Location));
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (HitTest(e.Location) < 0) NewRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Layout_();
        int max = Math.Max(0, TotalWidth - Width);
        _scroll = Math.Clamp(_scroll - e.Delta / 2, 0, max);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
