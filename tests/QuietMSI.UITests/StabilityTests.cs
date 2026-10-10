using System.Diagnostics;
using System.Runtime.InteropServices;
using QuietMSI.Core;
using QuietMSI.UI;

internal static partial class Program
{
    private static void TestResizePainting()
    {
        using var theme = new Theme { Dark = true };
        using var host = new Form { ClientSize = new Size(360, 280), BackColor = theme.Background, StartPosition = FormStartPosition.CenterScreen };
        var card = new RoundedCard(theme) { Location = new Point(20, 20), Size = new Size(200, 180) };
        host.Controls.Add(card); host.Show(); UiEvents();
        // This pixel was outside the old rounded corner but inside the widened card.
        var probe = new Point(197, 177);
        var invalidated = new List<Rectangle>();
        card.Invalidated += (_, e) => invalidated.Add(e.InvalidRect);
        card.Width += 17;
        Check(invalidated.Any(rect => rect.Contains(probe) && rect.Width == card.Width && rect.Height == card.Height),
            "ResizePainting: expanding by a scrollbar width must invalidate the entire old rounded edge, not only the newly exposed strip.");
        Console.WriteLine("PASS ResizePainting (native resize invalidates the full card including the old rounded edge; no forced Refresh)");
    }

    private static void TestPollingPaintStability()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); UiEvents(); Pump(form.StartupLoaded); UiEvents();
        var controls = Descendants(form).Prepend(form).ToArray();
        var boundsChanges = 0; var fontChanges = 0; var enabledChanges = 0; var handleChanges = 0; var chartRedraws = 0;
        foreach (var c in controls)
        {
            c.SizeChanged += (_, _) => boundsChanges++;
            c.LocationChanged += (_, _) => boundsChanges++;
            c.FontChanged += (_, _) => fontChanges++;
            c.EnabledChanged += (_, _) => enabledChanges++;
            c.HandleCreated += (_, _) => handleChanges++;
        }
        Descendants(form).OfType<FanCurveChart>().Single().Invalidated += (_, _) => chartRedraws++;
        for (var i = 0; i < 100; i++) { Pump(form.PollAsync()); UiEvents(); }
        for (var i = 0; i < 100; i++)
        {
            bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bridge.HeldRead.SetResult(bridge.Snapshot with { CpuCelsius = 40 + i % 30 });
            Pump(form.PollAsync()); UiEvents();
        }
        bridge.HeldRead = null;
        Check(boundsChanges == 0 && fontChanges == 0 && enabledChanges == 0 && handleChanges == 0 && chartRedraws == 0,
            $"PollingPaintStability: unchanged samples must not rearrange or repaint static controls: bounds={boundsChanges}, fonts={fontChanges}, enabled={enabledChanges}, handles={handleChanges}, chart={chartRedraws}.");
        Check(bridge.Requests.Count == 0, "PollingPaintStability: no hardware writes.");
        using var readOnly = new MainForm(new ReadOnlyBridge(), null);
        readOnly.Show(); Pump(readOnly.PollAsync()); UiEvents(); Pump(readOnly.StartupLoaded); UiEvents();
        var layoutPasses = 0;
        foreach (var c in Descendants(readOnly)) c.Layout += (_, _) => layoutPasses++;
        for (var i = 0; i < 20; i++) { Pump(readOnly.PollAsync()); UiEvents(); }
        Check(layoutPasses == 0, $"PollingPaintStability: hidden unavailable reasons must not trigger layout, got {layoutPasses} passes.");
        var dashboard = Descendants(form).Single(c => c.Name == "SensorCard").Parent!;
        var height = dashboard.Height;
        form.Hide();
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.HeldRead.SetResult(bridge.Snapshot with { CpuFan = new FanReading(45, 2345, "fixture") });
        Pump(form.PollAsync());
        Check(dashboard.Height == height, "PollingPaintStability: hidden background samples must not collapse the dashboard.");
        form.Show(); UiEvents();
        Check(Descendants(form).Single(c => c.Name == "CoolingCard").Bottom <= dashboard.Height, "PollingPaintStability: reopening must retain all page content.");
        Console.WriteLine("PASS PollingPaintStability (200 unchanged/changing-temperature samples: zero bounds/font/enabled/handle changes or chart redraws)");
    }

    private static void TestApplyDuringRead()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = form.PollAsync();
        var apply = form.ApplyControlAsync(new(ControlKind.PerformanceProfile, "Eco"));
        Pump(form.ApplyControlAsync(new(ControlKind.PerformanceProfile, "Turbo")));
        Check(!apply.IsCompleted && bridge.Requests.Count == 0, "ApplyDuringRead: queue one request until the read completes; never overlap writes.");
        var pending = bridge.HeldRead; bridge.HeldRead = null; pending.SetResult(bridge.Snapshot);
        Pump(read); Pump(apply);
        Check(bridge.Requests.Count == 1 && bridge.State.Performance == 0xc2 && !form.IsApplying && !form.IsStale, "ApplyDuringRead: run exactly one queued request and verify its new state.");
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        read = form.PollAsync(); apply = form.ApplyControlAsync(new(ControlKind.PerformanceProfile, "High"));
        form.SetSuspended(true);
        pending = bridge.HeldRead; bridge.HeldRead = null; pending.SetResult(bridge.Snapshot);
        Pump(read); Pump(apply);
        Check(bridge.Requests.Count == 1 && form.IsStale, "ApplyDuringRead: suspension must cancel queued intent without writing.");
        form.SetSuspended(false); Pump(form.PollAsync());
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        read = form.PollAsync(); apply = form.ApplyControlAsync(new(ControlKind.PerformanceProfile, "High"));
        form.Dispose(); Pump(read); Pump(apply);
        Check(bridge.Requests.Count == 1, "ApplyDuringRead: disposal must cancel queued intent without writing.");
        Console.WriteLine("PASS ApplyDuringRead (one queued apply, fresh readback; duplicate and suspended requests guarded)");
    }

    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);

    private static void AssertNativeFrame(MainForm form)
    {
        Check((GetWindowLongPtr(form.Handle, -16).ToInt64() & 0x00C00000) == 0, "NativeFrame: actual HWND must have no WS_CAPTION bits.");
        Check(GetWindowRect(form.Handle, out var outer) && GetClientRect(form.Handle, out var client) &&
            outer.Right - outer.Left == client.Right && outer.Bottom - outer.Top == client.Bottom,
            "NativeFrame: the actual client area must include the entire window, without a legacy caption inset.");
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            var rect = new NativeRect { Left = 10, Top = 20, Right = 510, Bottom = 620 };
            Marshal.StructureToPtr(rect, memory, false);
            SendMessage(form.Handle, 0x83, IntPtr.Zero, memory);
            var result = Marshal.PtrToStructure<NativeRect>(memory);
            Check(result.Top == rect.Top && result.Bottom == rect.Bottom && result.Left == rect.Left && result.Right == rect.Right,
                "NativeFrame: wParam=0 NCCALCSIZE must not reintroduce borders.");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static void TestNativeFrame()
    {
        using var form = new MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        var theme = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ThemeMode");
        for (var i = 0; i < 20; i++)
        {
            theme.SelectedItem = i % 2 == 0 ? "Light" : "Dark";
            SendMessage(form.Handle, 0x86, IntPtr.Zero, IntPtr.Zero);
            SendMessage(form.Handle, 0x86, new IntPtr(1), IntPtr.Zero);
            SendMessage(form.Handle, 0x85, new IntPtr(1), IntPtr.Zero);
            form.ClientSize = i % 2 == 0 ? new Size(440, 600) : new Size(760, 760);
            UiEvents(); AssertNativeFrame(form);
        }
        var requested = 0; form.RestoreRequested += (_, _) => requested++;
        SendMessage(form.Handle, AppInstance.ShowMessage, IntPtr.Zero, IntPtr.Zero);
        Check(requested == 1, "NativeFrame: a second launch must request that the existing tray window reopen.");
        Console.WriteLine("PASS NativeFrame (actual HWND styles/client extent, both NCCALCSIZE forms, activation/theme/resize; reopen message)");
    }

    private static void TestRuntimeLog()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QuietMSI-log-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "application.log");
        try
        {
            RuntimeLog.Write("Test", new InvalidOperationException("fixture failure"), path);
            Check(File.ReadAllText(path).Contains("fixture failure"), "RuntimeLog: retain the specific exception for support.");
            File.WriteAllText(path, new string('x', 1024 * 1024));
            RuntimeLog.Write("Test", new IOException("fixture after rotation"), path);
            Check(new FileInfo(path).Length < 10000 && File.Exists(path + ".previous"), "RuntimeLog: rotate the bounded local support log.");
            RuntimeLog.Write("Test", new IOException("unwritable fixture"), directory);
            Console.WriteLine("PASS RuntimeLog (specific exception, bounded rotation, nonfatal write failure)");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void TestStabilityLoop()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new MainForm(bridge, null);
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        using var deadline = new System.Windows.Forms.Timer { Interval = 120000 };
        Exception? failure = null;
        var samples = 0; var busy = false; var baselineGdi = 0; var baselineUser = 0; var finalGdi = 0; var finalUser = 0;
        var stopwatch = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        deadline.Tick += (_, _) => { failure = new TimeoutException("StabilityLoop exceeded two minutes."); form.Close(); };
        form.Shown += async (_, _) =>
        {
            await form.PollAsync();
            baselineGdi = GetGuiResources(process.Handle, 0); baselineUser = GetGuiResources(process.Handle, 1);
            deadline.Start(); timer.Start();
        };
        timer.Tick += async (_, _) =>
        {
            if (busy) return;
            busy = true;
            try
            {
                bridge.FailAnyRead = samples % 31 == 30;
                await form.PollAsync();
                Check(form.IsStale == bridge.FailAnyRead, "StabilityLoop: failures and recovery must update freshness.");
                if (samples % 25 == 0)
                {
                    ((Button)Descendants(form).Single(c => c.Name == "Nav_" + (samples % 50 == 0 ? "Cooling" : "Overview"))).PerformClick();
                    form.ClientSize = samples % 50 == 0 ? new Size(440, 530) : new Size(960, 760);
                    Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = samples % 50 == 0 ? "Dark" : "Light";
                    AssertNativeFrame(form);
                }
                if (samples % 100 == 0) { form.WindowState = FormWindowState.Maximized; form.WindowState = FormWindowState.Normal; form.Hide(); form.Show(); }
                if (++samples == 100)
                {
                    // Warm every initially hidden editor, native popup and theme cache before
                    // comparing resource counts. First-use HWNDs are not a per-sample leak.
                    baselineGdi = GetGuiResources(process.Handle, 0); baselineUser = GetGuiResources(process.Handle, 1);
                    Console.WriteLine($"StabilityLoop warm baseline: GDI {baselineGdi}, USER {baselineUser}");
                }
                if (samples >= 1000)
                {
                    finalGdi = GetGuiResources(process.Handle, 0); finalUser = GetGuiResources(process.Handle, 1);
                    Check(finalGdi - baselineGdi < 20 && finalUser - baselineUser < 20,
                        $"StabilityLoop: resources must remain bounded: GDI {baselineGdi}->{finalGdi}, USER {baselineUser}->{finalUser}.");
                    Check(bridge.Requests.Count == 0, "StabilityLoop: stress testing may not write hardware.");
                    timer.Stop(); deadline.Stop(); form.Close();
                }
            }
            catch (Exception e) { failure = e; timer.Stop(); form.Close(); }
            finally { busy = false; }
        };
        Application.Run(form);
        if (failure is not null) throw failure;
        Console.WriteLine($"PASS StabilityLoop (real Application.Run: {samples} samples, failures/recovery, 40 page/theme/size changes, hide/show/maximize; {stopwatch.Elapsed.TotalSeconds:F1}s; GDI {baselineGdi}->{finalGdi}, USER {baselineUser}->{finalUser}; zero writes)");
    }
}
