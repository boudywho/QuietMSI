using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed class MainForm : Form
{
    private readonly Theme _theme = new();
    private readonly IHardwareBridge _bridge;
    private readonly Label _machine;
    private readonly Label _identity;
    private readonly Label _sampling;
    private readonly Label _cpu;
    private readonly Label _gpu;
    private readonly Label _cpuFan;
    private readonly Label _gpuFan;
    private readonly Label _battery;
    private readonly Label _source;
    private readonly TextBox _diagnostics;
    private readonly Dictionary<ControlKind, List<Control>> _hardware = new();
    private readonly Dictionary<ControlKind, Label> _reasons = new();
    private readonly CancellationTokenSource _shutdown = new();
    private bool _polling;
    private bool _resourcesDisposed;
    private bool _suspended;
    private int _sampleGeneration;
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

        var header = Stack(_theme.Header, new Padding(22, 16, 22, 16));
        header.Dock = DockStyle.Top;
        var title = TextLabel("QuietMSI", _theme.Title);
        title.ForeColor = _theme.HeaderInk;
        header.Controls.Add(title);
        _machine = TextLabel("Machine identity unavailable", _theme.Heading);
        _identity = TextLabel("BIOS unavailable · EC firmware unidentified");
        _sampling = TextLabel("Read-only · Waiting for first sample");
        foreach (var label in new[] { _machine, _identity, _sampling })
        {
            label.ForeColor = _theme.HeaderInk;
            header.Controls.Add(label);
        }

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
        _cpuFan = TextLabel("CPU fan · Unavailable\nSource: unavailable");
        _gpuFan = TextLabel("GPU fan · Unavailable\nSource: unavailable");
        _battery = TextLabel("Battery · Unavailable  |  Power source · Unavailable");
        _source = TextLabel("Temperature source: unavailable. No sample received.");
        foreach (var label in new[] { _cpuFan, _gpuFan, _battery, _source }) telemetry.Controls.Add(label);

        var performance = Section(body, "Performance");
        performance.Controls.Add(TextLabel("Performance profile"));
        var profiles = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Performance profile" };
        profiles.Items.AddRange(new object[] { "Unavailable — no verified profile mapping", "Silent", "Balanced", "Extreme performance" });
        profiles.SelectedIndex = 0;
        Hardware(performance, ControlKind.PerformanceProfile, profiles, "Profile");
        Hardware(performance, ControlKind.PerformanceProfile, NativeButton("Apply profile"), "ApplyProfile");
        Reason(performance, ControlKind.PerformanceProfile);

        var cooling = Section(body, "Cooling");
        Hardware(cooling, ControlKind.CoolerBoost, new CheckBox { Text = "Cooler Boost", AutoSize = true, AccessibleName = "Cooler Boost" }, "Boost");
        Hardware(cooling, ControlKind.CoolerBoost, NativeButton("Apply Cooler Boost"), "ApplyBoost");
        Reason(cooling, ControlKind.CoolerBoost);
        cooling.Controls.Add(TextLabel("Fan curve · Temperature / fan target"));
        Hardware(cooling, ControlKind.FanCurve, new TextBox { Text = "Unavailable — curve payload not verified", ReadOnly = true, Dock = DockStyle.Top, AccessibleName = "Fan curve" }, "Curve");
        Hardware(cooling, ControlKind.FanCurve, NativeButton("Apply fan curve"), "ApplyCurve");
        Reason(cooling, ControlKind.FanCurve);
        Hardware(cooling, ControlKind.AutomaticFans, NativeButton("Return fans to BIOS automatic"), "AutomaticFans");
        Reason(cooling, ControlKind.AutomaticFans);

        var charge = Section(body, "Battery care");
        charge.Controls.Add(TextLabel("Charge limit · Percent"));
        var limit = new NumericUpDown { Minimum = 50, Maximum = 100, Value = 80, Width = 90, AccessibleName = "Proposed charge limit, not current hardware value" };
        Hardware(charge, ControlKind.ChargeLimit, limit, "ChargeLimit");
        charge.Controls.Add(TextLabel("Proposed value only; current hardware limit is unavailable."));
        Hardware(charge, ControlKind.ChargeLimit, NativeButton("Apply charge limit"), "ApplyCharge");
        Reason(charge, ControlKind.ChargeLimit);

        var evidence = Section(body, "Safety & diagnostics");
        evidence.Controls.Add(TextLabel("Hardware changes are locked. No command is sent on startup, selection, resume or exit."));
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
        RefreshCapabilities(new DeviceIdentity("Unknown", "Unknown", "Unknown", null));
    }

    private TableLayoutPanel Stack(Color background, Padding padding) => new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Dock = DockStyle.Top, BackColor = background, Padding = padding, Margin = Padding.Empty,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) }
    };

    private TableLayoutPanel Section(TableLayoutPanel body, string title)
    {
        var section = Stack(_theme.Surface, new Padding(16, 12, 16, 12));
        section.Margin = new Padding(0, 0, 0, 12);
        section.Controls.Add(TextLabel(title, _theme.Heading));
        body.Controls.Add(section);
        return section;
    }

    private Label TextLabel(string text, Font? font = null) => new()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top, Font = font ?? _theme.Body,
        ForeColor = _theme.Muted, Margin = new Padding(0, 3, 0, 5), UseMnemonic = false,
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

    private void RefreshCapabilities(DeviceIdentity identity)
    {
        foreach (var (kind, controls) in _hardware)
        {
            var capability = HardwarePolicy.GetCapability(identity, kind);
            // This UI release has no write handlers or validated payload editor.
            foreach (var control in controls) control.Enabled = false;
            _reasons[kind].Text = "Locked · " + capability.Reason;
        }
    }

    public async Task ExportDiagnosticsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        UpdateFreshness(DateTimeOffset.UtcNow);
        var identity = LastSnapshot?.Identity ?? new DeviceIdentity("Unknown", "Unknown", "Unknown", null);
        var report = new
        {
            Application = "QuietMSI", ReadOnly = true, ExportedAt = DateTimeOffset.UtcNow,
            IsStale, LastError, Snapshot = LastSnapshot,
            Capabilities = Enum.GetValues<ControlKind>().Select(kind => HardwarePolicy.GetCapability(identity, kind)).ToArray()
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

    public async Task PollAsync()
    {
        if (_polling || _suspended || IsDisposed || _shutdown.IsCancellationRequested) return;
        _polling = true;
        var generation = _sampleGeneration;
        var token = _shutdown.Token;
        try
        {
            var snapshot = await _bridge.ReadSnapshotAsync(token);
            if (IsDisposed || token.IsCancellationRequested || _suspended || generation != _sampleGeneration) return;
            LastSnapshot = snapshot;
            LastError = null;
            IsStale = DateTimeOffset.UtcNow - snapshot.Timestamp > TimeSpan.FromSeconds(45);
            _machine.Text = $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model}";
            _identity.Text = $"BIOS {snapshot.Identity.Bios} · EC {snapshot.Identity.EcFirmware ?? "unidentified"}";
            _sampling.Text = $"Read-only · {(IsStale ? "Stale sample" : "Last sample")} {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";
            _cpu.Text = Temperature(snapshot.CpuCelsius);
            _gpu.Text = Temperature(snapshot.GpuCelsius);
            _cpuFan.Text = Fan("CPU fan", snapshot.CpuFan);
            _gpuFan.Text = Fan("GPU fan", snapshot.GpuFan);
            _battery.Text = $"Battery · {(snapshot.BatteryPercent is >= 0 and <= 100 ? $"{snapshot.BatteryPercent}%" : "Unavailable")}  |  Power · {(snapshot.OnAcPower is true ? "AC" : snapshot.OnAcPower is false ? "Battery" : "Unavailable")}";
            _source.Text = "Provider / temperature status: " + snapshot.Status;
            _diagnostics.Text = snapshot.Diagnostics.Count == 0 ? snapshot.Status : string.Join(Environment.NewLine, snapshot.Diagnostics);
            RefreshCapabilities(snapshot.Identity);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                LastError = exception.Message;
                MarkStale("Sample failed: " + exception.Message);
                _diagnostics.Text = "Read failed: " + exception.Message + Environment.NewLine + "No hardware command was sent. Try Refresh or export diagnostics.";
            }
        }
        finally { _polling = false; }
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
    }

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
