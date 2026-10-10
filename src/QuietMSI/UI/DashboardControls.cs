using System.Drawing.Drawing2D;
using QuietMSI.Core;

namespace QuietMSI.UI;

// Suppress native focus reveal only during synchronous programmatic updates.
// Never hold this scope across an await: Tab navigation must reveal its target.
internal class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
}

internal sealed class BufferedLabel : Label
{
    public BufferedLabel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
}

internal sealed class DashboardViewport : BufferedPanel
{
    private int _updateDepth;
    public IDisposable PreserveViewport() => new UpdateScope(this);
    protected override Point ScrollToControl(Control activeControl)
    {
        if (_updateDepth > 0) return DisplayRectangle.Location;
        // NumericUpDown focuses its inner edit; reveal the whole native editor, not only that inset child.
        if (activeControl.Parent is NumericUpDown editor) activeControl = editor;
        return base.ScrollToControl(activeControl);
    }
    private sealed class UpdateScope : IDisposable
    {
        private readonly DashboardViewport _owner;
        private readonly Point _position;
        public UpdateScope(DashboardViewport owner) { _owner = owner; _position = owner.AutoScrollPosition; owner._updateDepth++; }
        public void Dispose()
        {
            try { if (_owner.AutoScrollPosition != _position) _owner.AutoScrollPosition = new Point(-_position.X, -_position.Y); }
            finally { _owner._updateDepth--; }
        }
    }
}

internal sealed class RoundedCard : Panel
{
    public Theme Theme { get; }
    public RoundedCard(Theme theme)
    {
        Theme = theme;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Padding = new Padding(18);
        BackColor = theme.Surface;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shapes.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 14);
        using var fill = new SolidBrush(Theme.Surface);
        using var border = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, path);
        if (SystemInformation.HighContrast) e.Graphics.DrawPath(border, path);
    }
}

internal static class Shapes
{
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); var d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

internal sealed class DashboardButton : Button
{
    public Theme Theme { get; }
    public bool Primary { get; set; }
    public bool Selected { get; set; }
    public bool Navigation { get; set; }
    private bool _hover;
    public DashboardButton(Theme theme)
    {
        Theme = theme; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        DoubleBuffered = true; Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        // Clear rounded corners ourselves; Button's native background painter leaves
        // focus/hover fragments outside the rounded path when its state changes.
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = Primary && Enabled ? Theme.Accent : Selected || _hover ? Theme.Selected : Navigation ? Theme.Surface : Theme.ControlSurface;
        var ink = !Enabled ? Theme.Muted : Primary ? Theme.AccentInk : Selected ? Theme.Accent : Theme.Ink;
        using var path = Shapes.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 8);
        using var brush = new SolidBrush(fill); using var pen = new Pen(Navigation ? fill : Theme.Border);
        e.Graphics.FillPath(brush, path); if (SystemInformation.HighContrast) e.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Navigation && Selected) { using var line = new Pen(Theme.Accent, 3); e.Graphics.DrawLine(line, 12, Height - 2, Width - 12, Height - 2); }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ink, fill);
    }
}

internal sealed class FanCurveChart : Control
{
    private readonly Theme _theme;
    private int[]? _points, _targets;
    public FanCurveChart(Theme theme) { _theme = theme; DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); TabStop = false; AccessibleName = "Current CPU fan curve, read-only; actual temperature axis"; }
    public void SetData(HardwareControlState? state)
    {
        static bool Same(int[]? old, IReadOnlyList<int>? next) => old is null ? next is null : next is not null && old.SequenceEqual(next);
        if (Same(_points, state?.CpuBreakpoints) && Same(_targets, state?.CpuCurve)) return;
        _points = state?.CpuBreakpoints?.ToArray(); _targets = state?.CpuCurve?.ToArray();
        AccessibleDescription = _points is { Length: 6 } && _targets is { Length: 6 }
            ? string.Join(" · ", _points.Zip(_targets, (temperature, target) => $"{temperature}°C → {target}%"))
            : "Current CPU curve unavailable";
        Invalidate();
    }
    private RectangleF PlotBounds => new(39, 12, Math.Max(1, Width - 72), Math.Max(1, Height - 76));
    // A six-column companion table keeps every exact point readable without inventing a categorical axis.
    internal IReadOnlyList<Rectangle> TemperatureAnnotationBounds()
    {
        if (_points is not { Length: 6 }) return Array.Empty<Rectangle>();
        return _points.Select((p, i) => new Rectangle(i * Width / 6, Height - 44, Width / 6, 22)).ToArray();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_points is not { Length: 6 } || _targets is not { Length: 6 } || _points.Where((p, i) => p < 0 || p > 100 || (i > 0 && p <= _points[i - 1])).Any() || _targets.Any(p => p < 0 || p > 100))
        { TextRenderer.DrawText(g, "Current curve unavailable · waiting for a complete six-point table", _theme.Body, ClientRectangle, _theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter); return; }
        var plot = PlotBounds;
        using var grid = new Pen(_theme.Border); using var ink = new SolidBrush(_theme.Muted);
        for (var percent = 0; percent <= 100; percent += 20)
        { var y = plot.Bottom - plot.Height * percent / 100; g.DrawLine(grid, plot.Left, y, plot.Right, y); g.DrawString(percent + "%", _theme.Small, ink, 0, y - 7); }
        var start = _points[0]; var end = Math.Max(start + 1, _points[5]);
        var vertices = _points.Select((p, i) => new PointF(plot.Left + (p - start) * plot.Width / (end - start), plot.Bottom - _targets[i] * plot.Height / 100)).ToArray();
        using var area = new GraphicsPath(); area.AddLines(vertices); area.AddLine(vertices[^1], new PointF(plot.Right, plot.Bottom)); area.AddLine(new PointF(plot.Right, plot.Bottom), new PointF(plot.Left, plot.Bottom)); area.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb(24, _theme.Accent)); g.FillPath(fill, area);
        using var line = new Pen(_theme.Accent, 2.5f); using var dot = new SolidBrush(_theme.Accent); g.DrawLines(line, vertices);
        var annotations = TemperatureAnnotationBounds();
        for (var i = 0; i < 6; i++)
        {
            g.FillEllipse(dot, vertices[i].X - 3, vertices[i].Y - 3, 6, 6);
            // Companion table, not tick labels: plotted vertices retain the actual temperature axis.
            TextRenderer.DrawText(g, _points[i] + "°C", _theme.Small, annotations[i], _theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter);
            var targetBounds = annotations[i]; targetBounds.Y += 22;
            TextRenderer.DrawText(g, _targets[i] + "%", _theme.Small, targetBounds, _theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter);
        }
    }
}
