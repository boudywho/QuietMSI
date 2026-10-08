using System.Runtime.InteropServices;

namespace QuietMSI.UI;

public sealed partial class MainForm
{
    private CaptionButton? _minimizeWindow, _maximizeWindow, _closeWindow;

    private void InitializeChrome(Panel header, params Control[] dragTargets)
    {
        _minimizeWindow = new CaptionButton(_theme, "\uE921") { Name = "Window_Minimize", AccessibleName = "Minimize to tray" };
        _maximizeWindow = new CaptionButton(_theme, "\uE922") { Name = "Window_Maximize", AccessibleName = "Maximize window" };
        _closeWindow = new CaptionButton(_theme, "\uE8BB") { Name = "Window_Close", AccessibleName = "Close to tray", CloseAction = true };
        _minimizeWindow.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _maximizeWindow.Click += (_, _) => ToggleMaximize();
        _closeWindow.Click += (_, _) => Close();
        header.Controls.AddRange(new Control[] { _minimizeWindow, _maximizeWindow, _closeWindow });
        foreach (var target in dragTargets.Append(header))
        {
            target.MouseDown += (_, e) => {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // Native caption drag / snap.
            };
            target.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleMaximize(); };
        }
        _tips.SetToolTip(_minimizeWindow, "Minimize to tray");
        _tips.SetToolTip(_maximizeWindow, "Maximize or restore");
        _tips.SetToolTip(_closeWindow, "Close to tray · Use tray → Exit to quit");
    }

    private void LayoutChrome(Control header)
    {
        if (_closeWindow is null || _minimizeWindow is null || _maximizeWindow is null) return;
        var right = header.Width - D(12);
        var width = D(36);
        _closeWindow.SetBounds(right - width, D(12), width, D(32));
        _maximizeWindow.SetBounds(right - 2 * width, D(12), width, D(32));
        _minimizeWindow.SetBounds(right - 3 * width, D(12), width, D(32));
    }

    private void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // Keep native resizing, snapping, Alt+Space and taskbar window actions.
            parameters.Style = (parameters.Style & ~0x00C00000) | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeAppearance.ApplyBorderless(this);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_maximizeWindow is null) return;
        _maximizeWindow.Text = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
        _maximizeWindow.AccessibleName = WindowState == FormWindowState.Maximized ? "Restore window" : "Maximize window";
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == AppInstance.ShowMessage)
        { RestoreRequested?.Invoke(this, EventArgs.Empty); message.Result = IntPtr.Zero; return; }
        if (message.Msg == 0x83) // Both WM_NCCALCSIZE forms must preserve the entire client area.
        { message.Result = IntPtr.Zero; return; }
        // DefWindowProc paints a legacy caption for WS_SYSMENU/WS_THICKFRAME on activation
        // and theme changes, even though NCCALCSIZE removed the non-client area.
        if (message.Msg is 0x85 or 0xAE or 0xAF) // WM_NCPAINT / undocumented caption redraws
        { message.Result = IntPtr.Zero; return; }
        if (message.Msg == 0x86) // WM_NCACTIVATE: update activation without painting a native frame.
        { message.Result = DefWindowProc(Handle, message.Msg, message.WParam, new IntPtr(-1)); return; }
        if (message.Msg == 0x84 && WindowState == FormWindowState.Normal) // WM_NCHITTEST
        {
            var packed = message.LParam.ToInt64();
            var point = PointToClient(new Point(unchecked((short)packed), unchecked((short)(packed >> 16))));
            var edge = D(6);
            var left = point.X < edge; var right = point.X >= ClientSize.Width - edge;
            var top = point.Y < edge; var bottom = point.Y >= ClientSize.Height - edge;
            var hit = top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
            if (hit != 0) { message.Result = (IntPtr)hit; return; }
        }
        base.WndProc(ref message);
        if (message.Msg == 0x24) // WM_GETMINMAXINFO: respect the taskbar and current monitor.
        {
            var screen = Screen.FromHandle(Handle);
            var limits = Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
            limits.MaximumPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            limits.MaximumSize = screen.WorkingArea.Size;
            limits.MinimumTrackSize = MinimumSize;
            Marshal.StructureToPtr(limits, message.LParam, false);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Point Reserved; public Size MaximumSize; public Point MaximumPosition; public Size MinimumTrackSize; public Size MaximumTrackSize; }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}

internal sealed class CaptionButton : Button
{
    private readonly Theme _theme;
    private readonly Font _symbol = new("Segoe MDL2 Assets", 10);
    private bool _hover;
    public bool CloseAction { get; init; }
    public CaptionButton(Theme theme, string symbol)
    {
        _theme = theme; Text = symbol; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        DoubleBuffered = true; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var fill = _hover ? CloseAction ? Color.FromArgb(196, 43, 55) : _theme.Selected : _theme.Surface;
        var ink = _hover && CloseAction ? Color.White : _theme.Muted;
        using var brush = new SolidBrush(fill); e.Graphics.FillRectangle(brush, ClientRectangle);
        TextRenderer.DrawText(e.Graphics, Text, _symbol, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ink, fill);
    }
    protected override void Dispose(bool disposing) { if (disposing) _symbol.Dispose(); base.Dispose(disposing); }
}
