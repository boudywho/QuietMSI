using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed partial class MainForm : Form
{
    private readonly Theme _theme = new();
    private readonly Icon _appIcon;
    private readonly Bitmap _brandImage;
    private int D(int value) => LogicalToDeviceUnits(value);
    private readonly IHardwareBridge _bridge;
    private readonly Label _machine, _sampling, _current, _result, _cpu, _gpu, _cpuFan, _gpuFan, _battery;
    private readonly Label _settingsHint;
    private readonly ToolTip _tips = new() { AutoPopDelay = 15000 };
    private readonly NumericUpDown[] _curveInputs = new NumericUpDown[6];
    private readonly Label[] _curveBreakpoints = new Label[6];
    private byte[]? _originalCpuCurve;
    private int[]? _originalCpuBreakpoints;
    private bool _curveDirty;
    private readonly Dictionary<ControlKind, List<Control>> _hardware = new();
    private readonly Dictionary<ControlKind, Label> _reasons = new();
    private readonly Dictionary<ControlKind, bool> _reasonVisibility = new();
    private readonly CancellationTokenSource _shutdown = new();
    private bool _polling, _resourcesDisposed, _suspended;
    private Task _activePoll = Task.CompletedTask;
    private bool _applyQueued;
    private (bool Dark, bool HighContrast)? _appliedPalette;
    private int _sampleGeneration;
    private string? _applyError;
    private bool _profileDirty, _boostDirty, _fanModeDirty, _renderingState;
    public bool IsApplying { get; private set; }
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
    private RoundedCard _sensorCard = null!, _profileCard = null!, _coolingCard = null!, _chartCard = null!, _editorCard = null!;
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
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = "QuietMSI laptop controls";
        Font = _theme.Body;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(D(560), D(700));
        MinimumSize = new Size(440, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = _appIcon = AppIcon.Load();
        using (var headerIcon = AppIcon.Load(64)) _brandImage = headerIcon.ToBitmap();
        var work = Screen.FromControl(this).WorkingArea;
        if (Width > work.Width || Height > work.Height) Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));

        var header = new BufferedPanel { Dock = DockStyle.Top, Height = 110 };
        var mark = new PictureBox { Location = new Point(22, 18), Size = new Size(34, 34), SizeMode = PictureBoxSizeMode.Zoom, Image = _brandImage, AccessibleName = "QuietMSI icon" };
        var brand = TextLabel("QuietMSI", _theme.Title);
        brand.Dock = DockStyle.None; brand.Location = new Point(66, 12);
        _machine = TextLabel("Waiting for device…", _theme.Small);
        _machine.Dock = DockStyle.None; _machine.AutoSize = false; _machine.AutoEllipsis = true;
        header.Controls.AddRange(new Control[] { mark, brand, _machine });
        InitializeChrome(header, mark, brand, _machine);
        var nav = new FlowLayoutPanel { Location = new Point(22, 68), Width = 260, Height = 36, WrapContents = false };
        foreach (var page in new[] { "Overview", "Cooling", "Settings" })
        {
            var button = new DashboardButton(_theme) { Text = page == "Overview" ? "Controls" : page == "Cooling" ? "Fan curve" : "Settings", Name = "Nav_" + page, AccessibleName = page == "Overview" ? "Main controls" : page == "Cooling" ? "CPU fan curve editor" : "Application settings", Navigation = true, Selected = page == _page, Size = new Size(100, 34), Margin = new Padding(0, 0, 4, 0) };
            button.Click += (_, _) => Navigate(page); _navigation[page] = button; nav.Controls.Add(button);
        }
        header.Controls.Add(nav);
        var refresh = NativeButton("Refresh");
        refresh.AutoSize = false; refresh.Size = new Size(76, 32);
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
            header.Height = D(110);
            mark.SetBounds(D(22), D(18), D(34), D(34));
            brand.AutoSize = false; brand.Size = size; brand.Location = new Point(D(66), D(12));
            refresh.Size = new Size(D(76), D(32));
            nav.SetBounds(D(22), D(68), Math.Max(D(120), header.Width - D(136)), D(36));
            _machine.SetBounds(brand.Right + D(14), D(24), Math.Max(0, header.Width - brand.Right - D(172)), D(23));
            _machine.Visible = _machine.Width >= D(80);
            LayoutChrome(header);
            refresh.Location = new Point(header.Width - D(98), D(69));
            foreach (var button in _navigation.Values) button.Size = new Size(Math.Min(D(112), (nav.Width - D(12)) / 3), D(34));
        }
        header.Resize += (_, _) => PositionHeader();

        var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 86 };
        _sampling = TextLabel("Waiting for first sample", _theme.Small);
        _result = TextLabel("Select a setting, then Apply to confirm.", _theme.Small);
        _result.Name = "ApplyResult";
        _settingsHint = TextLabel("Appearance and startup changes are saved automatically.", _theme.Small);
        _settingsHint.Name = "SettingsHint"; _settingsHint.Visible = false;
        foreach (var label in new[] { _sampling, _result, _settingsHint }) { label.Dock = DockStyle.None; label.AutoSize = false; footer.Controls.Add(label); }
        footer.Resize += (_, _) => {
            footer.Height = D(86);
            _sampling.SetBounds(D(22), D(10), Math.Max(D(100), footer.Width - D(44)), D(22));
            _result.SetBounds(D(22), D(34), Math.Max(D(100), footer.Width - D(44)), D(44));
            _settingsHint.Bounds = _result.Bounds;
        };
        _tips.SetToolTip(footer, "Close or minimize to keep QuietMSI in the tray. Use tray → Exit to quit.");

        _sensorCard = new RoundedCard(_theme) { Name = "SensorCard" };
        var cpuTitle = TextLabel("CPU", _theme.Small); var gpuTitle = TextLabel("GPU", _theme.Small);
        _cpu = TextLabel("Unavailable", _theme.Heading); _cpu.AccessibleName = "CPU temperature";
        _gpu = TextLabel("Unavailable", _theme.Heading); _gpu.AccessibleName = "GPU temperature";
        _cpuFan = TextLabel("Fan 1 · Unavailable", _theme.Small);
        _gpuFan = TextLabel("Fan 2 · Unavailable", _theme.Small);
        _battery = TextLabel("Battery · Unavailable", _theme.Small);
        foreach (var c in new[] { cpuTitle, gpuTitle, _cpu, _gpu, _cpuFan, _gpuFan, _battery }) { c.Dock = DockStyle.None; c.AutoSize = false; _sensorCard.Controls.Add(c); }
        _layoutSensors = () => {
            var textWidth = Math.Max(D(100), _sensorCard.Width - D(36));
            var half = textWidth / 2;
            foreach (var reading in new[] { _cpu, _gpu }) reading.Font = reading.Text == "Unavailable" ? _theme.Heading : half < D(200) ? _theme.CompactReading : _theme.Reading;
            int HeightFor(Label label, int width) => TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 4;
            cpuTitle.SetBounds(D(18), D(14), half - D(8), D(22)); gpuTitle.SetBounds(D(18) + half, D(14), half - D(8), D(22));
            _cpu.SetBounds(D(18), D(36), half - D(8), HeightFor(_cpu, half - D(8)));
            _gpu.SetBounds(D(18) + half, D(36), half - D(8), HeightFor(_gpu, half - D(8)));
            var top = Math.Max(_cpu.Bottom, _gpu.Bottom) + D(10);
            _cpuFan.SetBounds(D(18), top, textWidth, HeightFor(_cpuFan, textWidth));
            _gpuFan.SetBounds(D(18), _cpuFan.Bottom + D(2), textWidth, HeightFor(_gpuFan, textWidth));
            _battery.SetBounds(D(18), (_gpuFan.Visible ? _gpuFan.Bottom : _cpuFan.Bottom) + D(8), textWidth, HeightFor(_battery, textWidth));
            return _battery.Bottom + D(14);
        };
        _sensorCard.Resize += (_, _) => _layoutSensors();

        var performance = Stack(_theme.Surface, Padding.Empty);
        performance.Controls.Add(TextLabel("Performance", _theme.Heading));

        var profiles = new ProfileSelector(_theme) { AccessibleName = "Performance profile", Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 4) };
        profiles.SelectedIndexChanged += (_, _) => { if (!_renderingState) { _profileDirty = true; _result.Text = profiles.SelectedItem + " selected · Apply profile to confirm."; } };
        Hardware(performance, ControlKind.PerformanceProfile, profiles, "Profile");
        var profileRow = ActionRow(performance);
        var applyProfile = NativeButton("Apply profile"); ((DashboardButton)applyProfile).Primary = true;
        applyProfile.Click += (_, _) => { if (profiles.SelectedItem is string value) BeginApply(new(ControlKind.PerformanceProfile, value)); };
        Hardware(performance, ControlKind.PerformanceProfile, applyProfile, "ApplyProfile");
        profileRow.Controls.Add(applyProfile);
        _current = TextLabel("Profile unavailable · Fans unavailable · Boost unavailable", _theme.Small);
        performance.Controls.Add(_current);
        Reason(performance, ControlKind.PerformanceProfile);
        _profileCard = WrapCard(performance, "PerformanceCard");

        var cooling = Stack(_theme.Surface, Padding.Empty);
        cooling.Controls.Add(TextLabel("Cooling", _theme.Heading));
        var modeRow = ActionRow(cooling);
        var modeLabel = TextLabel("Fan mode", _theme.Small); modeLabel.Margin = new Padding(0, 11, 12, 0); modeRow.Controls.Add(modeLabel);
        var fanMode = new ThemedComboBox(_theme) { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, AccessibleName = "Fan mode: Auto or Advanced using current curve", Margin = new Padding(0, 6, 10, 0) };
        fanMode.AccessibleDescription = "Mode only, not a factory reset. Auto follows firmware; Advanced uses the existing curve.";
        _tips.SetToolTip(fanMode, fanMode.AccessibleDescription);
        fanMode.Items.AddRange(new object[] { "Auto", "Advanced" }); fanMode.SelectedIndex = -1;
        fanMode.SelectedIndexChanged += (_, _) => { if (!_renderingState) _fanModeDirty = true; };
        Hardware(cooling, ControlKind.AutomaticFans, fanMode, "FanMode"); modeRow.Controls.Add(fanMode);
        var automatic = NativeButton("Apply mode");
        automatic.Click += (_, _) => { if (fanMode.SelectedItem is string value) BeginApply(new(ControlKind.AutomaticFans, value)); };
        Hardware(cooling, ControlKind.AutomaticFans, automatic, "AutomaticFans"); modeRow.Controls.Add(automatic);
        Reason(cooling, ControlKind.AutomaticFans);
        cooling.Controls.Add(TextLabel("Auto follows firmware. Advanced uses the current curve.", _theme.Small));
        var boostRow = ActionRow(cooling);
        var boost = new CheckBox { Text = "Cooler Boost", AutoSize = true, AccessibleName = "Proposed CoolerBoost", Margin = new Padding(0, 10, 16, 0) };
        boost.CheckedChanged += (_, _) => { if (!_renderingState) _boostDirty = true; };
        Hardware(cooling, ControlKind.CoolerBoost, boost, "Boost"); boostRow.Controls.Add(boost);
        var applyBoost = NativeButton("Apply boost");
        applyBoost.Click += (_, _) => BeginApply(new(ControlKind.CoolerBoost, boost.Checked ? "true" : "false"));
        Hardware(cooling, ControlKind.CoolerBoost, applyBoost, "ApplyBoost"); boostRow.Controls.Add(applyBoost);
        Reason(cooling, ControlKind.CoolerBoost);
        _tips.SetToolTip(boost, "Run fans at maximum effort. Select the checkbox, then Apply boost.");
        _coolingCard = WrapCard(cooling, "CoolingCard");

        _chartCard = new RoundedCard(_theme) { Name = "CurrentCurveCard" };
        var chartTitle = TextLabel("CPU fan curve", _theme.Heading); chartTitle.Dock = DockStyle.None; chartTitle.Location = new Point(18, 14);
        var edit = NativeButton("Edit curve →"); edit.AutoSize = false; edit.Size = new Size(112, 32); edit.Click += (_, _) => Navigate("Cooling");
        _chart = new FanCurveChart(_theme);
        _chartCard.Controls.AddRange(new Control[] { chartTitle, edit, _chart });
        _chartCard.Resize += (_, _) => { edit.Location = new Point(_chartCard.Width - D(130), D(10)); _chart.SetBounds(D(18), D(52), _chartCard.Width - D(36), _chartCard.Height - D(66)); };

        var editor = Stack(_theme.Surface, Padding.Empty);
        editor.Controls.Add(TextLabel("Tune CPU cooling", _theme.Heading));
        editor.Controls.Add(TextLabel("Raise fan effort at each fixed temperature. Values are percentages.", _theme.Small));
        var curveRow = ActionRow(editor);
        for (var i = 0; i < 6; i++)
        {
            var point = Stack(_theme.Surface, Padding.Empty); point.Dock = DockStyle.None; point.AutoSize = false; point.Height = D(60); point.Width = 70; point.Margin = new Padding(0, 8, 8, 8);
            var label = TextLabel("— °C", _theme.Small); label.Name = "CurveBreakpoint" + i; _curveBreakpoints[i] = label; point.Controls.Add(label);
            var input = new ThemedNumeric(_theme) { Width = 66, Minimum = 0, Maximum = 100, DecimalPlaces = 0, AccessibleName = "CPU curve point " + (i + 1) + " percentage" };
            input.ValueChanged += (_, _) => { if (!_renderingState) _curveDirty = true; };
            _curveInputs[i] = input; Hardware(editor, ControlKind.FanCurve, input, "CpuCurve" + i); point.Controls.Add(input); curveRow.Controls.Add(point);
        }
        curveRow.Resize += (_, _) => {
            var columns = curveRow.ClientSize.Width < D(400) ? 3 : 6;
            foreach (Control point in curveRow.Controls)
            {
                point.Width = Math.Max(D(64), curveRow.ClientSize.Width / columns - D(8));
                foreach (Control input in point.Controls) if (input is NumericUpDown) input.Width = point.Width;
            }
        };
        var curveActions = ActionRow(editor);
        var applyCurve = NativeButton("Apply CPU curve"); ((DashboardButton)applyCurve).Primary = true;
        applyCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, string.Join(',', _curveInputs.Select(input => ((int)input.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        Hardware(editor, ControlKind.FanCurve, applyCurve, "ApplyCurve"); curveActions.Controls.Add(applyCurve);
        var restoreCurve = NativeButton("Restore captured original");
        restoreCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, "Restore"));
        Hardware(editor, ControlKind.FanCurve, restoreCurve, "RestoreCurve"); curveActions.Controls.Add(restoreCurve);
        editor.Controls.Add(TextLabel("Restore returns the curve captured when this session started. Restore before quitting if you want those values back.", _theme.Small));
        Reason(editor, ControlKind.FanCurve);
        _editorCard = WrapCard(editor, "CurveEditorCard");

        _dashboard.Controls.AddRange(new Control[] { _sensorCard, _profileCard, _coolingCard, _chartCard, _editorCard });
        InitializeSettings();
        _viewport.Dock = DockStyle.Fill; _viewport.Controls.Add(_dashboard);
        Controls.Add(_viewport); Controls.Add(footer); Controls.Add(header);
        _viewport.SizeChanged += (_, _) => LayoutDashboard();
        _themeSelector.SelectedIndexChanged += (_, _) => { ApplyTheme(); if (!_settings.Write((string)_themeSelector.SelectedItem!)) _result.Text = "Appearance changed; preference could not be saved."; };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Shown += (_, _) => { PositionHeader(); ApplyTheme(true); LayoutDashboard(); refresh.Focus(); };
        Shown += (_, _) => StartupLoaded = LoadStartupAsync();
        PositionHeader(); ApplyTheme(); RefreshCapabilities(); LayoutDashboard();
    }
    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Color or Microsoft.Win32.UserPreferenceCategory.VisualStyle or Microsoft.Win32.UserPreferenceCategory.Accessibility)) return;
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => { if (!IsDisposed) ApplyTheme(); })); }
        catch (InvalidOperationException) { /* Handle was destroyed during shutdown. */ }
    }

    private void ApplyTheme(bool force = false)
    {
        var palette = ((string?)_themeSelector.SelectedItem == "Dark" || ((string?)_themeSelector.SelectedItem == "System" && ThemeSettings.SystemDark()), SystemInformation.HighContrast);
        if (!force && _appliedPalette == palette) return;
        _appliedPalette = palette;
        using var viewportUpdate = _viewport.PreserveViewport();
        _theme.Dark = palette.Item1;
        BackColor = _theme.Background; ForeColor = _theme.Ink;
        void Paint(Control root)
        {
            foreach (Control control in root.Controls)
            {
                control.BackColor = control == _viewport || control == _dashboard ? _theme.Background : _theme.Surface;
                control.ForeColor = control is Label label && label.Font.Size < _theme.Heading.Size ? _theme.Muted : _theme.Ink;
                if (control is Button button) button.UseVisualStyleBackColor = false;
                if (control is TextBox box) box.BorderStyle = BorderStyle.FixedSingle;
                NativeAppearance.Apply(control, _theme.Dark);
                if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
                Paint(control); control.Invalidate();
            }
        }
        NativeAppearance.Apply(this, _theme.Dark);
        Paint(this); _cpu.ForeColor = _gpu.ForeColor = _theme.Ink; _machine.ForeColor = _theme.Ink; _current.ForeColor = _theme.Accent;
        Invalidate(true);
    }

    private RoundedCard WrapCard(TableLayoutPanel content, string name)
    {
        var card = new RoundedCard(_theme) { Name = name };
        content.Dock = DockStyle.Fill; content.AutoSize = false; content.Padding = Padding.Empty; content.Margin = Padding.Empty;
        card.Controls.Add(content); return card;
    }

    private void Navigate(string page)
    {
        if (_page == page) return;
        _page = page;
        _result.Visible = page != "Settings"; _settingsHint.Visible = page == "Settings";
        foreach (var (name, button) in _navigation) { button.Selected = name == page; button.Invalidate(); }
        _viewport.AutoScrollPosition = Point.Empty; LayoutDashboard();
    }

    private void LayoutDashboard()
    {
        if (_dashboardLayout || _sensorCard is null) return;
        using var viewportUpdate = _viewport.PreserveViewport();
        _dashboardLayout = true;
        _viewport.SuspendLayout();
        _dashboard.SuspendLayout();
        try
        {
            var width = Math.Max(D(300), _viewport.ClientSize.Width - D(44));
            var gap = D(12);
            var wide = width >= D(900);
            _dashboard.SetBounds(D(22) + _viewport.AutoScrollPosition.X, D(12) + _viewport.AutoScrollPosition.Y, width, _dashboard.Height);
            _sensorCard.Visible = _profileCard.Visible = _coolingCard.Visible = _page == "Overview";
            _chartCard.Visible = _page == "Cooling" || (_page == "Overview" && wide);
            _editorCard.Visible = _page == "Cooling";
            _appearanceCard.Visible = _startupCard.Visible = _updateCard.Visible = _aboutCard.Visible = _page == "Settings";
            _chartCard.Controls.OfType<Button>().Single().Visible = _page == "Overview";
            int Fit(RoundedCard card, int x, int y, int w)
            {
                card.SetBounds(x, y, w, card.Height);
                var content = (TableLayoutPanel)card.Controls[0];
                content.Width = w - card.Padding.Horizontal;
                content.PerformLayout();
                card.Height = content.GetPreferredSize(new Size(content.Width, 0)).Height + card.Padding.Vertical;
                card.Visible = true;
                return card.Bottom + gap;
            }
            if (_page == "Overview")
            {
                var left = wide ? (width - gap) / 2 : width;
                _sensorCard.SetBounds(0, 0, left, _sensorCard.Height);
                _sensorCard.Height = _layoutSensors(); _sensorCard.Visible = true;
                var x = wide ? left + gap : 0;
                var y = Fit(_profileCard, x, wide ? 0 : _sensorCard.Bottom + gap, wide ? width - left - gap : width);
                Fit(_coolingCard, x, y, _profileCard.Width);
                if (wide) { _chartCard.SetBounds(0, _sensorCard.Bottom + gap, left, D(240)); _chartCard.Visible = true; }
            }
            else if (_page == "Cooling")
            {
                _chartCard.SetBounds(0, 0, width, D(240)); _chartCard.Visible = true;
                Fit(_editorCard, 0, _chartCard.Bottom + gap, width);
            }
            else
            {
                var y = Fit(_appearanceCard, 0, 0, width);
                y = Fit(_startupCard, 0, y, width);
                y = Fit(_updateCard, 0, y, width);
                Fit(_aboutCard, 0, y, width);
            }
            // Visible reports false for every descendant while the tray window is hidden.
            // Measure the selected page explicitly so background samples cannot collapse it.
            var cards = _page == "Overview"
                ? wide ? new[] { _sensorCard, _profileCard, _coolingCard, _chartCard } : new[] { _sensorCard, _profileCard, _coolingCard }
                : _page == "Cooling" ? new[] { _chartCard, _editorCard } : new[] { _appearanceCard, _startupCard, _updateCard, _aboutCard };
            _dashboard.Height = cards.Max(c => c.Bottom) + D(12);
        }
        finally { _dashboard.ResumeLayout(true); _viewport.ResumeLayout(true); _dashboardLayout = false; }
    }
    private TableLayoutPanel Stack(Color background, Padding padding) => new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Dock = DockStyle.Top, BackColor = background, Padding = padding, Margin = Padding.Empty,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) }
    };

    private Label TextLabel(string text, Font? font = null) => new BufferedLabel()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top, Font = font ?? _theme.Body,
        ForeColor = _theme.Muted, Margin = new Padding(0, 1, 0, 3), UseMnemonic = false
    };

    private Button NativeButton(string text) => new DashboardButton(_theme)
    {
        Text = text, AccessibleName = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(0, 32), Margin = new Padding(0, 4, 8, 5), UseVisualStyleBackColor = true
    };

    private static FlowLayoutPanel ActionRow(TableLayoutPanel section)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        section.Controls.Add(row);
        return row;
    }

    private void Hardware(TableLayoutPanel section, ControlKind kind, Control control, string name)
    {
        control.Name = "Hardware_" + name;
        control.Enabled = false;
        if (!_hardware.TryGetValue(kind, out var controls)) _hardware[kind] = controls = new();
        controls.Add(control);
        section.Controls.Add(control);
    }

    private void Reason(TableLayoutPanel section, ControlKind kind)
    {
        var label = TextLabel("Unavailable · Verification required.");
        label.Name = "Reason_" + kind;
        _reasons[kind] = label;
        section.Controls.Add(label);
    }

    private Capability CapabilityFor(ControlKind kind)
    {
        if (LastSnapshot is null || _bridge is not IControlCapabilityProvider provider)
            return new(kind, false, "Backend has no validated firmware control capability.");
        try { return provider.GetCapability(LastSnapshot, kind); }
        catch (Exception exception) { return new(kind, false, "Capability unavailable: " + exception.Message); }
    }

    private void RefreshCapabilities()
    {
        using var viewportUpdate = _viewport.PreserveViewport();
        var layoutChanged = false;
        foreach (var (kind, label) in _reasons)
        {
            var capability = CapabilityFor(kind);
            var available = capability.Kind == kind && capability.CanWrite && ValidState(kind) && !IsStale && !_suspended;
            // A safe background read need not disable a fresh focused draft input. Actions remain serialized.
            available &= kind is ControlKind.PerformanceProfile or ControlKind.CoolerBoost or ControlKind.AutomaticFans or ControlKind.FanCurve;
            var enabled = available && !IsApplying;
            if (_hardware.TryGetValue(kind, out var controls)) foreach (var control in controls) control.Enabled = enabled;
            var reason = kind == ControlKind.FanCurve && !ValidCpuCurveState()
                ? "A complete captured CPU table, unchanged fixed temperatures and preserved terminal cap are required. " + capability.Reason
                : capability.Reason;
            var text = IsApplying ? "Applying · Waiting for verification."
                : IsStale ? "Fresh sample required · Refresh to check firmware controls."
                : kind == ControlKind.FanCurve && !ValidCpuCurveState() ? "Read-only · A complete, verified CPU curve is required."
                : "Read-only · Firmware control is unavailable for this device.";
            var visibilityChanged = !_reasonVisibility.TryGetValue(kind, out var wasVisible) || wasVisible != !available;
            layoutChanged |= visibilityChanged || (!available && label.Text != text);
            _reasonVisibility[kind] = !available;
            label.Text = text;
            label.AccessibleDescription = reason;
            SetTip(label, reason);
            if (visibilityChanged) label.Visible = !available;
        }
        if (layoutChanged) LayoutDashboard();
    }

    private bool ValidState(ControlKind kind) => kind switch
    {
        ControlKind.PerformanceProfile => Profile(LastSnapshot?.Controls?.Performance) != "unavailable",
        ControlKind.CoolerBoost => LastSnapshot?.Controls?.Boost is not null,
        ControlKind.AutomaticFans => FanMode(LastSnapshot?.Controls?.FanMode) != "unavailable",
        ControlKind.FanCurve => ValidCpuCurveState(),
        _ => false
    };

    private static bool CompleteCpuCurve(HardwareControlState? state)
    {
        if (state?.CpuCurvePacket is not { Length: 8 } packet || packet[7] is < 20 or > 100
            || state.CpuCurve is not { Count: 6 } curve || state.CpuBreakpoints is not { Count: 6 } points) return false;
        for (var i = 0; i < 6; i++)
            if (curve[i] != packet[i + 1] || curve[i] < 20 || curve[i] > packet[7] || points[i] < 0 || points[i] > 100
                || (i > 0 && (curve[i] < curve[i - 1] || points[i] <= points[i - 1]))) return false;
        return true;
    }

    private bool ValidCpuCurveState()
    {
        var state = LastSnapshot?.Controls;
        return _originalCpuCurve is not null && _originalCpuBreakpoints is not null && CompleteCpuCurve(state)
            && state!.CpuBreakpoints!.SequenceEqual(_originalCpuBreakpoints)
            && state.CpuCurvePacket![7] == _originalCpuCurve[7]
            && state.CpuCurve!.Select((value, i) => value >= _originalCpuCurve[i + 1]).All(valid => valid);
    }

    private void RenderCpuCurve(HardwareControlState? state)
    {
        var capability = CapabilityFor(ControlKind.FanCurve);
        if (_originalCpuCurve is null && capability.Kind == ControlKind.FanCurve && capability.CanWrite && CompleteCpuCurve(state))
        {
            _originalCpuCurve = (byte[])state!.CpuCurvePacket!.Clone();
            _originalCpuBreakpoints = state.CpuBreakpoints!.ToArray();
        }
        if (!ValidCpuCurveState()) return;
        _renderingState = true;
        for (var i = 0; i < 6; i++)
        {
            _curveBreakpoints[i].Text = _originalCpuBreakpoints![i] + "°C · %";
            _curveInputs[i].Maximum = _originalCpuCurve![7];
            _curveInputs[i].Minimum = _originalCpuCurve[i + 1];
            if (!_curveDirty) _curveInputs[i].Value = state!.CpuCurve![i];
        }
        _renderingState = false;
    }

    private bool ValidCpuCurveRequest(ControlRequest request)
    {
        if (request.Value == "Restore") return true;
        try { _ = CoolingCurvePlan.Create(_originalCpuCurve!, request.Value); return true; }
        catch (ArgumentException) { return false; }
    }

    private void BeginApply(ControlRequest request)
    {
        if (IsApplying || IsDisposed) return;
        ActiveApply = ApplyControlAsync(request);
    }

    public void ShowOperationStatus(string text)
    {
        if (!IsDisposed) _result.Text = text;
    }

    public async Task ApplyControlAsync(ControlRequest request)
    {
        if (_applyQueued || IsApplying || IsDisposed || _shutdown.IsCancellationRequested) return;
        if (_polling)
        {
            _applyQueued = true;
            _result.Text = "Waiting for the current sample before applying…";
            try { await _activePoll; }
            finally { _applyQueued = false; }
        }
        UpdateFreshness(DateTimeOffset.UtcNow);
        var cap = CapabilityFor(request.Kind);
        if (IsDisposed || _shutdown.IsCancellationRequested || IsApplying || _polling || _suspended || IsStale || !ValidState(request.Kind) || !cap.CanWrite || cap.Kind != request.Kind) return;
        if (request.Kind == ControlKind.FanCurve && !ValidCpuCurveRequest(request))
        {
            _result.Text = "Curve not applied · Use six nondecreasing percentages within the captured-original bounds.";
            return;
        }
        IsApplying = true;
        _applyError = null;
        RefreshCapabilities();
        _result.Text = "Applying; waiting for backend verification…";
        var token = _shutdown.Token;
        try
        {
            var result = await _bridge.ApplyAsync(request, token);
            if (IsDisposed || token.IsCancellationRequested) return;
            if (request.Kind == ControlKind.PerformanceProfile) _profileDirty = false;
            if (request.Kind == ControlKind.CoolerBoost) _boostDirty = false;
            if (request.Kind == ControlKind.AutomaticFans) _fanModeDirty = false;
            if (request.Kind == ControlKind.FanCurve) _curveDirty = false;
            // Poll even a failed transaction: the backend may have rolled back after a write.
            await StartPoll(true);
            if (IsDisposed || token.IsCancellationRequested) return;
            _result.Text = (result.Applied ? "Backend verified · " : "Apply failed · ") + result.Message;
            if (result.Applied && (IsStale || !ReadbackMatches(request)))
            {
                LastError = _applyError = IsStale ? "UI readback unavailable; Refresh required." : "UI readback mismatch; requested value was not observed.";
                _result.Text = _applyError + " · Backend response: " + result.Message;
            }
            if (!result.Applied) LastError = _applyError = result.Message;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                RuntimeLog.Write("Apply", exception);
                LastError = _applyError = exception.Message;
                _result.Text = "Apply failed · " + exception.Message;
                MarkStale("Transaction state unknown; Refresh required");
            }
        }
        finally
        {
            IsApplying = false;
            if (!IsDisposed) { RefreshCapabilities(); ApplyCompleted?.Invoke(this, EventArgs.Empty); }
        }
    }

    private bool ReadbackMatches(ControlRequest request) => request.Kind switch
    {
        ControlKind.PerformanceProfile => Profile(LastSnapshot?.Controls?.Performance) == request.Value,
        ControlKind.CoolerBoost => LastSnapshot?.Controls?.Boost is byte boost && ((boost & 0x80) != 0) == (request.Value == "true"),
        ControlKind.AutomaticFans => FanMode(LastSnapshot?.Controls?.FanMode) == request.Value,
        ControlKind.FanCurve => LastSnapshot?.Controls?.CpuCurvePacket is byte[] packet && CoolingCurvePlan.SameStableFields(packet,request.Value == "Restore" ? _originalCpuCurve! : CoolingCurvePlan.Create(_originalCpuCurve!, request.Value)) && ValidCpuCurveState(),
        _ => false
    };

    public async Task ExportDiagnosticsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        UpdateFreshness(DateTimeOffset.UtcNow);
        var capabilities = Enum.GetValues<ControlKind>().Select(CapabilityFor).ToArray();
        var report = new
        {
            Application = "QuietMSI", ReadOnly = !capabilities.Any(cap => cap.CanWrite), ExportedAt = DateTimeOffset.UtcNow,
            IsStale, LastError, Snapshot = LastSnapshot,
            Capabilities = capabilities
        };
        var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, _shutdown.Token);
    }

    public Task PollAsync() => StartPoll(false);

    private Task StartPoll(bool readback)
    {
        if (_polling) return _activePoll;
        return _activePoll = PollAsync(readback);
    }

    private void SetTip(Control control, string? text)
    {
        if (_tips.GetToolTip(control) != text) _tips.SetToolTip(control, text);
    }

    private async Task PollAsync(bool readback)
    {
        if (_polling || (IsApplying && !readback) || _suspended || IsDisposed || _shutdown.IsCancellationRequested) return;
        _polling = true;
        var generation = _sampleGeneration;
        var token = _shutdown.Token;
        try
        {
            var snapshot = await _bridge.ReadSnapshotAsync(token);
            if (IsDisposed || token.IsCancellationRequested || _suspended || generation != _sampleGeneration) return;
            using var viewportUpdate = _viewport.PreserveViewport();
            LastSnapshot = snapshot;
            LastError = _applyError;
            IsStale = DateTimeOffset.UtcNow - snapshot.Timestamp > TimeSpan.FromSeconds(45);
            _machine.Text = snapshot.Identity.Manufacturer == "Micro-Star International Co., Ltd." ? snapshot.Identity.Model : $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model}";
            _machine.AccessibleDescription = $"BIOS {snapshot.Identity.Bios} · EC {snapshot.Identity.EcFirmware ?? "unidentified"}";
            SetTip(_machine, _machine.AccessibleDescription);
            _sampling.Text = $"{(_bridge is IControlCapabilityProvider ? "Live" : "Read-only")} · {(IsStale ? "Stale sample" : "Updated")} {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";
            _current.Text = $"Profile {Profile(snapshot.Controls?.Performance)} · Fans {FanMode(snapshot.Controls?.FanMode)} · Boost {(snapshot.Controls?.Boost is byte boost ? ((boost & 0x80) != 0 ? "On" : "Off") : "unavailable")}";
            _renderingState = true;
            if (!_profileDirty && snapshot.Controls?.Performance is byte performance)
            {
                var selector = (ProfileSelector)_hardware[ControlKind.PerformanceProfile][0];
                selector.SelectedItem = Profile(performance);
            }
            if (!_boostDirty && snapshot.Controls?.Boost is byte boostState)
                ((CheckBox)_hardware[ControlKind.CoolerBoost][0]).Checked = (boostState & 0x80) != 0;
            if (!_fanModeDirty && snapshot.Controls?.FanMode is byte modeState)
                ((ComboBox)_hardware[ControlKind.AutomaticFans][0]).SelectedItem = FanMode(modeState);
            _renderingState = false;
            RenderCpuCurve(snapshot.Controls);
            var layoutChanged = (_cpu.Text == "Unavailable") != (Temperature(snapshot.CpuCelsius) == "Unavailable")
                || (_gpu.Text == "Unavailable") != (Temperature(snapshot.GpuCelsius) == "Unavailable")
                || _cpuFan.Text != Fan("Fan 1", snapshot.CpuFan) || _gpuFan.Text != Fan("Fan 2", snapshot.GpuFan);
            _cpu.Text = Temperature(snapshot.CpuCelsius);
            _gpu.Text = Temperature(snapshot.GpuCelsius);
            _cpuFan.Text = Fan("Fan 1", snapshot.CpuFan);
            _gpuFan.Text = Fan("Fan 2", snapshot.GpuFan);
            _cpuFan.AccessibleDescription = snapshot.CpuFan.Source; _gpuFan.AccessibleDescription = snapshot.GpuFan.Source;
            SetTip(_cpuFan, snapshot.CpuFan.Source); SetTip(_gpuFan, snapshot.GpuFan.Source);
            _gpuFan.Visible = snapshot.GpuFan.Rpm is not null || snapshot.GpuFan.TargetPercent is not null;
            _battery.Text = $"Battery · {(snapshot.BatteryPercent is >= 0 and <= 100 ? $"{snapshot.BatteryPercent}%" : "Unavailable")}  |  Power · {(snapshot.OnAcPower is true ? "AC" : snapshot.OnAcPower is false ? "Battery" : "Unavailable")}";
            _chart.SetData(snapshot.Controls);
            if (layoutChanged && _page == "Overview") LayoutDashboard();
            _sampling.AccessibleDescription = snapshot.Status;
            _cpu.AccessibleDescription = snapshot.Status;
            _gpu.AccessibleDescription = snapshot.GpuTemperatureSource ?? snapshot.Status;
            SetTip(_cpu, snapshot.Status);
            SetTip(_gpu, _gpu.AccessibleDescription);
            SetTip(_sampling, snapshot.Status);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                if (LastError != exception.Message) RuntimeLog.Write("Sample", exception);
                LastError = exception.Message;
                MarkStale("Sample failed: " + exception.Message);
                _result.Text = "Refresh failed · " + exception.Message;
            }
        }
        finally { _polling = false; if (!IsDisposed) RefreshCapabilities(); }
    }

    public void UpdateFreshness(DateTimeOffset now)
    {
        if (IsDisposed) return;
        if (LastSnapshot is not null && now - LastSnapshot.Timestamp > TimeSpan.FromSeconds(45))
            MarkStale($"Last sample {LastSnapshot.Timestamp.ToLocalTime():HH:mm:ss}; awaiting fresh data");
    }

    public void SetSuspended(bool suspended)
    {
        if (IsDisposed) return;
        _suspended = suspended;
        _sampleGeneration++;
        MarkStale(suspended ? "Sampling paused while Windows is suspended" : "Windows resumed; waiting for a fresh sample");
    }

    private void MarkStale(string reason)
    {
        using var viewportUpdate = _viewport.PreserveViewport();
        IsStale = true;
        _sampling.Text = "Read-only · Stale · " + reason;
        RefreshCapabilities();
    }

    private static string Profile(byte? value) => value switch { 0xc0 => "High", 0xc1 => "Medium", 0xc4 => "Turbo", 0xc2 => "Eco", _ => "unavailable" };
    private static string FanMode(byte? value) => value switch { 0x0d => "Auto", 0x8d => "Advanced", 0x1d => "Silent", _ => "unavailable" };

    private static string Temperature(double? value) => TelemetryRules.Temperature(value) is double valid ? $"{valid:F1} °C" : "Unavailable";

    private static string Fan(string name, FanReading reading)
    {
        var measured = reading.Rpm is >= 0 and <= 30000 ? $"{reading.Rpm} RPM reported" : "Measured RPM unavailable";
        var target = reading.TargetPercent is >= 0 and <= 150 ? $" · Target {reading.TargetPercent:0.#}% (not RPM)" : "";
        return $"{name} · {measured}{target}";
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
