using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed class MainForm : Form
{
    private readonly Theme _theme = new();
    private readonly IHardwareBridge _bridge;
    private readonly Label _machine;
    private readonly Label _identity;
    private readonly Label _sampling;
    private readonly Label _current;
    private readonly Label _result;
    private readonly Label _cpu;
    private readonly Label _gpu;
    private readonly Label _cpuFan;
    private readonly Label _gpuFan;
    private readonly Label _battery;
    private readonly Label _source;
    private readonly Label _curve;
    private readonly NumericUpDown[] _curveInputs = new NumericUpDown[6];
    private readonly Label[] _curveBreakpoints = new Label[6];
    private byte[]? _originalCpuCurve;
    private int[]? _originalCpuBreakpoints;
    private bool _curveDirty;
    private readonly TextBox _diagnostics;
    private readonly Dictionary<ControlKind, List<Control>> _hardware = new();
    private readonly Dictionary<ControlKind, Label> _reasons = new();
    private readonly CancellationTokenSource _shutdown = new();
    private bool _polling;
    private bool _resourcesDisposed;
    private bool _suspended;
    private int _sampleGeneration;
    private string? _applyError;
    private bool _profileDirty;
    private bool _boostDirty;
    private bool _fanModeDirty;
    private bool _renderingState;
    public bool IsApplying { get; private set; }
    public Task ActiveApply { get; private set; } = Task.CompletedTask;
    public event EventHandler? ApplyCompleted;
    public HardwareSnapshot? LastSnapshot { get; private set; }
    public string? LastError { get; private set; }
    public bool IsStale { get; private set; } = true;

    public MainForm(IHardwareBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        Text = "QuietMSI";
        AccessibleName = "QuietMSI laptop telemetry and hardware capability status";
        Font = _theme.Body;
        BackColor = _theme.Background;
        ForeColor = _theme.Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 740);
        MinimumSize = new Size(440, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;

        var header = Stack(_theme.Header, new Padding(18, 10, 18, 10));
        header.Dock = DockStyle.Top;
        _machine = TextLabel("Machine identity unavailable", _theme.Heading);
        _identity = TextLabel("BIOS unavailable · EC firmware unidentified");
        _sampling = TextLabel("Read-only · Waiting for first sample");
        foreach (var label in new[] { _machine, _identity, _sampling })
        {
            label.ForeColor = _theme.HeaderInk;
            header.Controls.Add(label);
        }
        _current = TextLabel("Profile unavailable · Fans unavailable · Boost unavailable");
        _current.ForeColor = _theme.HeaderInk;
        header.Controls.Add(_current);
        _result = TextLabel("Changes require an explicit Apply.");
        _result.Name = "ApplyResult";
        _result.ForeColor = _theme.HeaderInk;
        header.Controls.Add(_result);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = _theme.Background, Padding = new Padding(18, 14, 18, 16), AccessibleName = "Telemetry and settings", TabStop = false };
        var body = Stack(_theme.Background, Padding.Empty);
        body.Dock = DockStyle.Top;
        scroll.Controls.Add(body);
        header.TabIndex = 0;
        scroll.TabIndex = 1;
        Controls.Add(scroll);
        Controls.Add(header);

        var telemetry = Section(body, "Live telemetry");
        var temperatures = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 4, 0, 8) };
        temperatures.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        temperatures.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _cpu = Reading(temperatures, 0, "CPU temperature");
        _gpu = Reading(temperatures, 1, "GPU temperature");
        telemetry.Controls.Add(temperatures);
        _cpuFan = TextLabel("Fan 1 · Unavailable\nSource: unavailable");
        _gpuFan = TextLabel("Fan 2 · Unavailable\nSource: unavailable");
        _battery = TextLabel("Battery · Unavailable  |  Power source · Unavailable");
        _source = TextLabel("Temperature source: unavailable. No sample received.");
        foreach (var label in new[] { _cpuFan, _gpuFan, _battery }) telemetry.Controls.Add(label);

        var performance = Section(body, "Performance");
        var profileRow = ActionRow(performance);
        var profiles = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, AccessibleName = "Performance profile", Margin = new Padding(0, 5, 8, 0) };
        profiles.Items.AddRange(new object[] { "High", "Medium", "Turbo", "Eco" });
        profiles.SelectedIndex = -1;
        profiles.SelectedIndexChanged += (_, _) => { if (!_renderingState) _profileDirty = true; };
        Hardware(performance, ControlKind.PerformanceProfile, profiles, "Profile");
        profileRow.Controls.Add(profiles);
        var applyProfile = NativeButton("Apply profile");
        applyProfile.Click += (_, _) => { if (profiles.SelectedItem is string value) BeginApply(new(ControlKind.PerformanceProfile, value)); };
        Hardware(performance, ControlKind.PerformanceProfile, applyProfile, "ApplyProfile");
        profileRow.Controls.Add(applyProfile);
        Reason(performance, ControlKind.PerformanceProfile);

        var cooling = Section(body, "Cooling");
        var boostRow = ActionRow(cooling);
        var boost = new CheckBox { Text = "CoolerBoost", AutoSize = true, AccessibleName = "Proposed CoolerBoost" };
        boost.CheckedChanged += (_, _) => { if (!_renderingState) _boostDirty = true; };
        Hardware(cooling, ControlKind.CoolerBoost, boost, "Boost");
        boost.Margin = new Padding(0, 8, 8, 0);
        boostRow.Controls.Add(boost);
        var applyBoost = NativeButton("Apply CoolerBoost");
        applyBoost.Click += (_, _) => BeginApply(new(ControlKind.CoolerBoost, boost.Checked ? "true" : "false"));
        Hardware(cooling, ControlKind.CoolerBoost, applyBoost, "ApplyBoost");
        boostRow.Controls.Add(applyBoost);
        Reason(cooling, ControlKind.CoolerBoost);
        var modeRow = ActionRow(cooling);
        var fanMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, AccessibleName = "Fan mode: Auto or Advanced using current curve", Margin = new Padding(0, 5, 8, 0) };
        fanMode.Items.AddRange(new object[] { "Auto", "Advanced" });
        fanMode.SelectedIndex = 0;
        fanMode.SelectedIndexChanged += (_, _) => { if (!_renderingState) _fanModeDirty = true; };
        Hardware(cooling, ControlKind.AutomaticFans, fanMode, "FanMode");
        modeRow.Controls.Add(fanMode);
        var automatic = NativeButton("Apply fan mode");
        automatic.Click += (_, _) => { if (fanMode.SelectedItem is string value) BeginApply(new(ControlKind.AutomaticFans, value)); };
        Hardware(cooling, ControlKind.AutomaticFans, automatic, "AutomaticFans");
        modeRow.Controls.Add(automatic);
        Reason(cooling, ControlKind.AutomaticFans);
        cooling.Controls.Add(TextLabel("Advanced uses the existing curve; Auto hands control to firmware. Mode only, not a factory reset."));
        _curve = TextLabel("Current curve · Unavailable");
        _curve.AccessibleName = "Current fan curve, read-only";
        cooling.Controls.Add(_curve);
        cooling.Controls.Add(TextLabel("CPU curve · Increases only from this session's captured original.\nFixed temperatures; GPU table and fan mode are unchanged."));
        var curveRow = ActionRow(cooling);
        for (var i = 0; i < 6; i++)
        {
            var point = Stack(_theme.Surface, Padding.Empty);
            point.Dock = DockStyle.None;
            point.Width = 94;
            point.Margin = new Padding(0, 0, 8, 4);
            var label = TextLabel("Temperature unavailable");
            label.Name = "CurveBreakpoint" + i;
            _curveBreakpoints[i] = label;
            point.Controls.Add(label);
            var input = new NumericUpDown { Width = 84, Minimum = 0, Maximum = 100, DecimalPlaces = 0, AccessibleName = "CPU curve point " + (i + 1) + " percentage" };
            input.ValueChanged += (_, _) => { if (!_renderingState) _curveDirty = true; };
            _curveInputs[i] = input;
            Hardware(cooling, ControlKind.FanCurve, input, "CpuCurve" + i);
            point.Controls.Add(input);
            curveRow.Controls.Add(point);
        }
        var curveActions = ActionRow(cooling);
        var applyCurve = NativeButton("Apply CPU curve");
        applyCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, string.Join(',', _curveInputs.Select(input => ((int)input.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        Hardware(cooling, ControlKind.FanCurve, applyCurve, "ApplyCurve");
        curveActions.Controls.Add(applyCurve);
        var restoreCurve = NativeButton("Restore captured original");
        restoreCurve.Click += (_, _) => BeginApply(new(ControlKind.FanCurve, "Restore"));
        Hardware(cooling, ControlKind.FanCurve, restoreCurve, "RestoreCurve");
        curveActions.Controls.Add(restoreCurve);
        Reason(cooling, ControlKind.FanCurve);

        var evidence = Section(body, "Safety & diagnostics");
        evidence.Controls.Add(_source);
        evidence.Controls.Add(TextLabel("No changes on startup, selection, resume or exit. Only Apply sends a request."));
        _diagnostics = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 86, Dock = DockStyle.Top, Text = "No sample received. Diagnostics will appear here.", AccessibleName = "Provider diagnostics", BackColor = _theme.Surface, ForeColor = _theme.Ink };
        evidence.Controls.Add(_diagnostics);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        var refresh = NativeButton("&Refresh");
        refresh.Click += async (_, _) => await PollAsync();
        refresh.TabIndex = 0;
        var export = NativeButton("Export &diagnostics…");
        export.ForeColor = _theme.Accent;
        export.Click += async (_, _) => await ChooseDiagnosticsPathAsync();
        refresh.ForeColor = _theme.Ink;
        header.Controls.Add(refresh);
        Shown += (_, _) => refresh.Focus();
        actions.Controls.Add(export);
        evidence.Controls.Add(actions);
        evidence.Controls.Add(TextLabel("Close or minimize keeps QuietMSI in the tray. Use tray → Exit to quit."));
        RefreshCapabilities();
    }

    private TableLayoutPanel Stack(Color background, Padding padding) => new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Dock = DockStyle.Top, BackColor = background, Padding = padding, Margin = Padding.Empty,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) }
    };

    private TableLayoutPanel Section(TableLayoutPanel body, string title)
    {
        var section = Stack(_theme.Surface, new Padding(12, 8, 12, 8));
        section.Margin = new Padding(0, 0, 0, 8);
        section.Controls.Add(TextLabel(title, _theme.Heading));
        body.Controls.Add(section);
        return section;
    }

    private Label TextLabel(string text, Font? font = null) => new()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top, Font = font ?? _theme.Body,
        ForeColor = _theme.Muted, Margin = new Padding(0, 1, 0, 3), UseMnemonic = false,
        AccessibleName = text
    };

    private Label Reading(TableLayoutPanel row, int column, string title)
    {
        var group = Stack(_theme.Surface, Padding.Empty);
        group.Controls.Add(TextLabel(title));
        var value = TextLabel("Unavailable", _theme.Reading);
        value.ForeColor = _theme.Ink;
        value.AccessibleName = title;
        group.Controls.Add(value);
        row.Controls.Add(group, column, 0);
        return value;
    }

    private Button NativeButton(string text) => new()
    {
        Text = text, AccessibleName = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(0, 32), Margin = new Padding(0, 4, 0, 5), UseVisualStyleBackColor = true
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
        foreach (var (kind, label) in _reasons)
        {
            var capability = CapabilityFor(kind);
            var enabled = capability.Kind == kind && capability.CanWrite && ValidState(kind) && !IsStale && !_polling && !IsApplying && !_suspended;
            // Only expose implemented, state-validated Apply workflows.
            enabled &= kind is ControlKind.PerformanceProfile or ControlKind.CoolerBoost or ControlKind.AutomaticFans or ControlKind.FanCurve;
            if (_hardware.TryGetValue(kind, out var controls)) foreach (var control in controls) control.Enabled = enabled;
            var reason = kind == ControlKind.FanCurve && !ValidCpuCurveState()
                ? "A complete captured CPU table, unchanged fixed temperatures and preserved terminal cap are required. " + capability.Reason
                : capability.Reason;
            label.Text = (IsApplying ? "Applying · " : IsStale ? "Fresh sample required · " : enabled ? "Ready · " : "Read-only · ") + reason;
            label.Visible = !enabled;
        }
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
            await PollAsync(true);
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

    private async Task ChooseDiagnosticsPathAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export QuietMSI diagnostics", Filter = "JSON diagnostics (*.json)|*.json",
            FileName = "QuietMSI-diagnostics.json", DefaultExt = "json", AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await ExportDiagnosticsAsync(dialog.FileName);
            if (!IsDisposed) MessageBox.Show(this, "Diagnostics exported. Review device identity before sharing this file.", "QuietMSI", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed) MessageBox.Show(this, "Could not export diagnostics: " + exception.Message, "QuietMSI", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public Task PollAsync() => PollAsync(false);

    private async Task PollAsync(bool readback)
    {
        if (_polling || (IsApplying && !readback) || _suspended || IsDisposed || _shutdown.IsCancellationRequested) return;
        _polling = true;
        RefreshCapabilities();
        var generation = _sampleGeneration;
        var token = _shutdown.Token;
        try
        {
            var snapshot = await _bridge.ReadSnapshotAsync(token);
            if (IsDisposed || token.IsCancellationRequested || _suspended || generation != _sampleGeneration) return;
            LastSnapshot = snapshot;
            LastError = _applyError;
            IsStale = DateTimeOffset.UtcNow - snapshot.Timestamp > TimeSpan.FromSeconds(45);
            _machine.Text = $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model}";
            _identity.Text = $"BIOS {snapshot.Identity.Bios} · EC {snapshot.Identity.EcFirmware ?? "unidentified"}";
            _sampling.Text = $"{(_bridge is IControlCapabilityProvider ? "Backend capability gated" : "Read-only")} · {(IsStale ? "Stale sample" : "Last sample")} {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";
            _current.Text = $"Profile {Profile(snapshot.Controls?.Performance)} · Fans {FanMode(snapshot.Controls?.FanMode)} · Boost {(snapshot.Controls?.Boost is byte boost ? ((boost & 0x80) != 0 ? "On" : "Off") : "unavailable")}";
            _renderingState = true;
            if (!_profileDirty && snapshot.Controls?.Performance is byte performance)
            {
                var selector = (ComboBox)_hardware[ControlKind.PerformanceProfile][0];
                selector.SelectedItem = Profile(performance);
            }
            if (!_boostDirty && snapshot.Controls?.Boost is byte boostState)
                ((CheckBox)_hardware[ControlKind.CoolerBoost][0]).Checked = (boostState & 0x80) != 0;
            if (!_fanModeDirty && snapshot.Controls?.FanMode is byte modeState)
                ((ComboBox)_hardware[ControlKind.AutomaticFans][0]).SelectedItem = FanMode(modeState);
            _renderingState = false;
            var curve = snapshot.Controls?.CpuCurve;
            var points = snapshot.Controls?.CpuBreakpoints;
            _curve.Text = curve is not null && points is not null && curve.Count == points.Count && curve.Count > 0
                ? "Current CPU curve · " + string.Join(" · ", points.Zip(curve, (temperature, target) => $"{temperature}°C → {target}%"))
                : "Current CPU curve · Unavailable";
            RenderCpuCurve(snapshot.Controls);
            _cpu.Text = Temperature(snapshot.CpuCelsius);
            _gpu.Text = Temperature(snapshot.GpuCelsius);
            _cpu.Font = _cpu.Text == "Unavailable" ? _theme.Heading : _theme.Reading;
            _gpu.Font = _gpu.Text == "Unavailable" ? _theme.Heading : _theme.Reading;
            _cpuFan.Text = Fan("Fan 1", snapshot.CpuFan);
            _gpuFan.Text = Fan("Fan 2", snapshot.GpuFan);
            _gpuFan.Visible = snapshot.GpuFan.Rpm is not null || snapshot.GpuFan.TargetPercent is not null;
            _battery.Text = $"Battery · {(snapshot.BatteryPercent is >= 0 and <= 100 ? $"{snapshot.BatteryPercent}%" : "Unavailable")}  |  Power · {(snapshot.OnAcPower is true ? "AC" : snapshot.OnAcPower is false ? "Battery" : "Unavailable")}";
            _source.Text = "Provider / temperature status: " + snapshot.Status;
            _diagnostics.Text = snapshot.Diagnostics.Count == 0 ? snapshot.Status : string.Join(Environment.NewLine, snapshot.Diagnostics);
            RefreshCapabilities();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                LastError = exception.Message;
                MarkStale("Sample failed: " + exception.Message);
                _diagnostics.Text = "Read failed: " + exception.Message + Environment.NewLine + (readback ? "Apply returned, but UI readback failed. Inspect the backend result; Refresh before another change." : "This refresh sent no hardware command. Try Refresh or export diagnostics.");
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
        return $"{name} · {measured}{target}\nSource: {reading.Source}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _shutdown.Cancel();
            _shutdown.Dispose();
            base.Dispose(disposing);
            _theme.Dispose();
            return;
        }
        base.Dispose(disposing);
    }
}
