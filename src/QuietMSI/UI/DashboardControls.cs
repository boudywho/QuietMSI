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

internal sealed class DashboardCard : BufferedPanel
{
    private readonly Theme _theme;
    public DashboardCard(Theme theme)
    {
        _theme = theme;
        Padding = new Padding(16);
        BackColor = theme.Surface;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width > 1 && Height > 1)
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
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
        var highlighted = Enabled && (Selected || _hover);
        var fill = Primary && Enabled ? Theme.Accent : highlighted ? Theme.Selected : Navigation ? Theme.Surface : Theme.ControlSurface;
        var ink = !Enabled ? (SystemInformation.HighContrast ? SystemColors.GrayText : Theme.Muted)
            : Primary ? Theme.AccentInk : highlighted ? Theme.SelectedInk : Theme.Ink;
        e.Graphics.Clear(fill);
        if (!Navigation && (!Primary || !Enabled) && Width > 1 && Height > 1)
        {
            using var border = new Pen(Selected && Enabled && !SystemInformation.HighContrast ? Theme.Accent : Theme.Border);
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Navigation && Selected && Width >= 24) { using var line = new Pen(Theme.SelectedInk, LogicalToDeviceUnits(2)); e.Graphics.DrawLine(line, 8, Height - 2, Width - 8, Height - 2); }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ink, fill);
    }
}

internal sealed class FanCurveChart : Control
{
    private readonly Theme _theme;
    private int[]? _points, _targets, _draft;
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
    public void SetDraft(int[]? draft)
    {
        if (_draft is null ? draft is null : draft is not null && _draft.SequenceEqual(draft)) return;
        _draft = draft;
        Invalidate();
    }

    private int D(int value) => LogicalToDeviceUnits(value);
    private RectangleF PlotBounds => new(D(39), D(12), Math.Max(1, Width - D(59)), Math.Max(1, Height - D(71)));
    // A six-column companion table keeps every exact point readable without inventing a categorical axis.
    internal IReadOnlyList<Rectangle> TemperatureAnnotationBounds()
    {
        if (_points is not { Length: 6 }) return Array.Empty<Rectangle>();
        return _points.Select((_, i) => new Rectangle(i * Width / 6, Height - D(42), Width / 6, D(20))).ToArray();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_points is not { Length: 6 } points || _targets is not { Length: 6 } targets || points.Where((p, i) => p < 0 || p > 100 || (i > 0 && p <= points[i - 1])).Any() || targets.Any(p => p < 0 || p > 100))
        { TextRenderer.DrawText(g, "Current curve unavailable · waiting for a complete six-point table", _theme.Body, ClientRectangle, _theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter); return; }
        var plot = PlotBounds;
        using var grid = new Pen(_theme.Border); using var ink = new SolidBrush(_theme.Muted);
        for (var percent = 0; percent <= 100; percent += 20)
        { var y = plot.Bottom - plot.Height * percent / 100; g.DrawLine(grid, plot.Left, y, plot.Right, y); g.DrawString(percent + "%", _theme.Small, ink, 0, y - D(7)); }
        var start = points[0]; var end = points[^1];
        PointF[] Vertices(int[] values) => points.Select((p, i) => new PointF(plot.Left + (p - start) * plot.Width / (end - start), plot.Bottom - values[i] * plot.Height / 100)).ToArray();
        var vertices = Vertices(targets);
        using var area = new GraphicsPath(); area.AddLines(vertices); area.AddLine(vertices[^1], new PointF(plot.Right, plot.Bottom)); area.AddLine(new PointF(plot.Right, plot.Bottom), new PointF(plot.Left, plot.Bottom)); area.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb(24, _theme.Accent)); g.FillPath(fill, area);
        using var line = new Pen(_theme.Accent, D(2)); using var dot = new SolidBrush(_theme.Accent); g.DrawLines(line, vertices);
        if (_draft is { Length: 6 } draft)
        {
            using var draftLine = new Pen(_theme.Ink, D(2)) { DashStyle = DashStyle.Dash };
            g.DrawLines(draftLine, Vertices(draft));
        }
        var annotations = TemperatureAnnotationBounds();
        for (var i = 0; i < 6; i++)
        {
            g.FillEllipse(dot, vertices[i].X - 3, vertices[i].Y - 3, 6, 6);
            // Companion table, not tick labels: plotted vertices retain the actual temperature axis.
            TextRenderer.DrawText(g, points[i] + "°C", _theme.Small, annotations[i], _theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter);
            var targetBounds = annotations[i]; targetBounds.Y += D(20);
            TextRenderer.DrawText(g, targets[i] + "%", _theme.Small, targetBounds, _theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter);
        }
    }
}
