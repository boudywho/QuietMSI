using System.Runtime.InteropServices;

namespace QuietMSI.UI;

internal static class NativeAppearance
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref FrameMargins margins);
    [StructLayout(LayoutKind.Sequential)] private struct FrameMargins { public int Left, Right, Top, Bottom; }
    public static void ApplyBorderless(Form form)
    {
        var rounded = 2;
        DwmSetWindowAttribute(form.Handle, 33, ref rounded, sizeof(int));
        var border = SystemInformation.HighContrast ? -1 : -2; // DWMWA_COLOR_DEFAULT / NONE
        DwmSetWindowAttribute(form.Handle, 34, ref border, sizeof(int));
        var shadow = new FrameMargins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
        DwmExtendFrameIntoClientArea(form.Handle, ref shadow);
    }
    public static void Apply(Control control, bool dark)
    {
        if (!control.IsHandleCreated) return;
        // Custom painted controls must never receive the native Explorer button skin.
        if (control is ComboBox or CheckBox or TextBox or NumericUpDown)
            SetWindowTheme(control.Handle, dark && !SystemInformation.HighContrast ? "DarkMode_Explorer" : null, null);
        if (control is Form form) { var value = dark && !SystemInformation.HighContrast ? 1 : 0; DwmSetWindowAttribute(control.Handle, 20, ref value, sizeof(int)); ApplyBorderless(form); }
    }
}

internal sealed class ThemedComboBox : ComboBox
{
    private readonly Theme _theme;
    public ThemedComboBox(Theme theme) { _theme = theme; DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 24; FlatStyle = FlatStyle.Flat; }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeAppearance.Apply(this, _theme.Dark); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? _theme.Selected : _theme.Surface);
        e.Graphics.FillRectangle(brush, e.Bounds);
        if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, Rectangle.Inflate(e.Bounds, -4, 0), Enabled ? _theme.Ink : _theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is 0xF or 0x318 && IsHandleCreated)
        {
            using var graphics = m.Msg == 0x318 && m.WParam != IntPtr.Zero ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(Handle);
            using var fill = new SolidBrush(_theme.Surface); using var pen = new Pen(_theme.Border);
            graphics.FillRectangle(fill, Width - 22, 1, 21, Height - 2);
            graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using var arrow = new Pen(_theme.Muted, 1.5f); var x = Width - 12; var y = Height / 2;
            graphics.DrawLines(arrow, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
        }
    }
}

internal sealed class ThemedNumeric : NumericUpDown
{
    private readonly Theme _theme;
    public ThemedNumeric(Theme theme) { _theme = theme; BorderStyle = BorderStyle.FixedSingle; }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        foreach (Control child in Controls)
            if (child is not TextBox) { var skin = new SpinnerSkin(_theme); skin.AssignHandle(child.Handle); }
    }
    private sealed class SpinnerSkin : NativeWindow
    {
        private readonly Theme _theme;
        public SpinnerSkin(Theme theme) => _theme = theme;
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is 0xF or 0x318 && Handle != IntPtr.Zero)
            {
                using var g = m.Msg == 0x318 && m.WParam != IntPtr.Zero ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(Handle);
                var control = Control.FromHandle(Handle); if (control is null) return;
                using var fill = new SolidBrush(_theme.Selected); g.FillRectangle(fill, control.ClientRectangle);
                using var pen = new Pen(_theme.Muted, 1.4f); var x = control.Width / 2; var quarter = control.Height / 4;
                g.DrawLines(pen, new[] { new Point(x - 3, quarter + 1), new Point(x, quarter - 2), new Point(x + 3, quarter + 1) });
                g.DrawLines(pen, new[] { new Point(x - 3, quarter * 3 - 1), new Point(x, quarter * 3 + 2), new Point(x + 3, quarter * 3 - 1) });
            }
        }
    }
}
