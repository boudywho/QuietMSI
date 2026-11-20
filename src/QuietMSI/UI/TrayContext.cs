using Microsoft.Win32;
using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly Action? _prepareHardware;
    private bool _exiting;
    private bool _disposed;
    private bool _exitPending;
    private bool _preparing;
    private bool _started;
    private bool _suspended;
    private bool _drainingRead;
    public MainForm Window { get; }
    public int PollIntervalMilliseconds => _pollTimer.Interval;

    public TrayContext(IHardwareBridge bridge) : this(bridge, false) { }

    public TrayContext(IHardwareBridge bridge, bool startInTray) : this(bridge, startInTray, null) { }

    internal TrayContext(IHardwareBridge bridge, bool startInTray, Action? prepareHardware)
    {
        Window = new MainForm(bridge);
        _prepareHardware = prepareHardware;
        _preparing = prepareHardware is not null;
        if (_preparing) Window.BeginHardwareSetup();
        if (startInTray) _ = Window.Handle; // Receive the second-launch message even before the first Show.
        // Do not assign ApplicationContext.MainForm: its close semantics would exit the message loop.
        _menu = new ContextMenuStrip();
        _menu.Items.Add("&Open QuietMSI", null, (_, _) => ShowWindow());
        _menu.Items.Add("&Refresh telemetry", null, async (_, _) => await Window.PollAsync());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("E&xit", null, (_, _) => ExitApplication());
        _tray = new NotifyIcon { Icon = Window.Icon, Text = "QuietMSI · Laptop controls", ContextMenuStrip = _menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();
        _pollTimer = new System.Windows.Forms.Timer { Interval = startInTray ? 30000 : 5000 };
        _pollTimer.Tick += PollTick;
        Window.VisibleChanged += VisibilityChanged;
        Window.Resize += WindowResized;
        Window.FormClosing += ClosingWindow;
        Window.ApplyCompleted += ApplyCompleted;
        Window.RestoreRequested += (_, _) => ShowWindow();
        SystemEvents.PowerModeChanged += PowerModeChanged;
        if (!_preparing) _pollTimer.Start();
    }

    internal async Task StartAsync()
    {
        if (_started || _disposed || _exiting) return;
        _started = true;
        if (_prepareHardware is not null)
        {
            string status;
            try
            {
                await Task.Run(_prepareHardware);
                status = "Independent firmware support is ready. MSI Center is not required.";
            }
            catch (Exception error)
            {
                status = "Hardware support needs attention · " + error.Message;
                await Task.Run(() => RuntimeLog.Write("Hardware support setup", error));
            }
            _preparing = false;
            if (_disposed || _exiting) return;
            Window.FinishHardwareSetup(status);
            if (_exitPending) { ExitApplication(); return; }
        }
        if (_suspended) return;
        _pollTimer.Start();
        if (Window.LastSnapshot is null) await Window.PollAsync();
    }

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
        if (Window.HasPendingApply || _preparing)
        {
            e.Cancel = true;
            Window.Hide();
            if (e.CloseReason != CloseReason.UserClosing) ExitApplication();
            return;
        }
        if (_exiting || e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing or CloseReason.ApplicationExitCall) return;
        e.Cancel = true;
        Window.Hide();
    }

    public void ShowWindow()
    {
        if (_disposed || _exiting || _exitPending) return;
        var reopening = !Window.Visible;
        Window.Show();
        if (Window.WindowState == FormWindowState.Minimized) Window.WindowState = FormWindowState.Normal;
        Window.Activate();
        _pollTimer.Interval = 5000;
        if (reopening && !_preparing && !_suspended) _ = Window.PollAsync();
    }

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_disposed || _exiting || !Window.IsHandleCreated || Window.IsDisposed) return;
        try
        {
            Window.BeginInvoke((Action)(async () =>
            {
                if (_disposed || _exiting || _exitPending) return;
                if (e.Mode == PowerModes.Suspend && !_suspended)
                {
                    _suspended = true;
                    _pollTimer.Stop();
                    Window.SetSuspended(true);
                }
                else if (e.Mode == PowerModes.Resume && _suspended)
                {
                    _suspended = false;
                    Window.SetSuspended(false);
                    if (!_preparing)
                    {
                        _pollTimer.Start();
                        await Window.RefreshAfterResumeAsync();
                    }
                }
            }));
        }
        catch (InvalidOperationException) { /* Window is shutting down. */ }
    }

    public async void ExitApplication()
    {
        if (_exiting || _disposed || _drainingRead) return;
        if (Window.HasPendingApply || _preparing)
        {
            _exitPending = true;
            _pollTimer.Stop();
            Window.RequestShutdown(_preparing ? "Exit queued · Finishing hardware support setup." : Window.IsApplying
                ? "Exit queued · Waiting for transaction and readback." : "Exit queued · Cancelling the unsent request.");
            return;
        }
        _exitPending = true;
        _pollTimer.Stop();
        if (!Window.ActiveSample.IsCompleted)
        {
            _drainingRead = true;
            Window.RequestShutdown("Exiting · Stopping telemetry.");
            try { await Window.ActiveSample; }
            finally { _drainingRead = false; }
            if (_disposed || _exiting) return;
        }
        _exiting = true;
        _tray.Visible = false;
        Window.Close();
        Window.Dispose();
        ExitThread();
    }

    private void ApplyCompleted(object? sender, EventArgs e)
    {
        if (_exitPending && !_disposed && Window.IsHandleCreated)
            Window.BeginInvoke((Action)ExitApplication);
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
