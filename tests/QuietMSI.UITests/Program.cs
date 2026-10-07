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
            if (args.Contains("--synthetic-screenshots"))
            {
                using var fixture = new QuietMSI.UI.MainForm(new SyntheticControlBridge());
                fixture.Show(); Pump(fixture.PollAsync());
                var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "screenshots"));
                Directory.CreateDirectory(folder);
                Capture(fixture, Path.Combine(folder, "synthetic-ui.png"));
                var scroll = Descendants(fixture).OfType<Panel>().Single(c => c.AccessibleName == "Telemetry and settings");
                scroll.ScrollControlIntoView(Descendants(fixture).Single(c => c.Name == "Hardware_RestoreCurve")); Application.DoEvents();
                Capture(fixture, Path.Combine(folder, "synthetic-cooling.png"));
                scroll.AutoScrollPosition = Point.Empty;
                fixture.ClientSize = new Size(440, 500); Application.DoEvents();
                Capture(fixture, Path.Combine(folder, "synthetic-compact.png"));
                Console.WriteLine("PASS SyntheticCaptures " + folder);
                return 0;
            }
            TestCpuCurveWorkflow();
            TestCpuCurveRestore();
            TestCpuCurveBounds();
            TestPerformanceWorkflow();
            TestBoostWorkflow();
            TestFanModeWorkflow();
            TestFailedApply();
            TestExitDuringApply();
            TestInvalidStateFailsClosed();
            TestActionVisibility();
            TestCapabilityDiagnostics();
            TestApplyDisposal();
            TestReadbackMismatch();
            TestReadbackFailure();
            var type = Assembly.GetExecutingAssembly().GetType("QuietMSI.UI.MainForm");
            Check(type is not null, "ReadOnlyControls: native MainForm has not been implemented.");
            using var form = (Form)Activator.CreateInstance(type!, new ReadOnlyBridge())!;
            form.Show();
            Application.DoEvents();
            var hardware = Descendants(form).Where(c => c.Name.StartsWith("Hardware_", StringComparison.Ordinal)).ToArray();
            Check(hardware.Length == 14, "ReadOnlyControls: profile, boost, fan-mode and six CPU curve inputs with explicit Apply/Restore should exist.");
            Check(hardware.All(c => !c.Enabled), "ReadOnlyControls: every hardware control must be disabled.");
            Check(((ComboBox)hardware.Single(c => c.Name == "Hardware_Profile")).SelectedIndex == -1, "ReadOnlyControls: do not imply a current profile before any snapshot.");
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

    private static void Capture(Form form, string path)
    {
        form.Refresh(); Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void TestCpuCurveWorkflow()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        var inputs = Descendants(form).OfType<NumericUpDown>().Where(c => c.Name.StartsWith("Hardware_CpuCurve")).OrderBy(c => c.Name).ToArray();
        Check(inputs.Length == 6, "CpuCurveWorkflow: six native CPU percentage inputs are required.");
        var original = new[] { 38, 43, 48, 54, 60, 70 };
        Check(inputs.Select(c => (int)c.Value).SequenceEqual(original), "CpuCurveWorkflow: draft starts at the current six percentages.");
        Check(inputs.Select(c => (int)c.Minimum).SequenceEqual(original) && inputs.All(c => c.Maximum == 85 && c.Enabled), "CpuCurveWorkflow: captured-original floors and preserved terminal cap must bound every enabled input.");
        Check(Enumerable.Range(0, 6).All(i => Descendants(form).Any(c => c.Name == "CurveBreakpoint" + i && c is Label && c.Text.Contains(bridge.State.CpuBreakpoints![i] + "°C"))), "CpuCurveWorkflow: six fixed breakpoint labels, not temperature editors, are required.");
        inputs[2].Value = 49;
        Pump(form.PollAsync());
        Check(inputs[2].Value == 49 && bridge.Requests.Count == 0, "CpuCurveWorkflow: draft edits and polling must not send writes or erase the draft.");
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyCurve")).PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.SequenceEqual(new[] { new ControlRequest(ControlKind.FanCurve, "38,43,49,54,60,70") }) && bridge.Reads == 3, "CpuCurveWorkflow: explicit Apply must send exactly six CSV percentages and read back once.");
        Check(form.LastError is null && Descendants(form).Any(c => c.Name == "ApplyResult" && c.Text.StartsWith("Backend verified")), "CpuCurveWorkflow: successful curve readback must be recognized.");
        Check(inputs.Select(c => (int)c.Minimum).SequenceEqual(original), "CpuCurveWorkflow: successful Apply must not replace session-original floors.");
        Check(bridge.State.FanMode == 0x8d && bridge.State.CpuBreakpoints!.SequenceEqual(new[] { 0, 55, 64, 73, 76, 82 }), "CpuCurveWorkflow: curve editing must not change mode or temperatures.");
        Console.WriteLine("PASS CpuCurveWorkflow (synthetic; six fixed breakpoints, bounded drafts, explicit CSV Apply and readback)");
    }

    private static void TestCpuCurveRestore()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        var original = (byte[])bridge.State.CpuCurvePacket!.Clone();
        Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "38,43,49,54,60,70")));
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_RestoreCurve")).PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.SequenceEqual(new[] { new ControlRequest(ControlKind.FanCurve, "38,43,49,54,60,70"), new ControlRequest(ControlKind.FanCurve, "Restore") }), "CpuCurveRestore: explicit Restore must send the exact backend Restore command, not guessed percentages.");
        Check(bridge.State.CpuCurvePacket!.SequenceEqual(original) && form.LastError is null, "CpuCurveRestore: full captured-table restoration must be recognized by readback.");
        Check(((NumericUpDown)Descendants(form).Single(c => c.Name == "Hardware_CpuCurve2")).Value == 48, "CpuCurveRestore: restored current values must replace the applied draft.");
        Check(Descendants(form).Any(c => c.Text == "Restore captured original"), "CpuCurveRestore: label must describe the session capture, not a factory reset.");
        bridge.IgnoreApply = true;
        Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "38,43,49,54,60,70")));
        Check(form.LastError?.Contains("mismatch") == true, "CpuCurveRestore: backend success cannot hide a curve readback mismatch.");
        bridge.IgnoreApply = false;
        Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "38,43,49,54,60,70")));
        bridge.IgnoreApply = true;
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_RestoreCurve")).PerformClick(); Pump(form.ActiveApply);
        Check(form.LastError?.Contains("mismatch") == true, "CpuCurveRestore: backend success cannot hide a failed original-table restoration.");
        Console.WriteLine("PASS CpuCurveRestore (synthetic; exact Restore command, full original packet readback, mismatch detection)");
    }

    private static void TestCpuCurveBounds()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        var input = (NumericUpDown)Descendants(form).Single(c => c.Name == "Hardware_CpuCurve2");
        input.Value = 60;
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyCurve")).PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Count == 0 && Descendants(form).Any(c => c.Name == "ApplyResult" && c.Text.Contains("nondecreasing")), "CpuCurveBounds: nonmonotonic drafts must be rejected visibly without a backend request.");
        foreach (var csv in new[] { "38,43,47,54,60,70", "38,43,48,54,60,86", "38,43,48,54,60", "Restore factory" })
            Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, csv)));
        Check(bridge.Requests.Count == 0, "CpuCurveBounds: public Apply guard must reject lower/over-cap/malformed curve requests.");
        bridge.State = bridge.State with { CpuCurvePacket = null };
        Pump(form.PollAsync());
        Check(Descendants(form).Where(c => c.Name.StartsWith("Hardware_") && (c is NumericUpDown || c.Name.EndsWith("Curve"))).All(c => !c.Enabled), "CpuCurveBounds: missing preserved terminal cap must disable editor and both actions.");
        Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "Restore")));
        Check(bridge.Requests.Count == 0, "CpuCurveBounds: missing packet must also block public Restore.");
        Check(Descendants(form).Any(c => c.Name == "Reason_FanCurve" && c.Visible && c.Text.Contains("complete captured CPU table")), "CpuCurveBounds: explain unavailable bounds instead of implying a writable curve.");
        var unavailable = new SyntheticControlBridge();
        unavailable.State = unavailable.State with { CpuCurvePacket = null };
        using var unknown = new QuietMSI.UI.MainForm(unavailable);
        unknown.Show(); Pump(unknown.PollAsync());
        Check(Descendants(unknown).OfType<NumericUpDown>().All(c => !c.Enabled), "CpuCurveBounds: do not invent initial fallback bounds when the packet is unavailable.");
        bridge.State = new SyntheticControlBridge().State;
        bridge.DenyCurve = true; Pump(form.PollAsync());
        Check(!input.Enabled, "CpuCurveBounds: complete data without write capability must remain read-only.");
        Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "38,43,49,54,60,70")));
        Check(bridge.Requests.Count == 0, "CpuCurveBounds: denied backend capability must not submit.");
        bridge.DenyCurve = false;
        foreach (var invalid in new[]
        {
            bridge.State with { CpuCurvePacket = new byte[] { 0, 38, 43, 48, 54, 60, 70 } },
            bridge.State with { CpuCurvePacket = new byte[] { 0, 38, 43, 48, 54, 60, 70, 86 } },
            bridge.State with { CpuCurve = new[] { 38, 43, 49, 54, 60, 70 } },
            bridge.State with { CpuBreakpoints = new[] { 0, 55, 65, 73, 76, 82 } },
            bridge.State with { CpuBreakpoints = null }
        })
        {
            bridge.State = invalid; Pump(form.PollAsync());
            Check(!input.Enabled, "CpuCurveBounds: malformed/inconsistent table or changed temperature/cap must disable editing.");
            Pump(form.ApplyControlAsync(new(ControlKind.FanCurve, "Restore")));
        }
        Check(bridge.Requests.Count == 0, "CpuCurveBounds: invalid or changed table metadata must never submit a restore.");
        Console.WriteLine("PASS CpuCurveBounds (synthetic; monotonic/floor/cap validation, unknown-data and capability gating)");
    }

    private static void TestPerformanceWorkflow()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show();
        Pump(form.PollAsync());
        var profile = (ComboBox)Descendants(form).Single(c => c.Name == "Hardware_Profile");
        var apply = (Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile");
        Check(profile.Enabled && apply.Enabled, "PerformanceWorkflow: fresh backend capability must enable native profile controls.");
        Check(profile.Items.Cast<string>().SequenceEqual(new[] { "High", "Medium", "Turbo", "Eco" }), "PerformanceWorkflow: expose the actual backend performance levels.");
        profile.SelectedItem = "Turbo";
        Pump(form.PollAsync());
        Check((string)profile.SelectedItem! == "Turbo", "PerformanceWorkflow: polling must not erase a pending selection.");
        Application.DoEvents();
        Check(bridge.Requests.Count == 0, "PerformanceWorkflow: selection alone must not write.");
        apply.PerformClick();
        PumpUntil(() => bridge.Reads == 3);
        Check(bridge.Requests.SequenceEqual(new[] { new ControlRequest(ControlKind.PerformanceProfile, "Turbo") }), "PerformanceWorkflow: Apply must issue exactly one correct request.");
        Check(Descendants(form).Any(c => c.Text.Contains("Profile Turbo")), "PerformanceWorkflow: current profile must use immediate readback.");
        Console.WriteLine("PASS PerformanceWorkflow (synthetic capability bridge; explicit Apply and readback)");
    }

    private static void TestBoostWorkflow()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        var boost = (CheckBox)Descendants(form).Single(c => c.Name == "Hardware_Boost");
        var apply = (Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyBoost");
        Check(boost.Enabled && apply.Enabled, "BoostWorkflow: backend capability must enable CoolerBoost.");
        boost.Checked = true;
        Check(bridge.Requests.Count == 0, "BoostWorkflow: checkbox alone must not write.");
        apply.PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Single() == new ControlRequest(ControlKind.CoolerBoost, "true") && bridge.Reads == 2, "BoostWorkflow: one true request and immediate readback required.");
        Check(Descendants(form).Any(c => c.Text.Contains("Boost On")), "BoostWorkflow: readback bit must be displayed.");
        boost.Checked = false; apply.PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Last() == new ControlRequest(ControlKind.CoolerBoost, "false") && bridge.Reads == 3, "BoostWorkflow: explicit off must use false.");
        form.UpdateFreshness(DateTimeOffset.UtcNow.AddMinutes(2));
        Check(Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "BoostWorkflow: stale must disable all hardware controls.");
        apply.PerformClick(); Check(bridge.Requests.Count == 2, "BoostWorkflow: stale must not submit.");
        Console.WriteLine("PASS BoostWorkflow (synthetic; on/off requests, readback, stale gating)");
    }

    private sealed class SyntheticControlBridge : IHardwareBridge, IControlCapabilityProvider
    {
        public List<ControlRequest> Requests { get; } = new();
        public int Reads { get; private set; }
        public bool FailApply { get; set; }
        public TaskCompletionSource<ControlResult>? HeldApply { get; set; }
        public bool MissingState { get; set; }
        public bool IgnoreApply { get; set; }
        public bool FailReadback { get; set; }
        public bool DenyCurve { get; set; }
        public HardwareControlState State { get; set; } = new(0xc1, 0x8d, 0, new[] { 38, 43, 48, 54, 60, 70 }, new[] { 0, 55, 64, 73, 76, 82 }) { CpuCurvePacket = new byte[] { 0, 38, 43, 48, 54, 60, 70, 85 } };
        public Capability GetCapability(HardwareSnapshot snapshot, ControlKind kind) => new(kind,
            (kind is ControlKind.PerformanceProfile or ControlKind.CoolerBoost or ControlKind.AutomaticFans or ControlKind.FanCurve) && !(DenyCurve && kind == ControlKind.FanCurve),
            "Synthetic test fixture; not live hardware.");
        public Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken token)
        {
            Reads++;
            if (FailReadback && Requests.Count > 0) throw new InvalidOperationException("Synthetic readback unavailable");
            return Task.FromResult(new HardwareSnapshot(new("Synthetic", "UI fixture", "Test BIOS", "Test EC"), DateTimeOffset.UtcNow,
                55, null, new FanReading(45, 1234, "Synthetic vendor-reported; not independently calibrated"), FanReading.FromTarget(null, "Not exposed"), 70, true,
                "Synthetic telemetry fixture; GPU asleep", new[] { "Synthetic fixture only — no live hardware transport." }) { Controls = MissingState ? null : State });
        }
        public Task<ControlResult> ApplyAsync(ControlRequest request, CancellationToken token)
        {
            Requests.Add(request);
            if (HeldApply is not null) return HeldApply.Task.WaitAsync(token);
            if (FailApply) return Task.FromResult(new ControlResult(false, "Synthetic firmware denied request"));
            if (IgnoreApply) return Task.FromResult(new ControlResult(true, "Synthetic backend reported success without state change"));
            if (request.Kind == ControlKind.FanCurve)
            {
                var original = new byte[] { 0, 38, 43, 48, 54, 60, 70, 85 };
                var packet = request.Value == "Restore" ? original : CoolingCurvePlan.Create(original, request.Value);
                State = State with { CpuCurve = packet.Skip(1).Take(6).Select(v => (int)v).ToArray(), CpuCurvePacket = packet };
            }
            if (request.Kind == ControlKind.AutomaticFans) State = State with { FanMode = (byte)(request.Value == "Advanced" ? 0x8d : 0x0d) };
            State = State with { Performance = request.Value switch { "High" => 0xc0, "Medium" => 0xc1, "Turbo" => 0xc4, "Eco" => 0xc2, _ => State.Performance } };
            State = State with { Boost = request.Kind == ControlKind.CoolerBoost ? (byte)(request.Value == "true" ? 0x80 : 0) : State.Boost };
            return Task.FromResult(new ControlResult(true, "Synthetic transaction verified."));
        }
    }

    private static void TestFanModeWorkflow()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        Check(Descendants(form).Any(c => c.Text.Contains("Fans Advanced")), "FanModeWorkflow: current Advanced mode must be shown.");
        Check(Descendants(form).Any(c => c.Text.Contains("0°C → 38%") && c.Text.Contains("82°C → 70%")), "FanModeWorkflow: readable six-point current curve required.");
        Check(!Descendants(form).Any(c => c.Name is "Hardware_ChargeLimit" or "Hardware_ApplyCharge"), "FanModeWorkflow: do not expose unsupported charge editors.");
        var auto = (Button)Descendants(form).Single(c => c.Name == "Hardware_AutomaticFans");
        ((ComboBox)Descendants(form).Single(c => c.Name == "Hardware_FanMode")).SelectedItem = "Auto";
        Check(auto.Enabled, "FanModeWorkflow: real Auto capability must enable Apply Auto.");
        auto.PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Single() == new ControlRequest(ControlKind.AutomaticFans, "Auto") && bridge.Reads == 2, "FanModeWorkflow: one Auto request and immediate readback required.");
        Check(Descendants(form).Any(c => c.Text.Contains("Fans Auto")), "FanModeWorkflow: Auto mode must use readback.");
        var mode = (ComboBox)Descendants(form).Single(c => c.Name == "Hardware_FanMode");
        mode.SelectedItem = "Advanced";
        Check(bridge.Requests.Count == 1, "FanModeWorkflow: Advanced selection alone must not write.");
        auto.PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Last() == new ControlRequest(ControlKind.AutomaticFans, "Advanced") && bridge.Reads == 3, "FanModeWorkflow: Advanced must use explicit mode-only request.");
        Check(Descendants(form).Any(c => c.Text.Contains("Fans Advanced")), "FanModeWorkflow: Advanced mode must use readback.");
        Check(Descendants(form).Any(c => c.Text.Contains("not a factory reset")), "FanModeWorkflow: clarify mode switching is not a factory reset.");
        Check(Descendants(form).Any(c => c.Text.StartsWith("Fan 1 ·")), "FanModeWorkflow: do not invent a CPU/GPU fan mapping.");
        Check(Descendants(form).Any(c => c.Text.Contains("GPU asleep")), "FanModeWorkflow: expose provider sleep/unavailable telemetry reason.");
        Console.WriteLine("PASS FanModeWorkflow (synthetic; Advanced/current curve, explicit Auto, no unsupported editors)");
    }

    private static void TestFailedApply()
    {
        var bridge = new SyntheticControlBridge { FailApply = true };
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        var apply = (Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile");
        apply.PerformClick(); Pump(form.ActiveApply);
        Check(bridge.Requests.Count == 1 && bridge.Reads == 2, "FailedApply: exactly one request then readback required.");
        Check(form.LastError == "Synthetic firmware denied request" && Descendants(form).Any(c => c.Name == "ApplyResult" && c.Text.Contains("Apply failed") && c.Text.Contains("denied")), "FailedApply: specific failure must remain visible.");
        Pump(form.PollAsync());
        Check(form.LastError == "Synthetic firmware denied request", "FailedApply: background polling must not erase the transaction error.");
        Console.WriteLine("PASS FailedApply (synthetic failure remains visible after refresh)");
    }

    private static void TestExitDuringApply()
    {
        var held = new TaskCompletionSource<ControlResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bridge = new SyntheticControlBridge { HeldApply = held };
        using var context = new QuietMSI.UI.TrayContext(bridge);
        context.ShowWindow(); Pump(context.Window.PollAsync());
        var form = context.Window;
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick();
        Check(form.IsApplying && Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "ExitDuringApply: busy must disable hardware controls.");
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick();
        Check(bridge.Requests.Count == 1, "ExitDuringApply: repeated click must not duplicate requests.");
        Pump(form.PollAsync());
        Check(bridge.Reads == 1, "ExitDuringApply: background poll must not run during apply.");
        context.ExitApplication();
        Check(!form.IsDisposed && form.IsApplying, "ExitDuringApply: exit must wait until transaction and readback resolve.");
        held.SetResult(new(false, "Synthetic transaction finished"));
        Pump(form.ActiveApply); PumpUntil(() => form.IsDisposed);
        Check(bridge.Reads == 2 && bridge.Requests.Count == 1, "ExitDuringApply: finish with readback, no exit write.");
        Console.WriteLine("PASS ExitDuringApply (synthetic held transaction; serialized writes, deferred exit)");
    }

    private static void TestInvalidStateFailsClosed()
    {
        var bridge = new SyntheticControlBridge { MissingState = true };
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        Check(Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "InvalidState: provider capability without current control values must fail closed.");
        Pump(form.ApplyControlAsync(new(ControlKind.PerformanceProfile, "Turbo")));
        Check(bridge.Requests.Count == 0, "InvalidState: public Apply guard must reject missing control state.");
        bridge.MissingState = false; Pump(form.PollAsync());
        Pump(form.ApplyControlAsync(new(ControlKind.ChargeLimit, "80")));
        Check(bridge.Requests.Count == 0, "InvalidState: unsupported UI workflows must never submit.");
        Console.WriteLine("PASS InvalidState (synthetic capabilities alone cannot authorize missing state)");
    }

    private static void TestActionVisibility()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge());
        form.Show(); Pump(form.PollAsync());
        var bounds = form.RectangleToScreen(form.ClientRectangle);
        foreach (var name in new[] { "Hardware_ApplyProfile", "Hardware_ApplyBoost", "Hardware_AutomaticFans" })
        {
            var control = Descendants(form).Single(c => c.Name == name);
            var actual = control.RectangleToScreen(control.ClientRectangle);
            Check(actual.Top >= bounds.Top && actual.Bottom <= bounds.Bottom, "ActionVisibility: initial window must expose " + name);
        }
        form.ClientSize = new Size(440, 500); Application.DoEvents();
        var gpu = Descendants(form).Single(c => c is Label && c.AccessibleName == "GPU temperature" && c.Text == "Unavailable");
        Check(gpu.Height < 40, "ActionVisibility: compact unavailable GPU status must not split the word across lines.");
        Console.WriteLine("PASS ActionVisibility (default Apply actions visible, compact status fit)");
    }

    private static void TestCapabilityDiagnostics()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge());
        Pump(form.PollAsync());
        var path = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location)!, "synthetic-diagnostics.json");
        try
        {
            Pump(form.ExportDiagnosticsAsync(path));
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Check(!json.RootElement.GetProperty("ReadOnly").GetBoolean(), "CapabilityDiagnostics: cannot label a writable synthetic bridge read-only.");
            var caps = json.RootElement.GetProperty("Capabilities").EnumerateArray().ToArray();
            Check(caps.Count(c => c.GetProperty("CanWrite").GetBoolean()) == 4, "CapabilityDiagnostics: export bridge capabilities, not obsolete identity policy.");
            Console.WriteLine("PASS CapabilityDiagnostics (synthetic capabilities serialized accurately)");
        }
        finally { File.Delete(path); }
    }

    private static void TestApplyDisposal()
    {
        var bridge = new SyntheticControlBridge { HeldApply = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick();
        var task = form.ActiveApply;
        form.Dispose(); Pump(task); form.Dispose();
        Check(bridge.Requests.Count == 1 && bridge.Reads == 1, "ApplyDisposal: cancellation must stop safely, without extra writes/readback on disposed UI.");
        Console.WriteLine("PASS ApplyDisposal (synthetic held apply cancelled safely)");
    }

    private static void TestReadbackMismatch()
    {
        var bridge = new SyntheticControlBridge { IgnoreApply = true };
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        ((ComboBox)Descendants(form).Single(c => c.Name == "Hardware_Profile")).SelectedItem = "Turbo";
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick(); Pump(form.ActiveApply);
        Check(form.LastError?.Contains("readback", StringComparison.OrdinalIgnoreCase) == true && Descendants(form).Any(c => c.Name == "ApplyResult" && c.Text.Contains("mismatch", StringComparison.OrdinalIgnoreCase)), "ReadbackMismatch: UI cannot claim verified success when fresh readback disagrees.");
        Console.WriteLine("PASS ReadbackMismatch (synthetic backend success cannot hide disagreeing state)");
    }

    private static void TestReadbackFailure()
    {
        var bridge = new SyntheticControlBridge { FailReadback = true };
        using var form = new QuietMSI.UI.MainForm(bridge);
        form.Show(); Pump(form.PollAsync());
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick(); Pump(form.ActiveApply);
        Check(form.IsStale && Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "ReadbackFailure: missing post-read must disable controls.");
        Check(Descendants(form).OfType<TextBox>().All(c => !c.Text.Contains("No hardware command was sent")), "ReadbackFailure: diagnostics must not deny an Apply request that actually occurred.");
        Console.WriteLine("PASS ReadbackFailure (synthetic post-read error; honest status and disabled controls)");
    }

    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Check(done(), "Asynchronous UI operation timed out.");
        Application.DoEvents();
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
