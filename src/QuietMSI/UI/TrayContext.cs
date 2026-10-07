using Microsoft.Win32;
using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private bool _exiting;
    private bool _disposed;
    public MainForm Window { get; }
    public int PollIntervalMilliseconds => _pollTimer.Interval;

    public TrayContext(IHardwareBridge bridge)
    {
        Window = new MainForm(bridge);
        // Do not assign ApplicationContext.MainForm: its close semantics would exit the message loop.
        _menu = new ContextMenuStrip();
        _menu.Items.Add("&Open QuietMSI", null, (_, _) => ShowWindow());
        _menu.Items.Add("&Refresh telemetry", null, async (_, _) => await Window.PollAsync());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("E&xit", null, (_, _) => ExitApplication());
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "QuietMSI · Read-only", ContextMenuStrip = _menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();
        _pollTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _pollTimer.Tick += PollTick;
        Window.VisibleChanged += VisibilityChanged;
        Window.Resize += WindowResized;
        Window.FormClosing += ClosingWindow;
        Window.Shown += FirstShown;
        SystemEvents.PowerModeChanged += PowerModeChanged;
        _pollTimer.Start();
    }

    private async void FirstShown(object? sender, EventArgs e) => await Window.PollAsync();
    private async void PollTick(object? sender, EventArgs e)
    {
        Window.UpdateFreshness(DateTimeOffset.UtcNow);
        await Window.PollAsync();
    }

    private void VisibilityChanged(object? sender, EventArgs e)
    {
        if (_disposed || _exiting) return;
        _pollTimer.Interval = Window.Visible && Window.WindowState != FormWindowState.Minimized ? 5000 : 30000;
    }

    private void WindowResized(object? sender, EventArgs e)
    {
        if (!_exiting && Window.WindowState == FormWindowState.Minimized) Window.Hide();
    }

    private void ClosingWindow(object? sender, FormClosingEventArgs e)
    {
        if (_exiting || e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing or CloseReason.ApplicationExitCall) return;
        e.Cancel = true;
        Window.Hide();
    }

    public void ShowWindow()
    {
        if (_disposed || _exiting) return;
        Window.Show();
        Window.WindowState = FormWindowState.Normal;
        Window.Activate();
        _pollTimer.Interval = 5000;
    }

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_disposed || _exiting || !Window.IsHandleCreated || Window.IsDisposed) return;
        try
        {
            Window.BeginInvoke((Action)(() =>
            {
                if (_disposed || _exiting) return;
                if (e.Mode == PowerModes.Suspend)
                {
                    _pollTimer.Stop();
                    Window.SetSuspended(true);
                }
                else if (e.Mode == PowerModes.Resume)
                {
                    Window.SetSuspended(false);
                    _pollTimer.Start();
                }
            }));
        }
        catch (InvalidOperationException) { /* Window is shutting down. */ }
    }

    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        _pollTimer.Stop();
        _tray.Visible = false;
        Window.Close();
        Window.Dispose();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        _pollTimer.Stop();
        _tray.Visible = false;
        if (!Window.IsDisposed) Window.Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            SystemEvents.PowerModeChanged -= PowerModeChanged;
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
            if (!Window.IsDisposed) Window.Dispose();
        }
        base.Dispose(disposing);
    }
}
