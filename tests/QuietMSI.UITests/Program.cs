using System.Reflection;
using QuietMSI.Core;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Contains("--smoke"))
            {
                var host = Assembly.GetExecutingAssembly().GetType("QuietMSI.UIHost");
                Check(host is not null, "UIHostSmoke: host has not been implemented.");
                var screenshot = args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
                host!.GetMethod("Run")!.Invoke(null, new object?[] { new ReadOnlyBridge(), true, screenshot });
                Console.WriteLine("PASS UIHostSmoke (finite real message loop; ReadOnlyBridge)");
                return 0;
            }
            var type = Assembly.GetExecutingAssembly().GetType("QuietMSI.UI.MainForm");
            Check(type is not null, "ReadOnlyControls: native MainForm has not been implemented.");
            using var form = (Form)Activator.CreateInstance(type!, new ReadOnlyBridge())!;
            form.Show();
            Application.DoEvents();
            var hardware = Descendants(form).Where(c => c.Name.StartsWith("Hardware_", StringComparison.Ordinal)).ToArray();
            Check(hardware.Length >= 9, "ReadOnlyControls: profile, boost, charge, curve and automatic-fan controls must exist.");
            Check(hardware.All(c => !c.Enabled), "ReadOnlyControls: every hardware control must be disabled.");
            Check(Descendants(form).Any(c => c.Text.Contains("firmware", StringComparison.OrdinalIgnoreCase)), "ReadOnlyControls: visible safety reason is missing.");
            Check(Descendants(form).Any(c => c.Text == "Unavailable"), "ReadOnlyControls: unknown telemetry must show Unavailable.");
            Console.WriteLine("PASS ReadOnlyControls (real native controls; no hardware transport)");
            var cpuLabel = Descendants(form).Single(c => c is Label && c.AccessibleName == "CPU temperature" && c.Text == "Unavailable");
            var cpuBounds = cpuLabel.RectangleToScreen(cpuLabel.ClientRectangle);
            var clientBounds = form.RectangleToScreen(form.ClientRectangle);
            Check(cpuBounds.Top >= clientBounds.Top && cpuBounds.Bottom <= clientBounds.Bottom, "InitialLayout: CPU telemetry must be visible when the window opens, not scrolled below the header.");
            Console.WriteLine("PASS InitialLayout");
            TestPolling(type!);
            TestDiagnostics(type!);
            TestTray();
            TestDisposal(type!);
            TestResponsiveLayout(type!);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception.GetBaseException().Message);
            return 1;
        }
    }

    private static void TestResponsiveLayout(Type type)
    {
        using var form = (Form)Activator.CreateInstance(type, new ReadOnlyBridge())!;
        form.Show();
        form.ClientSize = new Size(440, 500);
        Application.DoEvents();
        var client = form.RectangleToScreen(form.ClientRectangle);
        var overflow = Descendants(form).Where(c => c is Label || c.Name.StartsWith("Hardware_"))
            .FirstOrDefault(c => c.RectangleToScreen(c.ClientRectangle).Right > client.Right - 6);
        Check(overflow is null, "ResponsiveLayout: text/control overflows the compact window: " + overflow?.Text);
        form.Scale(new SizeF(1.5f, 1.5f));
        Application.DoEvents();
        Check(Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "ResponsiveLayout: scaling must preserve safety.");
        Console.WriteLine("PASS ResponsiveLayout (440px compact resize; 150% scaling)");
    }

    private static void TestDisposal(Type type)
    {
        var bridge = new HeldBridge();
        var form = (Form)Activator.CreateInstance(type, bridge)!;
        form.Show();
        var task = (Task)type.GetMethod("PollAsync")!.Invoke(form, null)!;
        form.Dispose();
        Pump(task);
        form.Dispose();
        Check(bridge.Writes == 0, "DisposeDuringPoll: cancellation or disposal must not write.");
        Console.WriteLine("PASS DisposeDuringPoll (cancellation, repeated disposal)");
    }

    private static void TestTray()
    {
        var type = Assembly.GetExecutingAssembly().GetType("QuietMSI.UI.TrayContext");
        Check(type is not null, "TrayLifecycle: tray context is missing.");
        using var context = (ApplicationContext)Activator.CreateInstance(type!, new ReadOnlyBridge())!;
        var form = (Form)type!.GetProperty("Window")!.GetValue(context)!;
        form.Show();
        Application.DoEvents();
        Check((int)type.GetProperty("PollIntervalMilliseconds")!.GetValue(context)! == 5000, "TrayLifecycle: visible polling must use 5 seconds.");
        form.WindowState = FormWindowState.Minimized;
        Application.DoEvents();
        Check(!form.Visible && !form.IsDisposed, "TrayLifecycle: minimizing must hide, not destroy the window.");
        Check((int)type.GetProperty("PollIntervalMilliseconds")!.GetValue(context)! == 30000, "TrayLifecycle: hidden polling must use 30 seconds.");
        type.GetMethod("ShowWindow")!.Invoke(context, null);
        form.Close();
        Application.DoEvents();
        Check(!form.IsDisposed && !form.Visible, "TrayLifecycle: Close must keep the tray application alive.");
        type.GetMethod("ExitApplication")!.Invoke(context, null);
        Check(form.IsDisposed, "TrayLifecycle: explicit Exit must dispose the window.");
        Console.WriteLine("PASS TrayLifecycle (minimize, restore, close-to-tray, explicit Exit)");
    }

    private static void TestDiagnostics(Type type)
    {
        var export = type.GetMethod("ExportDiagnosticsAsync");
        Check(export is not null, "DiagnosticsExport: JSON export is not implemented.");
        using var form = (Form)Activator.CreateInstance(type, new ReadOnlyBridge())!;
        form.Show();
        Pump((Task)type.GetMethod("PollAsync")!.Invoke(form, null)!);
        var path = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location)!, "ui-diagnostics-test.json");
        try
        {
            Pump((Task)export!.Invoke(form, new object[] { path })!);
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Check(json.RootElement.GetProperty("ReadOnly").GetBoolean(), "DiagnosticsExport: exported safety status missing.");
            Check(json.RootElement.GetProperty("Snapshot").GetProperty("CpuCelsius").ValueKind == System.Text.Json.JsonValueKind.Null, "DiagnosticsExport: unknown sensor must stay null.");
            Check(json.RootElement.GetProperty("Capabilities").EnumerateArray().All(c => !c.GetProperty("CanWrite").GetBoolean()), "DiagnosticsExport: blocked capabilities must remain blocked.");
            Console.WriteLine("PASS DiagnosticsExport (JSON read back)");
        }
        finally { File.Delete(path); }
    }

    private static void TestPolling(Type type)
    {
        var method = type.GetMethod("PollAsync");
        Check(method is not null, "NonOverlappingPolling: PollAsync is not implemented.");
        var bridge = new HeldBridge();
        using var form = (Form)Activator.CreateInstance(type, bridge)!;
        form.Show();
        var first = (Task)method!.Invoke(form, null)!;
        var second = (Task)method.Invoke(form, null)!;
        Check(bridge.Reads == 1 && second.IsCompleted, "NonOverlappingPolling: a second call must not overlap the first.");
        bridge.Release();
        Pump(first);
        Check(Descendants(form).Any(c => c.Text.Contains("55.0")), "TelemetryRendering: the real bridge snapshot was not displayed.");
        Check(Descendants(form).Any(c => c.Text.Contains("Target 45") && !c.Text.Contains("45 RPM")), "TelemetryRendering: fan target must not be described as measured RPM.");
        Check(Descendants(form).Any(c => c.Text.Contains("1234 RPM reported") && c.Text.Contains("vendor-reported; not independently calibrated")), "FanSource: vendor RPM must be reported with its calibration caveat, not claimed as verified physical RPM.");
        var freshness = type.GetMethod("UpdateFreshness");
        Check(freshness is not null, "OngoingFreshness: age-based update is missing.");
        freshness!.Invoke(form, new object[] { DateTimeOffset.UtcNow.AddMinutes(2) });
        Check((bool)type.GetProperty("IsStale")!.GetValue(form)!, "OngoingFreshness: stale sample must invalidate even without completed read.");
        Console.WriteLine("PASS OngoingFreshness (independent of provider completion)");
        var suspend = type.GetMethod("SetSuspended");
        Check(suspend is not null, "SuspendStale: suspension handler is missing.");
        suspend!.Invoke(form, new object[] { true });
        Pump((Task)method.Invoke(form, null)!);
        Check(bridge.Reads == 1, "SuspendStale: polling must stop while suspended.");
        Check(Descendants(form).Any(c => c.Text.Contains("Stale")), "SuspendStale: sleep must mark readings stale.");
        suspend.Invoke(form, new object[] { false });
        bridge.Fail = true;
        Pump((Task)method.Invoke(form, null)!);
        Check(Descendants(form).Any(c => c.Text.Contains("test provider denied")), "ProviderFailure: the specific error must remain visible.");
        Check(bridge.Writes == 0, "ReadOnlyLifecycle: UI must never send a hardware command.");
        Console.WriteLine("PASS NonOverlappingPolling");
        Console.WriteLine("PASS TelemetryRendering");
        Console.WriteLine("PASS SuspendStale");
        Console.WriteLine("PASS ProviderFailure");
        Console.WriteLine("PASS ReadOnlyLifecycle");
    }

    private static void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Check(task.IsCompleted, "Asynchronous UI operation timed out.");
        task.GetAwaiter().GetResult();
    }

    private sealed class HeldBridge : IHardwareBridge
    {
        private readonly TaskCompletionSource<HardwareSnapshot> _sample = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public bool Fail { get; set; }
        public Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken token)
        {
            Reads++;
            if (Fail) throw new InvalidOperationException("test provider denied");
            return _sample.Task.WaitAsync(token);
        }
        public void Release() => _sample.SetResult(new HardwareSnapshot(new("Test only", "Test fixture", "Fixture BIOS", null), DateTimeOffset.UtcNow,
            55, null, new FanReading(45, 1234, "Test vendor-reported; not independently calibrated"), FanReading.FromTarget(null, "Unavailable"), 70, true,
            "Test fixture temperature provider", new[] { "Synthetic test fixture; not live machine telemetry." }));
        public Task<ControlResult> ApplyAsync(ControlRequest request, CancellationToken token)
        {
            Writes++;
            return Task.FromResult(new ControlResult(false, "Tests must never write."));
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
