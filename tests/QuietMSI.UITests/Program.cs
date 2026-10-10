using System.Reflection;
using QuietMSI.Core;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        QuietMSI.UI.RuntimeLog.LogPath = Path.Combine(AppContext.BaseDirectory, "test-runtime.log");
        try
        {
            if (args.Length == 2 && args[0] == "--readme-screenshot")
            {
                var bridge = new SyntheticControlBridge { DemoGpu = true };
                var startup = new StartupFixture(); startup.Initial.SetResult(new(false, "Off · QuietMSI starts when you open it."));
                using var demo = new QuietMSI.UI.MainForm(bridge, null, startup);
                demo.TopMost = true; demo.Show(); Pump(demo.PollAsync());
                Descendants(demo).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = "Dark";
                demo.Activate(); demo.Refresh(); UiEvents();
                using var bitmap = ScreenCapture(demo.RectangleToScreen(demo.ClientRectangle));
                bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
                Check(bridge.Requests.Count == 0, "README screenshot is a synthetic fixture with no hardware writes.");
                Console.WriteLine("PASS README screenshot (actual visible native window; synthetic readings)");
                return 0;
            }
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
                var bridge = new SyntheticControlBridge();
                using var fixture = new QuietMSI.UI.MainForm(bridge, null);
                fixture.Show(); Pump(fixture.PollAsync());
                var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "ui-overhaul"));
                Directory.CreateDirectory(folder);
                var defaultSize = fixture.ClientSize;
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    Descendants(fixture).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = theme;
                    fixture.ClientSize = defaultSize; UiEvents();
                    foreach (var page in new[] { "Overview", "Cooling", "Settings" })
                    {
                        SelectPage(fixture, page);
                        Capture(fixture, Path.Combine(folder, "synthetic-" + theme.ToLowerInvariant() + "-" + page.ToLowerInvariant() + ".png"));
                    }
                    SelectPage(fixture, "Overview"); fixture.ClientSize = new Size(440, 600); UiEvents();
                    Capture(fixture, Path.Combine(folder, "synthetic-" + theme.ToLowerInvariant() + "-compact.png"));
                }
                Check(bridge.Requests.Count == 0, "SyntheticCaptures: no hardware request may be sent.");
                var images = Directory.GetFiles(folder, "synthetic-*.png");
                Check(images.Length == 8, "SyntheticCaptures: exactly eight redesigned views required.");
                File.WriteAllText(Path.Combine(folder, "synthetic-provenance.json"), System.Text.Json.JsonSerializer.Serialize(new { Origin = "Native WinForms MainForm.DrawToBitmap; dropdown arrows judged from companion actual-screen captures, not WM_PRINT", Fixture = "SyntheticControlBridge; no live hardware transport; no writes", ThemeSettings = "Nonpersisting fixture (null path)", Dpi = fixture.DeviceDpi, CapturedAt = DateTimeOffset.UtcNow, AssemblySha256 = FileHash(Assembly.GetExecutingAssembly().Location), Images = images.Select(path => new { File = Path.GetFileName(path), Sha256 = FileHash(path) }).ToArray() }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                TestOnScreenDropdowns(folder);
                Console.WriteLine("PASS SyntheticCaptures " + folder);
                return 0;
            }
            if(args.Contains("--keyboard-regression")) { TestKeyboardViewport(); TestAsyncPollingViewport(); TestPollingScrollStability(); return 0; }
            if(args.Contains("--compact-regression")) { TestCompactTelemetryFit(); return 0; }
            if(args.Contains("--brand-regression")) { TestBrandWidthFit(); return 0; }
            if(args.Contains("--icon-regression")) { TestAppIcon(); return 0; }
            if(args.Contains("--chart-regression")) { TestChartAnnotationFit(); return 0; }
            if(args.Contains("--native-dropdowns")) { TestOnScreenDropdowns(); return 0; }
            if(args.Contains("--overview-poll-regression")) { TestOverviewPollingDraft(); return 0; }
            if(args.Contains("--scroll-regression")) { TestPollingScrollStability(); return 0; }
            if(args.Contains("--stability")) { TestResizePainting(); TestPollingPaintStability(); TestApplyDuringRead(); TestNativeFrame(); TestRuntimeLog(); TestStabilityLoop(); return 0; }
            if(args.Contains("--stability-preview"))
            {
                using var preview = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
                using var timer = new System.Windows.Forms.Timer { Interval = 500 };
                preview.Shown += async (_, _) => { await preview.PollAsync(); timer.Start(); };
                timer.Tick += async (_, _) => await preview.PollAsync();
                Application.Run(preview); return 0;
            }
            TestSettings();
            TestStartupDefinition();
            TestResizePainting();
            TestPollingPaintStability();
            TestApplyDuringRead();
            TestNativeFrame();
            TestRuntimeLog();
            TestKeyboardViewport();
            TestCompactTelemetryFit();
            TestBrandWidthFit();
            TestAppIcon();
            TestChartAnnotationFit();
            TestOnScreenDropdowns();
            TestOverviewPollingDraft();
            TestDashboardNavigation();
            TestBorderlessChrome();
            TestDashboardThemes();
            TestDashboardFirstViewport();
            TestNativeThemePixels();
            TestHeaderAndReadingsFit();
            TestAsyncPollingViewport();
            TestThemePersistence();
            TestPollingScrollStability();
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
            UiEvents();
            var hardware = Descendants(form).Where(c => c.Name.StartsWith("Hardware_", StringComparison.Ordinal)).ToArray();
            Check(hardware.Length == 14, "ReadOnlyControls: profile, boost, fan-mode and six CPU curve inputs with explicit Apply/Restore should exist.");
            Check(hardware.All(c => !c.Enabled), "ReadOnlyControls: every hardware control must be disabled.");
            Check(((QuietMSI.UI.ProfileSelector)hardware.Single(c => c.Name == "Hardware_Profile")).SelectedIndex == -1, "ReadOnlyControls: do not imply a current profile before any snapshot.");
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
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static string FileHash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void Capture(Form form, string path)
    {
        form.Refresh(); UiEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void TestDashboardNavigation()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        var navigation = new[] { "Overview", "Cooling" }.Select(name => Descendants(form).OfType<Button>().SingleOrDefault(c => c.Name == "Nav_" + name)).ToArray();
        Check(navigation.All(c => c is not null), "DashboardNavigation: Controls/Fan curve navigation must exist.");
        var editor = Descendants(form).Single(c => c.Name == "Hardware_CpuCurve2");
        Check(!editor.Visible, "DashboardNavigation: advanced editor belongs to Cooling, not the Overview first viewport.");
        navigation[1]!.PerformClick(); UiEvents();
        Check(editor.Visible, "DashboardNavigation: Cooling must expose the six-point editor.");
        Check(!Descendants(form).Any(c => c.Name == "Nav_Diagnostics" || c.AccessibleName == "Provider diagnostics"), "DashboardNavigation: Diagnostics tab and panel must be removed.");

        navigation[0]!.PerformClick(); UiEvents();
        Check(!editor.Visible && bridge.Requests.Count == 0 && bridge.Reads == 1, "DashboardNavigation: tabs must not write or trigger extra hardware reads.");
        Console.WriteLine("PASS DashboardNavigation (native tabs; dedicated curve editor, no reads or writes)");
    }

    private static void TestDashboardThemes()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        var selector = Descendants(form).OfType<ComboBox>().SingleOrDefault(c => c.Name == "ThemeMode");
        Check(selector is not null, "DashboardThemes: live Light/Dark/System theme selector must exist.");
        Check(selector!.Items.Cast<string>().SequenceEqual(new[] { "System", "Light", "Dark" }), "DashboardThemes: System is the default, with explicit Light and Dark choices.");
        selector.SelectedItem = "Light"; UiEvents(); var light = form.BackColor;
        selector.SelectedItem = "Dark"; UiEvents();
        Check(light != form.BackColor && form.BackColor.GetBrightness() < .2f, "DashboardThemes: switching must recolor the actual native window.");
        Check(Descendants(form).Where(c => c is TextBox || c is NumericUpDown || c is ComboBox).All(c => c.BackColor.GetBrightness() < .3f && c.ForeColor.GetBrightness() > .5f), "DashboardThemes: editors and selectors must not remain light islands.");
        Check(bridge.Requests.Count == 0 && bridge.Reads == 1, "DashboardThemes: themes must have no hardware effects.");
        Console.WriteLine("PASS DashboardThemes (live whole-window palettes; native inputs; no hardware effects)");
    }

    private static void TestBorderlessChrome()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        Check(form.FormBorderStyle == FormBorderStyle.None, "BorderlessChrome: native title bar must be removed.");
        var buttons = new[] { "Window_Minimize", "Window_Maximize", "Window_Close" }.Select(name => Descendants(form).OfType<Button>().Single(c => c.Name == name)).ToArray();
        Check(buttons.All(button => button.Visible && button.Parent!.ClientRectangle.Contains(button.Bounds)), "BorderlessChrome: caption actions must stay visible and inside the header.");
        buttons[1].PerformClick(); UiEvents();
        Check(form.WindowState == FormWindowState.Maximized && buttons[1].AccessibleName == "Restore window", "BorderlessChrome: maximize must offer restore.");
        buttons[1].PerformClick(); UiEvents();
        Check(form.WindowState == FormWindowState.Normal, "BorderlessChrome: restore must return to normal size.");
        form.ClientSize = new Size(440, 600); UiEvents();
        Check(buttons.All(button => button.Parent!.ClientRectangle.Contains(button.Bounds)), "BorderlessChrome: compact window must keep every caption action.");
        Check(bridge.Requests.Count == 0, "BorderlessChrome: window actions must never write hardware.");
        Console.WriteLine("PASS BorderlessChrome (custom caption actions, maximize/restore, compact placement; no writes)");
    }

    private static void TestDashboardFirstViewport()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        var viewport = Descendants(form).Single(c => c.AccessibleName == "Telemetry and settings");
        var chart = Descendants(form).Single(c => c.Name == "CurrentCurveCard");
        var bounds = viewport.RectangleToScreen(viewport.ClientRectangle);
        Check(!chart.Visible, "DashboardFirstViewport: compact Controls reserves fan curve detail for its dedicated view.");
        Check(new[] { "SensorCard", "PerformanceCard", "CoolingCard" }.All(name => bounds.Contains(Descendants(form).Single(c => c.Name == name).RectangleToScreen(Descendants(form).Single(c => c.Name == name).ClientRectangle))), "DashboardFirstViewport: all daily controls fit at the default size.");
        var apply = Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile");
        Check(apply is QuietMSI.UI.DashboardButton { Primary: true }, "DashboardFirstViewport: explicit profile Apply must carry the concept's teal primary action.");
        Console.WriteLine("PASS DashboardFirstViewport (all daily controls and primary Apply at default size)");
    }

    private static void TestNativeThemePixels()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = "Dark";
        UiEvents();
        var selector = (QuietMSI.UI.ProfileSelector)Descendants(form).Single(c => c.Name == "Hardware_Profile");
        using var bitmap = new Bitmap(selector.Width, selector.Height);
        selector.DrawToBitmap(bitmap, selector.ClientRectangle);
        Check(bitmap.GetPixel(8, 4).GetBrightness() < .3f, "NativeThemePixels: native closed dropdown must actually render dark, not just carry BackColor metadata.");
        Console.WriteLine("PASS NativeThemePixels (actual native control capture)");
    }

    private static void TestHeaderAndReadingsFit()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        var cpu = Descendants(form).OfType<Label>().Single(c => c.AccessibleName == "CPU temperature" && c.Text.Contains("55.0"));
        Check(cpu.Height >= TextRenderer.MeasureText(cpu.Text, cpu.Font).Height, "HeaderAndReadingsFit: sampled temperature must fit its font height.");
        var brand = Descendants(form).OfType<Label>().Single(c => c.Text == "QuietMSI");
        var machine = Descendants(form).OfType<Label>().Single(c => c.Text == "Synthetic UI fixture");
        Check(!brand.RectangleToScreen(brand.ClientRectangle).IntersectsWith(machine.RectangleToScreen(machine.ClientRectangle)), "HeaderAndReadingsFit: identity must not overlap the brand.");
        form.ClientSize = new Size(440, 600); UiEvents();
        var nav = Descendants(form).OfType<Button>().Single(c => c.Name == "Nav_Cooling");
        Check(nav.Parent!.ClientRectangle.Contains(nav.Bounds), "HeaderAndReadingsFit: compact navigation must expose the entire Fan curve action.");
        Console.WriteLine("PASS HeaderAndReadingsFit (measured font height, identity separation, complete compact tabs)");
    }

    private static Bitmap ScreenCapture(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(IntPtr handle, ref ComboInfo info);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Bounds => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ComboInfo { public int Size; public NativeRect Item, Button; public int State; public IntPtr Combo, Edit, List; }

    private static void TestOnScreenDropdowns(string? folder = null)
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); form.Activate(); form.BringToFront(); UiEvents();
        Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = "Dark";
        foreach (var name in new[] { "Hardware_FanMode", "ThemeMode" })
        {
            SelectPage(form, name == "ThemeMode" ? "Settings" : "Overview");
            var combo = Descendants(form).OfType<ComboBox>().Single(c => c.Name == name);
            combo.Focus(); combo.Refresh(); form.Refresh(); UiEvents(); Thread.Sleep(120);
            var info = new ComboInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<ComboInfo>() };
            Check(GetComboBoxInfo(combo.Handle, ref info), "OnScreenDropdowns: native arrow rectangle missing.");
            var bounds = combo.RectangleToScreen(combo.ClientRectangle);
            using (var bitmap = ScreenCapture(bounds))
            {
                folder ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "ui-overhaul"));
                Directory.CreateDirectory(folder);
                bitmap.Save(Path.Combine(folder, "native-dark-" + name + "-closed.png"));
                var arrow = Rectangle.Inflate(info.Button.Bounds, -3, -3);
                var bright = 0; var samples = 0;
                for (var x = arrow.Left; x < arrow.Right; x++) for (var y = arrow.Top; y < arrow.Bottom; y++)
                { samples++; if (bitmap.GetPixel(x, y).GetBrightness() > .7f) bright++; }
                Check(samples > 0 && bright < samples / 5, $"OnScreenDropdowns: real arrow compartment {name} is light ({bright}/{samples}); DPI {form.DeviceDpi}; bounds {arrow}.");
            }
            combo.DroppedDown = true; UiEvents(); Thread.Sleep(120);
            Check(GetComboBoxInfo(combo.Handle, ref info) && GetWindowRect(info.List, out var list), "OnScreenDropdowns: open native list missing.");
            GetWindowRect(info.List, out var listRect);
            using (var open = ScreenCapture(Rectangle.Union(bounds, listRect.Bounds)))
                open.Save(Path.Combine(folder, "native-dark-" + name + "-open.png"));
            using (var listBitmap = ScreenCapture(listRect.Bounds))
                Check(listBitmap.GetPixel(Math.Min(8, listBitmap.Width - 1), Math.Min(8, listBitmap.Height - 1)).GetBrightness() < .3f, "OnScreenDropdowns: open list must render dark.");
            combo.DroppedDown = false; UiEvents();
        }
        Check(bridge.Requests.Count == 0, "OnScreenDropdowns: no writes.");
        File.WriteAllText(Path.Combine(folder!, "native-dropdown-provenance.json"), System.Text.Json.JsonSerializer.Serialize(new { Origin = "Actual visible desktop Graphics.CopyFromScreen; PerMonitorV2; native COMBOBOXINFO arrow/list rectangles", Fixture = "SyntheticControlBridge; no hardware transport or requests; null settings path", Dpi = form.DeviceDpi, CapturedAt = DateTimeOffset.UtcNow, AssemblySha256 = FileHash(Assembly.GetExecutingAssembly().Location), Images = Directory.GetFiles(folder!, "native-dark-*.png").Select(path => new { File = Path.GetFileName(path), Sha256 = FileHash(path) }).ToArray() }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS OnScreenDropdowns (actual desktop closed arrow compartments + open lists; two selectors; no hardware) " + folder);
    }

    private static void TestChartAnnotationFit()
    {
        using var theme = new QuietMSI.UI.Theme();
        using var chart = new QuietMSI.UI.FanCurveChart(theme);
        chart.SetData(new SyntheticControlBridge().State);
        foreach (var width in new[] { 339, 550, 1070 })
        {
            chart.Size = new Size(width, 170);
            var bounds = chart.TemperatureAnnotationBounds();
            Check(bounds.Count == 6 && bounds.All(chart.ClientRectangle.Contains), "ChartAnnotationFit: all six real-temperature labels must remain within padded chart edges.");
            Check(!bounds.SelectMany((a, i) => bounds.Skip(i + 1).Select(a.IntersectsWith)).Any(v => v), "ChartAnnotationFit: measured 73/76/82°C annotations must not collide.");
            foreach (var (rect, index) in bounds.Select((r, i) => (r, i)))
            {
                var measured = TextRenderer.MeasureText(new SyntheticControlBridge().State.CpuBreakpoints![index] + "°C", theme.Small, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                Check(rect.Width >= measured.Width && rect.Height >= measured.Height, "ChartAnnotationFit: companion cell must fit exact text measurement.");
            }
        }
        Console.WriteLine("PASS ChartAnnotationFit (six measured labels; close 73/76/82°C; compact/wide edge padding; actual axis retained)");
    }

    private static void TestAppIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QuietMSI.AppIcon.ico")!;
        using var reader = new BinaryReader(stream);
        Check(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 7, "AppIcon: embed a valid seven-size Windows icon.");
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        Check(form.Icon is not null && form.Icon.Size == new Size(32, 32), "AppIcon: use the embedded icon in the window and tray.");
        Console.WriteLine("PASS AppIcon (embedded ICO, seven sizes, window/tray identity)");
    }
    private static void TestBrandWidthFit()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        foreach (var width in new[] { 1160, 440 })
        {
            form.ClientSize = new Size(width, 600); UiEvents();
            var brand = Descendants(form).OfType<Label>().Single(c => c.Text == "QuietMSI");
            var identity = Descendants(form).OfType<Label>().Single(c => c.Text == "Synthetic UI fixture");
            var measure = TextRenderer.MeasureText(brand.Text, brand.Font, Size.Empty, TextFormatFlags.SingleLine);
            Check(brand.Width >= measure.Width && brand.Height >= measure.Height, $"BrandWidthFit: QuietMSI needs {measure}, allocated {brand.Size}.");
            Check(identity.Left >= brand.Right + 12, "BrandWidthFit: identity must follow measured brand with a real gap.");
        }
        Console.WriteLine("PASS BrandWidthFit (measured full brand at wide + compact; identity positioned after it)");
    }

    private static void TestCompactTelemetryFit()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); form.ClientSize = new Size(440, 600); UiEvents();
        var labels = Descendants(form).OfType<Label>().Where(c => c.Text.Contains("55.0") || c.Text.StartsWith("Fan 1 ·") || c.Text.StartsWith("Battery ·")).ToArray();
        foreach (var label in labels)
        {
            var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Check(label.Height >= measured.Height, $"CompactTelemetryFit: {label.Text.Replace('\n', ' ')} needs {measured.Height}px at width {label.Width}, allocated {label.Height}.");
            Check(label.Parent!.ClientRectangle.Contains(label.Bounds), "CompactTelemetryFit: sensor card must contain truthful telemetry.");
        }
        var fan = labels.Single(c => c.Text.StartsWith("Fan 1 ·"));
        Check(fan.AccessibilityObject.Name == fan.Text, "CompactTelemetryFit: assistive technology must receive the live fan reading.");
        Check(fan.Text.Contains("Target 45% (not RPM)") && fan.AccessibleDescription!.Contains("not independently calibrated"), "CompactTelemetryFit: no shortening away measurement truth.");
        Check(!labels.SelectMany((a, i) => labels.Skip(i + 1).Select(b => a.Bounds.IntersectsWith(b.Bounds))).Any(overlap => overlap), "CompactTelemetryFit: wrapped telemetry cannot overlap neighboring rows.");
        Check(bridge.Requests.Count == 0, "CompactTelemetryFit: layout must not write.");
        Console.WriteLine("PASS CompactTelemetryFit (width-constrained CPU units, fan target/calibration source and battery; growing card; no overlap)");
    }

    private static void TestOverviewPollingDraft()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); form.ClientSize = new Size(440, 600); UiEvents();
        var scroll = Descendants(form).OfType<Panel>().Single(c => c.AccessibleName == "Telemetry and settings");
        var profile = Descendants(form).OfType<QuietMSI.UI.ProfileSelector>().Single(c => c.Name == "Hardware_Profile");
        scroll.ScrollControlIntoView(profile); profile.Focus(); profile.SelectedItem = "Turbo"; UiEvents();
        var position = scroll.AutoScrollPosition;
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var poll = form.PollAsync(); UiEvents();
        Check(profile.ContainsFocus && scroll.AutoScrollPosition == position, "OverviewPollingDraft: pending poll cannot disturb focused profile.");
        bridge.HeldRead.SetResult(bridge.Snapshot); Pump(poll); bridge.HeldRead = null;
        Check(profile.ContainsFocus && (string?)profile.SelectedItem == "Turbo" && scroll.AutoScrollPosition == position, "OverviewPollingDraft: sensor relayout cannot hide/refocus the profile or lose scroll/draft.");
        bridge.FailAnyRead = true; Pump(form.PollAsync());
        Check(form.IsStale && !profile.Enabled && (string?)profile.SelectedItem == "Turbo" && scroll.AutoScrollPosition == position, "OverviewPollingDraft: error must fail closed without draft/scroll loss.");
        bridge.FailAnyRead = false; Pump(form.PollAsync());
        Check(profile.Enabled && (string?)profile.SelectedItem == "Turbo" && scroll.AutoScrollPosition == position, "OverviewPollingDraft: error recovery keeps draft and viewport.");
        form.UpdateFreshness(DateTimeOffset.UtcNow.AddMinutes(2)); UiEvents();
        Check(!profile.Enabled && (string?)profile.SelectedItem == "Turbo" && scroll.AutoScrollPosition == position, "OverviewPollingDraft: stale transition retains draft and scroll.");
        Pump(form.PollAsync());
        Check(profile.Enabled && (string?)profile.SelectedItem == "Turbo" && scroll.AutoScrollPosition == position, "OverviewPollingDraft: stale recovery retains draft and scroll.");
        Check(bridge.Requests.Count == 0, "OverviewPollingDraft: no implicit Apply.");
        Console.WriteLine("PASS OverviewPollingDraft (compact lower profile focused through heldread/render; scroll/draft retained; no writes)");
    }

    private static void TestKeyboardViewport()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); form.ClientSize = new Size(440, 600); UiEvents();
        var scroll = Descendants(form).OfType<Panel>().Single(c => c.AccessibleName == "Telemetry and settings");
        var dialogKey = typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Reach(string target, bool backwards)
        {
            ((Button)Descendants(form).Single(c => c.Name == "Nav_" + (target.Contains("Curve") ? "Cooling" : "Overview"))).Focus();
            scroll.AutoScrollPosition = Point.Empty; UiEvents();
            var control = Descendants(form).Single(c => c.Name == target);
            for (var n = 0; n < 80 && !control.ContainsFocus; n++)
            {
                dialogKey.Invoke(form, new object[] { backwards ? Keys.Tab | Keys.Shift : Keys.Tab }); UiEvents();
            }
            Check(control.ContainsFocus, "KeyboardViewport: Tab traversal must reach " + target);
            Check(scroll.RectangleToScreen(scroll.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle)),
                $"KeyboardViewport: {(backwards ? "Shift+Tab" : "Tab")} must automatically reveal {target}; scroll {scroll.AutoScrollPosition}; viewport {scroll.RectangleToScreen(scroll.ClientRectangle)}; target {control.RectangleToScreen(control.ClientRectangle)}.");
        }
        foreach (var backwards in new[] { false, true })
        {
            SelectPage(form, "Overview");
            foreach (var target in new[] { "Hardware_ApplyProfile", "Hardware_ApplyBoost", "Hardware_AutomaticFans" }) Reach(target, backwards);
            SelectPage(form, "Cooling");
            foreach (var target in new[] { "Hardware_ApplyCurve", "Hardware_RestoreCurve" }) Reach(target, backwards);
        }
        // A held provider read must not suppress genuine user navigation.
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var poll = form.PollAsync(); UiEvents();
        Reach("Hardware_CpuCurve2", false); Reach("Hardware_CpuCurve2", true);
        bridge.HeldRead.SetResult(bridge.Snapshot); Pump(poll); bridge.HeldRead = null;
        Check(bridge.Requests.Count == 0, "KeyboardViewport: keyboard navigation must never apply.");
        Console.WriteLine("PASS KeyboardViewport (compact Overview/Cooling Tab + Shift+Tab Apply/Restore reveal; navigation during held read; zero writes)");
    }

    private static void TestAsyncPollingViewport()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync()); SelectPage(form, "Cooling");
        form.ClientSize = new Size(560, 540); UiEvents();
        var scroll = Descendants(form).OfType<Panel>().Single(c => c.AccessibleName == "Telemetry and settings");
        var input = (NumericUpDown)Descendants(form).Single(c => c.Name == "Hardware_CpuCurve2");
        scroll.ScrollControlIntoView(input); input.Focus(); input.Value = 49; UiEvents();
        scroll.AutoScrollPosition = new Point(0, 110); UiEvents();
        var before = scroll.AutoScrollPosition;
        bridge.HeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var poll = form.PollAsync(); UiEvents();
        Check(!poll.IsCompleted && input.Enabled && input.ContainsFocus, "AsyncPollingViewport: fresh draft input must remain enabled and focused during a held background read.");
        Check(Descendants(form).Single(c => c.Name == "Hardware_ApplyCurve").Enabled && bridge.Requests.Count == 0, "AsyncPollingViewport: background reads must not flash buttons or send writes.");
        bridge.HeldRead.SetResult(bridge.Snapshot); Pump(poll); bridge.HeldRead = null;
        Check(input.ContainsFocus && input.Value == 49 && scroll.AutoScrollPosition == before, "AsyncPollingViewport: completing a real async read must preserve focus, draft and viewport.");
        bridge.FailAnyRead = true; Pump(form.PollAsync());
        Check(!input.Enabled && form.IsStale && scroll.AutoScrollPosition == before, "AsyncPollingViewport: error reason transition must fail closed without scrolling.");
        bridge.FailAnyRead = false; Pump(form.PollAsync());
        Check(input.Enabled && input.Value == 49 && scroll.AutoScrollPosition == before, "AsyncPollingViewport: recovery preserves the staged curve and viewport.");
        form.UpdateFreshness(DateTimeOffset.UtcNow.AddMinutes(2)); UiEvents();
        Check(form.IsStale && !input.Enabled && input.Value == 49 && scroll.AutoScrollPosition == before, "AsyncPollingViewport: freshness transition must fail closed without scroll/draft loss.");
        Pump(form.PollAsync());
        Check(input.Enabled && input.Value == 49 && scroll.AutoScrollPosition == before, "AsyncPollingViewport: stale recovery preserves draft and viewport.");
        Console.WriteLine("PASS AsyncPollingViewport (held asynchronous read; focus/draft/scroll; guarded Apply; failure and recovery)");
    }

    private static void TestThemePersistence()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "theme-test-" + Guid.NewGuid() + ".json");
        var bridge = new SyntheticControlBridge();
        try
        {
            using (var first = new QuietMSI.UI.MainForm(bridge, path))
            {
                var theme = Descendants(first).OfType<ComboBox>().Single(c => c.Name == "ThemeMode");
                Check((string?)theme.SelectedItem == "System", "ThemePersistence: missing settings default to System.");
                theme.SelectedItem = "Dark";
            }
            using (var second = new QuietMSI.UI.MainForm(bridge, path))
                Check((string?)Descendants(second).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem == "Dark", "ThemePersistence: chosen mode must survive reopening.");
            File.WriteAllText(path, "corrupt fixture");
            using (var corrupt = new QuietMSI.UI.MainForm(bridge, path))
                Check((string?)Descendants(corrupt).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem == "System", "ThemePersistence: corrupt preference must fall back safely.");
            using (var denied = new QuietMSI.UI.MainForm(bridge, AppContext.BaseDirectory))
            {
                Descendants(denied).OfType<ComboBox>().Single(c => c.Name == "ThemeMode").SelectedItem = "Dark";
                Check(Descendants(denied).Any(c => c.Name == "ApplyResult" && c.Text.Contains("could not be saved")), "ThemePersistence: save failure remains nonfatal and visible.");
            }
            Check(bridge.Reads == 0 && bridge.Requests.Count == 0, "ThemePersistence: construction, disposal and preferences cannot touch hardware.");
            Console.WriteLine("PASS ThemePersistence (isolated file readback/reopen; System fallback; nonfatal write failure; no hardware)");
        }
        finally { File.Delete(path); }
    }

    private static void SelectPage(Form form, string page)
    {
        ((Button)Descendants(form).Single(c => c.Name == "Nav_" + page)).PerformClick();
        UiEvents();
    }

    private static void TestPollingScrollStability()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        SelectPage(form, "Cooling");
        var scroll=Descendants(form).OfType<Panel>().Single(c=>c.AccessibleName=="Telemetry and settings");
        var input=Descendants(form).Single(c=>c.Name=="Hardware_CpuCurve2");
        scroll.ScrollControlIntoView(input); input.Focus(); UiEvents();
        scroll.AutoScrollPosition=new Point(0,Math.Max(0,-scroll.AutoScrollPosition.Y-30));
        UiEvents();var before=scroll.AutoScrollPosition;
        for(var i=0;i<5;i++) { Pump(form.PollAsync()); UiEvents();
            Check(scroll.AutoScrollPosition==before,$"PollingScrollStability: polling moved viewport from {before} to {scroll.AutoScrollPosition} with a focused curve input."); }
        Check(bridge.Requests.Count==0,"PollingScrollStability: reproduction must not write.");
        Console.WriteLine("PASS PollingScrollStability (focused input, five refreshes, unchanged viewport, no writes)");
    }

    private static void TestCpuCurveWorkflow()
    {
        var bridge = new SyntheticControlBridge();
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        SelectPage(form, "Cooling");
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        SelectPage(form, "Cooling");
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        SelectPage(form, "Cooling");
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
        Check(Descendants(form).Any(c => c.Name == "Reason_FanCurve" && c.Visible && c.Text.Contains("complete, verified CPU curve") && c.AccessibleDescription!.Contains("complete captured CPU table")), "CpuCurveBounds: explain unavailable bounds instead of implying a writable curve.");
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show();
        Pump(form.PollAsync());
        var profile = (QuietMSI.UI.ProfileSelector)Descendants(form).Single(c => c.Name == "Hardware_Profile");
        var apply = (Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile");
        Check(profile.Enabled && apply.Enabled, "PerformanceWorkflow: fresh backend capability must enable native profile controls.");
        Check(profile.Items.Cast<string>().SequenceEqual(new[] { "High", "Medium", "Turbo", "Eco" }), "PerformanceWorkflow: expose the actual backend performance levels.");
        profile.SelectedItem = "Turbo";
        Pump(form.PollAsync());
        Check((string)profile.SelectedItem! == "Turbo", "PerformanceWorkflow: polling must not erase a pending selection.");
        UiEvents();
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
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
        public bool DemoGpu { get; set; }
        public HardwareControlState State { get; set; } = new(0xc1, 0x8d, 0, new[] { 38, 43, 48, 54, 60, 70 }, new[] { 0, 55, 64, 73, 76, 82 }) { CpuCurvePacket = new byte[] { 0, 38, 43, 48, 54, 60, 70, 85 } };
        public Capability GetCapability(HardwareSnapshot snapshot, ControlKind kind) => new(kind,
            (kind is ControlKind.PerformanceProfile or ControlKind.CoolerBoost or ControlKind.AutomaticFans or ControlKind.FanCurve) && !(DenyCurve && kind == ControlKind.FanCurve),
            "Synthetic test fixture; not live hardware.");
        public TaskCompletionSource<HardwareSnapshot>? HeldRead { get; set; }
        public bool FailAnyRead { get; set; }
        public HardwareSnapshot Snapshot => new HardwareSnapshot(new("Synthetic", "UI fixture", "Test BIOS", "Test EC"), DateTimeOffset.UtcNow,
            55, DemoGpu ? 49 : null, new FanReading(45, 1234, "Synthetic vendor-reported; not independently calibrated"), FanReading.FromTarget(null, "Not exposed"), 70, true,
            "Synthetic telemetry fixture; GPU asleep", new[] { "Synthetic fixture only — no live hardware transport." }) { Controls = MissingState ? null : State };
        public Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken token)
        {
            Reads++;
            if (FailAnyRead || (FailReadback && Requests.Count > 0)) throw new InvalidOperationException("Synthetic readback unavailable");
            return HeldRead is not null ? HeldRead.Task.WaitAsync(token) : Task.FromResult(Snapshot);
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        Check(Descendants(form).Any(c => c.Text.Contains("Fans Advanced")), "FanModeWorkflow: current Advanced mode must be shown.");
        Check(Descendants(form).Any(c => c.AccessibleDescription?.Contains("0°C → 38%") == true && c.AccessibleDescription.Contains("82°C → 70%")), "FanModeWorkflow: readable six-point current curve required.");
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
        Check(Descendants(form).Any(c => c.AccessibleDescription?.Contains("not a factory reset") == true), "FanModeWorkflow: clarify mode switching is not a factory reset.");
        Check(Descendants(form).Any(c => c.Text.StartsWith("Fan 1 ·")), "FanModeWorkflow: do not invent a CPU/GPU fan mapping.");
        Check(Descendants(form).Any(c => c.AccessibleDescription?.Contains("GPU asleep") == true), "FanModeWorkflow: expose provider sleep/unavailable telemetry reason.");
        Console.WriteLine("PASS FanModeWorkflow (synthetic; Advanced/current curve, explicit Auto, no unsupported editors)");
    }

    private static void TestFailedApply()
    {
        var bridge = new SyntheticControlBridge { FailApply = true };
        using var form = new QuietMSI.UI.MainForm(bridge, null);
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
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
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
        form.Show(); Pump(form.PollAsync());
        var bounds = form.RectangleToScreen(form.ClientRectangle);
        foreach (var name in new[] { "Hardware_ApplyProfile", "Hardware_ApplyBoost", "Hardware_AutomaticFans" })
        {
            var control = Descendants(form).Single(c => c.Name == name);
            var actual = control.RectangleToScreen(control.ClientRectangle);
            Check(actual.Top >= bounds.Top && actual.Bottom <= bounds.Bottom, "ActionVisibility: initial window must expose " + name);
        }
        form.ClientSize = new Size(440, 500); UiEvents();
        var gpu = Descendants(form).Single(c => c is Label && c.AccessibleName == "GPU temperature" && c.Text == "Unavailable");
        Check(gpu.Height < 40, "ActionVisibility: compact unavailable GPU status must not split the word across lines.");
        Console.WriteLine("PASS ActionVisibility (default Apply actions visible, compact status fit)");
    }

    private static void TestCapabilityDiagnostics()
    {
        using var form = new QuietMSI.UI.MainForm(new SyntheticControlBridge(), null);
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
        var form = new QuietMSI.UI.MainForm(bridge, null);
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
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        ((QuietMSI.UI.ProfileSelector)Descendants(form).Single(c => c.Name == "Hardware_Profile")).SelectedItem = "Turbo";
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick(); Pump(form.ActiveApply);
        Check(form.LastError?.Contains("readback", StringComparison.OrdinalIgnoreCase) == true && Descendants(form).Any(c => c.Name == "ApplyResult" && c.Text.Contains("mismatch", StringComparison.OrdinalIgnoreCase)), "ReadbackMismatch: UI cannot claim verified success when fresh readback disagrees.");
        Console.WriteLine("PASS ReadbackMismatch (synthetic backend success cannot hide disagreeing state)");
    }

    private static void TestReadbackFailure()
    {
        var bridge = new SyntheticControlBridge { FailReadback = true };
        using var form = new QuietMSI.UI.MainForm(bridge, null);
        form.Show(); Pump(form.PollAsync());
        ((Button)Descendants(form).Single(c => c.Name == "Hardware_ApplyProfile")).PerformClick(); Pump(form.ActiveApply);
        Check(form.IsStale && Descendants(form).Where(c => c.Name.StartsWith("Hardware_")).All(c => !c.Enabled), "ReadbackFailure: missing post-read must disable controls.");
        Check(Descendants(form).OfType<TextBox>().All(c => !c.Text.Contains("No hardware command was sent")), "ReadbackFailure: diagnostics must not deny an Apply request that actually occurred.");
        Console.WriteLine("PASS ReadbackFailure (synthetic post-read error; honest status and disabled controls)");
    }

    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { UiEvents(); Thread.Sleep(5); }
        Check(done(), "Asynchronous UI operation timed out.");
        UiEvents();
    }

    private static void TestResponsiveLayout(Type type)
    {
        using var form = (Form)Activator.CreateInstance(type, new ReadOnlyBridge())!;
        form.Show();
        form.ClientSize = new Size(440, 500);
        UiEvents();
        var client = form.RectangleToScreen(form.ClientRectangle);
        foreach (var page in new[] { "Overview", "Cooling" })
        {
            SelectPage(form, page);
            var overflow = Descendants(form).Where(c => c.Visible && (c is Label || c.Name.StartsWith("Hardware_")))
                .FirstOrDefault(c => c.RectangleToScreen(c.ClientRectangle).Right > client.Right - 6);
            Check(overflow is null, "ResponsiveLayout: text/control overflows compact " + page + ": " + overflow?.Text);
        }
        form.Scale(new SizeF(1.5f, 1.5f));
        UiEvents();
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
        UiEvents();
        Check((int)type.GetProperty("PollIntervalMilliseconds")!.GetValue(context)! == 5000, "TrayLifecycle: visible polling must use 5 seconds.");
        form.WindowState = FormWindowState.Minimized;
        UiEvents();
        Check(!form.Visible && !form.IsDisposed, "TrayLifecycle: minimizing must hide, not destroy the window.");
        Check((int)type.GetProperty("PollIntervalMilliseconds")!.GetValue(context)! == 30000, "TrayLifecycle: hidden polling must use 30 seconds.");
        type.GetMethod("ShowWindow")!.Invoke(context, null);
        form.Close();
        UiEvents();
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
        Check(bridge.Reads == 1 && ReferenceEquals(first, second), "NonOverlappingPolling: concurrent callers must share the active read.");
        bridge.Release();
        Pump(first);
        Check(Descendants(form).Any(c => c.Text.Contains("55.0")), "TelemetryRendering: the real bridge snapshot was not displayed.");
        Check(Descendants(form).Any(c => c.Text.Contains("Target 45") && !c.Text.Contains("45 RPM")), "TelemetryRendering: fan target must not be described as measured RPM.");
        Check(Descendants(form).Any(c => c.Text.Contains("1234 RPM reported") && c.AccessibleDescription?.Contains("vendor-reported; not independently calibrated") == true), "FanSource: vendor RPM must be reported with its calibration caveat, not claimed as verified physical RPM.");
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

    // Tests pump a finite message loop. DoEvents can restore the base context on return;
    // retain the WinForms dispatcher before starting an async operation, as Application.Run does.
    private static void UiEvents()
    {
        Application.DoEvents();
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    }

    private static void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            UiEvents();
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
