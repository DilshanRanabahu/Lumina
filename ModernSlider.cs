using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Lumina;

/// <summary>Windows 11 style slider (0-100) with an accent-filled track and round thumb.</summary>
sealed class ModernSlider : Control
{
    int value;
    bool dragging;
    bool hover;

    public ModernSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float UiScale { get; set; } = 1f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int v = Math.Clamp(value, 0, 100);
            if (v == this.value) return;
            this.value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    float ThumbRadius => 10 * UiScale;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cy = Height / 2f;
        float x0 = ThumbRadius, x1 = Width - ThumbRadius;
        float fill = x0 + (x1 - x0) * value / 100f;
        var accent = Enabled ? Theme.Accent : Theme.SubText;

        using (var track = new Pen(Theme.Track, 4 * UiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(track, x0, cy, x1, cy);
        if (fill > x0)
        {
            using var active = new Pen(accent, 4 * UiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(active, x0, cy, fill, cy);
        }

        float outer = ThumbRadius;
        float inner = (dragging ? 5f : hover ? 7f : 6f) * UiScale;
        using (var ring = new SolidBrush(Color.FromArgb(69, 69, 69)))
            g.FillEllipse(ring, fill - outer, cy - outer, outer * 2, outer * 2);
        using (var dot = new SolidBrush(accent))
            g.FillEllipse(dot, fill - inner, cy - inner, inner * 2, inner * 2);

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Theme.Text, 1);
            g.DrawEllipse(focus, fill - outer - 1, cy - outer - 1, outer * 2 + 2, outer * 2 + 2);
        }
    }

    void SetFromX(int x)
    {
        float x0 = ThumbRadius, x1 = Width - ThumbRadius;
        Value = (int)Math.Round((x - x0) / (x1 - x0) * 100);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        dragging = true;
        SetFromX(e.X);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta) * 2;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Value -= 1; break;
            case Keys.Right or Keys.Up: Value += 1; break;
            case Keys.PageDown: Value -= 10; break;
            case Keys.PageUp: Value += 10; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = 100; break;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
