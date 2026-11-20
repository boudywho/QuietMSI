using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed partial class MainForm : Form
{
    private readonly Theme _theme = new();
    private readonly Icon _appIcon;
    private readonly Bitmap _brandImage;
    private int D(int value) => LogicalToDeviceUnits(value);
    private readonly IHardwareBridge _bridge;
    private readonly Label _machine, _sampling, _current, _currentMode, _currentBoost, _result, _cpu, _gpu, _cpuFan, _gpuFan, _battery;
    private readonly Label _draftCurve;
    private readonly ToolTip _tips = new() { AutoPopDelay = 15000 };
    private readonly NumericUpDown[] _curveInputs = new NumericUpDown[6];
    private readonly Label[] _curveBreakpoints = new Label[6];
    private byte[]? _originalCpuCurve;
    private int[]? _originalCpuBreakpoints;
    private bool _curveDirty;
    private readonly Dictionary<ControlKind, List<Control>> _hardware = new();
    private readonly Dictionary<ControlKind, Label> _reasons = new();
    private readonly CancellationTokenSource _shutdown = new();
    private bool _polling, _resourcesDisposed, _suspended, _preparing, _shutdownRequested;
    private CancellationTokenSource? _pollCancellation;
    private Task<bool> _sampleTask = Task.FromResult(false);
    private bool _applyQueued;
    private (bool Dark, bool HighContrast)? _appliedPalette;
    private int _sampleGeneration;
    private string? _applyError;
    private bool _profileDirty, _boostDirty, _fanModeDirty, _renderingState;
    public bool IsApplying { get; private set; }
    internal bool HasPendingApply => _applyQueued || IsApplying;
    public Task ActiveApply { get; private set; } = Task.CompletedTask;
    public event EventHandler? ApplyCompleted;
    public event EventHandler? RestoreRequested;
    public HardwareSnapshot? LastSnapshot { get; private set; }
    public string? LastError { get; private set; }
    public bool IsStale { get; private set; } = true;

    private readonly ThemeSettings _settings;
    private readonly ComboBox _themeSelector;
    private readonly DashboardViewport _viewport = new() { AutoScroll = true, AccessibleName = "Telemetry and settings", TabStop = false };
    private readonly Panel _dashboard = new BufferedPanel();
    private readonly Dictionary<string, DashboardButton> _navigation = new();
    private DashboardCard _sensorCard = null!, _profileCard = null!, _coolingCard = null!, _chartCard = null!, _editorCard = null!;
    private FanCurveChart _chart = null!;
    private string _page = "Overview";
    private bool _dashboardLayout;
    private Func<int> _layoutSensors = null!;

    public MainForm(IHardwareBridge bridge) : this(bridge, ThemeSettings.DefaultPath) { }

    public MainForm(IHardwareBridge bridge, string? themeSettingsPath, IStartupRegistration? startupRegistration = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _settings = new ThemeSettings(themeSettingsPath);
        _startupRegistration = startupRegistration ?? new StartupRegistration();
        Text = "QuietMSI";
        FormBorderStyle = FormBorderStyle.Sizable;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = "QuietMSI laptop controls";
        Font = _theme.Body;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 650);
        MinimumSize = new Size(440, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = _appIcon = AppIcon.Load();
        using (var headerIcon = AppIcon.Load(64)) _brandImage = headerIcon.ToBitmap();

        var header = new BufferedPanel { Dock = DockStyle.Top, Height = 104, TabIndex = 0 };
        var mark = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Image = _brandImage, AccessibleName = "QuietMSI icon" };
        var brand = TextLabel("QuietMSI", _theme.Title);
        brand.Dock = DockStyle.None;
        _machine = TextLabel("Waiting for device…", _theme.Small);
        _machine.Dock = DockStyle.None; _machine.AutoSize = false; _machine.AutoEllipsis = true;
        header.Controls.AddRange(new Control[] { mark, brand, _machine });
        var nav = new FlowLayoutPanel { WrapContents = false, TabIndex = 0 };
        foreach (var page in new[] { "Overview", "Cooling", "Settings" })
        {
            var button = new DashboardButton(_theme) { Text = page, Name = "Nav_" + page, AccessibleName = page, Navigation = true, Selected = page == _page, Margin = new Padding(0, 0, 4, 0), TabIndex = _navigation.Count };
            button.Click += (_, _) => Navigate(page); _navigation[page] = button; nav.Controls.Add(button);
        }
        header.Controls.Add(nav);
        var refresh = NativeButton("Refresh");
        refresh.AutoSize = false; refresh.TabIndex = 1;
        refresh.Click += async (_, _) => await PollAsync();
        _tips.SetToolTip(refresh, "Refresh temperatures and current hardware settings.");
        _themeSelector = new ThemedComboBox(_theme) { Name = "ThemeMode", AccessibleName = "Appearance: System, Light or Dark", DropDownStyle = ComboBoxStyle.DropDownList, Width = 98 };
        _themeSelector.Items.AddRange(new object[] { "System", "Light", "Dark" });
        _themeSelector.SelectedItem = _settings.Read();
        _tips.SetToolTip(_themeSelector, "Appearance");
        header.Controls.Add(refresh);
        void PositionHeader()
        {
            var size = TextRenderer.MeasureText(brand.Text, brand.Font);
            header.Height = D(104);
            mark.SetBounds(D(18), D(16), D(30), D(30));
            brand.AutoSize = false; brand.Size = size; brand.Location = new Point(D(58), D(13));
            refresh.SetBounds(header.Width - D(106), D(63), D(88), D(31));
            nav.SetBounds(D(18), D(61), Math.Max(0, refresh.Left - D(30)), D(36));
            _machine.SetBounds(brand.Right + D(14), D(22), Math.Max(0, refresh.Right - brand.Right - D(14)), D(25));
            foreach (var button in _navigation.Values)
            {
                button.Margin = new Padding(0, 0, D(4), 0);
                button.Size = new Size(Math.Clamp((nav.Width - D(12)) / 3, 0, D(110)), D(34));
            }
        }
        header.Resize += (_, _) => PositionHeader();

        var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 72 };
        _sampling = TextLabel("Waiting for first sample", _theme.Small);
        _sampling.AutoEllipsis = true;
        _result = TextLabel("Select a setting, then Apply to confirm.", _theme.Small);
        _result.Name = "ApplyResult";
        _result.AccessibleRole = AccessibleRole.StatusBar;
        _result.AutoEllipsis = true;
        foreach (var label in new[] { _sampling, _result }) { label.Dock = DockStyle.None; label.AutoSize = false; footer.Controls.Add(label); }
        void PositionFooter()
        {
            footer.Height = D(72);
            _sampling.SetBounds(D(18), D(8), Math.Max(0, footer.Width - D(36)), D(19));
            _result.SetBounds(D(18), D(29), Math.Max(0, footer.Width - D(36)), D(39));
        }
        footer.Resize += (_, _) => PositionFooter();
        _result.TextChanged += (_, _) => SetTip(_result, _result.Text);
        _tips.SetToolTip(footer, "Close or minimize to keep QuietMSI in the tray. Use tray → Exit to quit.");

        _sensorCard = new DashboardCard(_theme) { Name = "SensorCard", AccessibleName = "System telemetry" };
        var telemetryTitle = TextLabel("System telemetry", _theme.Heading);
        var cpuTitle = TextLabel("CPU TEMPERATURE", _theme.Small); var gpuTitle = TextLabel("GPU TEMPERATURE", _theme.Small);
        var fanTitle = TextLabel("FANS", _theme.Small); var powerTitle = TextLabel("POWER", _theme.Small);
        _cpu = TextLabel("Unavailable", _theme.CompactReading); _cpu.Name = "CpuTemperature"; _cpu.AccessibleName = "CPU temperature: Unavailable";
        _gpu = TextLabel("Unavailable", _theme.CompactReading); _gpu.Name = "GpuTemperature"; _gpu.AccessibleName = "GPU temperature: Unavailable";
        _cpuFan = TextLabel("Fan 1 · Unavailable", _theme.Body);
        _gpuFan = TextLabel("Fan 2 · Unavailable", _theme.Body);
        _battery = TextLabel("Battery · Unavailable", _theme.Body);
        foreach (var c in new[] { telemetryTitle, cpuTitle, gpuTitle, fanTitle, powerTitle, _cpu, _gpu, _cpuFan, _gpuFan, _battery })
        { c.Dock = DockStyle.None; c.AutoSize = false; _sensorCard.Controls.Add(c); }
        _layoutSensors = () => {
            var textWidth = Math.Max(0, _sensorCard.Width - D(36));
            var half = textWidth / 2;
            var wide = textWidth >= D(620);
            var cell = wide ? textWidth / 4 : half;
            var side = Math.Max(0, cell - D(8));
            // Reserve room for wrapped readings on resize, independently of the current sample.
            int ReadingHeight(string example, int width) => TextRenderer.MeasureText(example, _theme.Body,
                new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak).Height + D(4);
            var readingWidth = wide ? side : textWidth;
            var fanHeight = Math.Max(D(wide ? 54 : 36), Math.Max(
                ReadingHeight("Fan 2 · 30000 RPM reported · Target 149.9% (not RPM)", readingWidth),
                ReadingHeight("Fan 2 · Measured RPM unavailable · Target 149.9% (not RPM)", readingWidth)));
            var powerHeight = Math.Max(D(40), ReadingHeight("Battery · Unavailable\nSource · Unavailable", readingWidth));
            Size MeasureTemperature(string text, Font font) => TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.SingleLine);
            var temperatureFont = _theme.CompactReading;
            if (Math.Max(MeasureTemperature("Unavailable", temperatureFont).Width, MeasureTemperature("125.0 °C", temperatureFont).Width) > side)
                temperatureFont = _theme.Heading;
            _cpu.Font = _gpu.Font = temperatureFont;
            var temperatureHeight = Math.Max(D(36), Math.Max(MeasureTemperature("Unavailable", temperatureFont).Height,
                MeasureTemperature("125.0 °C", temperatureFont).Height) + D(4));
            telemetryTitle.SetBounds(D(18), D(12), textWidth, D(26));
            cpuTitle.SetBounds(D(18), D(43), side, D(18)); gpuTitle.SetBounds(D(18) + cell, D(43), side, D(18));
            _cpu.SetBounds(D(18), D(61), side, temperatureHeight); _gpu.SetBounds(D(18) + cell, D(61), side, temperatureHeight);
            fanTitle.Visible = powerTitle.Visible = wide;
            if (wide)
            {
                fanTitle.SetBounds(D(18) + 2 * cell, D(43), side, D(18));
                powerTitle.SetBounds(D(18) + 3 * cell, D(43), side, D(18));
                _cpuFan.SetBounds(fanTitle.Left, D(61), side, fanHeight);
                _gpuFan.SetBounds(fanTitle.Left, _cpuFan.Bottom, side, fanHeight);
                _battery.SetBounds(powerTitle.Left, D(61), side, powerHeight);
                return Math.Max(_gpuFan.Bottom, Math.Max(_battery.Bottom, _cpu.Bottom)) + D(11);
            }
            _cpuFan.SetBounds(D(18), _cpu.Bottom + D(4), textWidth, fanHeight);
            _gpuFan.SetBounds(D(18), _cpuFan.Bottom, textWidth, fanHeight);
            _battery.SetBounds(D(18), _gpuFan.Bottom, textWidth, powerHeight);
            return _battery.Bottom + D(11);
        };

        var performance = Stack(_theme.Surface, Padding.Empty);
        performance.Controls.Add(TextLabel("Performance", _theme.Heading));
        _current = StateLabel("Current profile · Unavailable");
        performance.Controls.Add(_current);

        var profiles = new ProfileSelector(_theme) { AccessibleName = "Performance profile", Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 4) };
        profiles.SelectedIndexChanged += (_, _) => { if (!_renderingState) { _profileDirty = true; SetOperationStatus(profiles.SelectedItem + " selected · Apply profile to confirm."); } };
        Hardware(performance, ControlKind.PerformanceProfile, profiles, "Profile");
        var profileRow = ActionRow(performance);
        var applyProfile = NativeButton("Apply profile"); ((DashboardButton)applyProfile).Primary = true;
        applyProfile.Click += (_, _) => { if (profiles.SelectedItem is string value) BeginApply(new(ControlKind.PerformanceProfile, value)); };
        Hardware(profileRow, ControlKind.PerformanceProfile, applyProfile, "ApplyProfile");
        Reason(performance, ControlKind.PerformanceProfile);
        _profileCard = WrapCard(performance, "PerformanceCard");

        var cooling = Stack(_theme.Surface, Padding.Empty);
        cooling.Controls.Add(TextLabel("Cooling", _theme.Heading));
        _currentMode = StateLabel("Current fan mode · Unavailable");
        cooling.Controls.Add(_currentMode);
        var modeRow = ActionRow(cooling);
        var fanMode = new ThemedComboBox(_theme) { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, AccessibleName = "Fan mode: Auto or Advanced using current curve", Margin = new Padding(0, 6, 10, 0) };
        fanMode.AccessibleDescription = "Not a factory reset. Auto follows firmware; Advanced uses the existing curve. Applying a mode turns Cooler Boost off.";
        _tips.SetToolTip(fanMode, fanMode.AccessibleDescription);
        fanMode.Items.AddRange(new object[] { "Auto", "Advanced" }); fanMode.SelectedIndex = -1;
        fanMode.SelectedIndexChanged += (_, _) => { if (!_renderingState) { _fanModeDirty = true; SetOperationStatus($"{fanMode.SelectedItem} selected · Apply mode to confirm."); } };
        Hardware(modeRow, ControlKind.AutomaticFans, fanMode, "FanMode");
        var automatic = NativeButton("Apply mode");
        automatic.Click += (_, _) => { if (fanMode.SelectedItem is string value) BeginApply(new(ControlKind.AutomaticFans, value)); };
        Hardware(modeRow, ControlKind.AutomaticFans, automatic, "AutomaticFans");
        Reason(cooling, ControlKind.AutomaticFans);
        _currentBoost = StateLabel("Cooler Boost · Unavailable");
        cooling.Controls.Add(_currentBoost);
        var boostRow = ActionRow(cooling);
        var boost = new CheckBox { Text = "Cooler Boost", AutoSize = true, AccessibleName = "Proposed CoolerBoost", Margin = new Padding(0, 10, 16, 0) };
        boost.CheckedChanged += (_, _) => { if (!_renderingState) { _boostDirty = true; SetOperationStatus($"Boost {(boost.Checked ? "on" : "off")} selected · Apply boost to confirm."); } };
        Hardware(boostRow, ControlKind.CoolerBoost, boost, "Boost");
        var applyBoost = NativeButton("Apply boost");
        applyBoost.Click += (_, _) => BeginApply(new(ControlKind.CoolerBoost, boost.Checked ? "true" : "false"));
        Hardware(boostRow, ControlKind.CoolerBoost, applyBoost, "ApplyBoost");
        Reason(cooling, ControlKind.CoolerBoost);
        _tips.SetToolTip(boost, "Run fans at maximum effort. Select the checkbox, then Apply boost.");
        _coolingCard = WrapCard(cooling, "CoolingCard");

        _chartCard = new DashboardCard(_theme) { Name = "CurrentCurveCard" };
        var chartTitle = TextLabel("Current CPU fan curve", _theme.Heading); chartTitle.Dock = DockStyle.None;
        _chart = new FanCurveChart(_theme);
        _chartCard.Controls.AddRange(new Control[] { chartTitle, _chart });
        void PositionChart()
        {
            chartTitle.Location = new Point(D(18), D(14));
            _chart.SetBounds(D(18), D(47), Math.Max(0, _chartCard.Width - D(36)), Math.Max(0, _chartCard.Height - D(61)));
        }
        _chartCard.Resize += (_, _) => PositionChart();

        var editor = Stack(_theme.Surface, Padding.Empty);
        editor.Controls.Add(TextLabel("Edit CPU fan curve", _theme.Heading));
        _draftCurve = StateLabel("Waiting for a complete, verified curve.");
        editor.Controls.Add(_draftCurve);
        editor.Controls.Add(TextLabel("Set fan effort at each fixed temperature. Values cannot go below the captured curve.", _theme.Small));
        var curveRow = ActionRow(editor);
        for (var i = 0; i < 6; i++)
        {
            var point = Stack(_theme.Surface, Padding.Empty); point.Dock = DockStyle.None; point.AutoSize = false; point.Height = D(60); point.Width = 70; point.Margin = new Padding(0, 8, 8, 8);
            var label = TextLabel("— °C", _theme.Small); label.Name = "CurveBreakpoint" + i; _curveBreakpoints[i] = label; point.Controls.Add(label);
            var input = new ThemedNumeric(_theme) { Dock = DockStyle.Top, Minimum = 0, Maximum = 100, DecimalPlaces = 0, AccessibleName = "CPU curve point " + (i + 1) + " percentage" };
            input.ValueChanged += (_, _) => { if (!_renderingState) { _curveDirty = true; RenderCurveDraftStatus(); } };
            _curveInputs[i] = input; Hardware(point, ControlKind.FanCurve, input, "CpuCurve" + i); curveRow.Controls.Add(point);
        }
        curveRow.Resize += (_, _) => {
            var columns = curveRow.ClientSize.Width < D(400) ? 3 : 6;
            foreach (Control point in curveRow.Controls)
            {
                point.Margin = new Padding(0, D(4), D(8), D(4));
                point.Size = new Size(Math.Max(0, curveRow.ClientSize.Width / columns - point.Margin.Horizontal), D(54));
            }
        };
        var curveActions = ActionRow(editor);
        var applyCurve = NativeButton("Apply CPU curve"); ((DashboardButton)applyCurve).Primary = true;
        applyCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, string.Join(',', _curveInputs.Select(input => ((int)input.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        Hardware(curveActions, ControlKind.FanCurve, applyCurve, "ApplyCurve");
        var restoreCurve = NativeButton("Restore captured original");
        restoreCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, "Restore"));
        Hardware(curveActions, ControlKind.FanCurve, restoreCurve, "RestoreCurve");
        editor.Controls.Add(TextLabel("Restore returns the curve captured when this session started. Restore before quitting if you want those values back.", _theme.Small));
        Reason(editor, ControlKind.FanCurve);
        _editorCard = WrapCard(editor, "CurveEditorCard");

        _dashboard.Controls.AddRange(new Control[] { _sensorCard, _profileCard, _coolingCard, _chartCard, _editorCard });
        InitializeSettings();
        _viewport.Dock = DockStyle.Fill; _viewport.TabIndex = 1; _viewport.Controls.Add(_dashboard);
        Controls.Add(_viewport); Controls.Add(footer); Controls.Add(header);
        _viewport.SizeChanged += (_, _) => LayoutDashboard();
        DpiChanged += (_, _) => { PositionHeader(); PositionFooter(); PositionChart(); LayoutDashboard(); };
        _themeSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_themeSelector.SelectedItem is not string selection) return;
            ApplyTheme();
            if (!_settings.Write(selection)) SetOperationStatus("Appearance changed; preference could not be saved.");
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Shown += (_, _) =>
        {
            PositionHeader();
            PositionFooter();
            ApplyTheme(true);
            FitInitialWindow();
            _navigation[_page].Focus();
        };
        Shown += (_, _) => StartupLoaded = LoadStartupAsync();
        PositionHeader(); PositionFooter(); ApplyTheme(); RefreshCapabilities(); LayoutDashboard();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _shutdown.Cancel();
            _shutdown.Dispose();
            base.Dispose(disposing);
            _tips.Dispose();
            _brandImage.Dispose();
            _appIcon.Dispose();
            _theme.Dispose();
            return;
        }
        base.Dispose(disposing);
    }
}
