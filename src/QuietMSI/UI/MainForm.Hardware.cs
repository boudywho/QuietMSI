using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed partial class MainForm
{
    private void RenderCurveDraftStatus()
    {
        var valid = ValidCpuCurveState();
        var text = !valid ? "Waiting for a complete, verified curve."
            : IsStale ? (_curveDirty ? "Readings are stale · Unsaved draft kept." : "Readings are stale · Refresh before editing.")
            : _curveDirty ? "Dashed line · Unsaved draft. Apply to save." : "Solid line · Currently reported curve.";
        SetText(_draftCurve, text);
        _chart.SetDraft(valid && !IsStale && _curveDirty ? _curveInputs.Select(input => (int)input.Value).ToArray() : null);
    }

    private void Reason(TableLayoutPanel section, ControlKind kind)
    {
        var label = StateLabel("Unavailable · Verification required.", 32);
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
        foreach (var (kind, label) in _reasons)
        {
            var capability = CapabilityFor(kind);
            var valid = ValidState(kind);
            var available = capability.Kind == kind && capability.CanWrite && valid && !IsStale && !_suspended;
            // A safe background read need not disable a fresh focused draft input. Actions remain serialized.
            available &= kind is ControlKind.PerformanceProfile or ControlKind.CoolerBoost or ControlKind.AutomaticFans or ControlKind.FanCurve;
            var enabled = available && !HasPendingApply && !_shutdownRequested;
            foreach (var control in _hardware[kind]) if (control.Enabled != enabled) control.Enabled = enabled;
            var reason = kind == ControlKind.FanCurve && !valid
                ? "A complete captured CPU table, unchanged fixed temperatures and preserved terminal cap are required. " + capability.Reason
                : capability.Reason;
            var text = _applyQueued ? "Pending · Waiting for the current sample."
                : IsApplying ? "Applying · Waiting for verification."
                : available ? kind switch
                {
                    ControlKind.PerformanceProfile => "Changes only take effect after Apply.",
                    ControlKind.AutomaticFans => "Applying a fan mode turns Cooler Boost off.",
                    ControlKind.CoolerBoost => "Cooler Boost runs fans at maximum effort.",
                    _ => "Only the CPU curve is edited; temperatures remain fixed."
                }
                : IsStale ? "Fresh sample required · Refresh to check firmware controls."
                : kind == ControlKind.AutomaticFans && LastSnapshot?.Controls?.FanMode == 0x1d ? "Read-only · Silent mode is not writable."
                : kind == ControlKind.FanCurve && !valid ? "Read-only · A complete, verified CPU curve is required."
                : "Read-only · Firmware control is unavailable for this device.";
            SetText(label, text);
            if (label.AccessibleDescription != reason) label.AccessibleDescription = reason;
            SetTip(label, reason);
        }
    }

    private bool ValidState(ControlKind kind) => kind switch
    {
        ControlKind.PerformanceProfile => Profile(LastSnapshot?.Controls?.Performance) != "unavailable",
        ControlKind.CoolerBoost => LastSnapshot?.Controls?.Boost is not null,
        ControlKind.AutomaticFans => LastSnapshot?.Controls is { FanMode: 0x0d or 0x8d, Boost: not null },
        ControlKind.FanCurve => ValidCpuCurveState(),
        _ => false
    };

    private bool ValidCpuCurveState()
    {
        var state = LastSnapshot?.Controls;
        return _originalCpuCurve is not null && _originalCpuBreakpoints is not null && CoolingCurvePlan.HasCompleteTable(state)
            && state!.CpuBreakpoints!.SequenceEqual(_originalCpuBreakpoints)
            && state.CpuCurvePacket![7] == _originalCpuCurve[7]
            && state.CpuCurve!.Select((value, i) => value >= _originalCpuCurve[i + 1]).All(valid => valid);
    }

    private void RenderCpuCurve(HardwareControlState? state)
    {
        var capability = CapabilityFor(ControlKind.FanCurve);
        if (_originalCpuCurve is null && capability.Kind == ControlKind.FanCurve && capability.CanWrite && CoolingCurvePlan.HasCompleteTable(state))
        {
            _originalCpuCurve = (byte[])state!.CpuCurvePacket!.Clone();
            _originalCpuBreakpoints = state.CpuBreakpoints!.ToArray();
        }
        if (!ValidCpuCurveState()) return;
        for (var i = 0; i < 6; i++)
        {
            SetText(_curveBreakpoints[i], _originalCpuBreakpoints![i] + "°C · %");
            _curveInputs[i].Maximum = _originalCpuCurve![7];
            _curveInputs[i].Minimum = _originalCpuCurve[i + 1];
            if (!_curveDirty) _curveInputs[i].Value = state!.CpuCurve![i];
        }
    }

    private bool ValidCpuCurveRequest(ControlRequest request)
    {
        if (request.Value == "Restore") return true;
        try { _ = CoolingCurvePlan.Create(_originalCpuCurve!, request.Value); return true; }
        catch (ArgumentException) { return false; }
    }

    private void BeginApply(ControlRequest request)
    {
        _ = ApplyControlAsync(request);
    }

    public Task ApplyControlAsync(ControlRequest request)
    {
        if (HasPendingApply || IsDisposed || _shutdownRequested) return Task.CompletedTask;
        return ActiveApply = ApplyControlCoreAsync(request);
    }

    private async Task ApplyControlCoreAsync(ControlRequest request)
    {
        var generation = _sampleGeneration;
        var token = _shutdown.Token;
        try
        {
            if (_polling)
            {
                _applyQueued = true;
                _result.Text = "Waiting for the current sample before applying…";
                RefreshCapabilities();
                await _sampleTask;
            }
            if (IsDisposed || token.IsCancellationRequested) return;
            UpdateFreshness(DateTimeOffset.UtcNow);
            var cap = CapabilityFor(request.Kind);
            if (_shutdownRequested || _preparing || _suspended || generation != _sampleGeneration || IsStale || !ValidState(request.Kind) || !cap.CanWrite || cap.Kind != request.Kind)
            {
                _result.Text = "Not applied · No command sent. Refresh hardware state before applying again.";
                return;
            }
            if (request.Kind == ControlKind.FanCurve && !ValidCpuCurveRequest(request))
            {
                _result.Text = "Curve not applied · Use six nondecreasing percentages within the captured-original bounds.";
                return;
            }
            IsApplying = true;
            _applyQueued = false;
            _applyError = null;
            RefreshCapabilities();
            _result.Text = "Applying · Waiting for hardware verification…";
            var transaction = _bridge.ApplyAsync(request, token);
            using var notice = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (await Task.WhenAny(transaction, Task.Delay(TimeSpan.FromSeconds(12), notice.Token)) != transaction && !token.IsCancellationRequested)
                _result.Text = "Hardware has not responded yet · Result unconfirmed. No retry will be sent.";
            notice.Cancel();
            var result = await transaction;
            if (IsDisposed || token.IsCancellationRequested) return;
            // A failed transaction may still have changed hardware. Never retry it automatically.
            var refreshed = await StartPoll(true);
            if (IsDisposed || token.IsCancellationRequested) return;
            if (!result.Applied)
            {
                LastError = _applyError = result.Message;
                _result.Text = "Apply failed · " + result.Message;
            }
            else if (!refreshed || IsStale || !ReadbackMatches(request))
            {
                LastError = _applyError = !refreshed || IsStale ? "UI readback unavailable; Refresh required." : "UI readback mismatch; requested value was not observed.";
                _result.Text = _applyError;
            }
            else
            {
                if (request.Kind == ControlKind.PerformanceProfile) _profileDirty = false;
                if (request.Kind == ControlKind.CoolerBoost) _boostDirty = false;
                if (request.Kind == ControlKind.AutomaticFans) _fanModeDirty = false;
                if (request.Kind == ControlKind.FanCurve) _curveDirty = false;
                RenderDrafts(LastSnapshot!.Controls);
                _result.Text = "Backend verified · " + result.Message;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                LastError = _applyError = exception.Message;
                _result.Text = "Apply failed · " + exception.Message;
                MarkStale("Transaction state unknown; Refresh required");
                await Task.Run(() => RuntimeLog.Write("Apply", exception));
            }
        }
        finally
        {
            IsApplying = false;
            _applyQueued = false;
            if (!IsDisposed) { RefreshCapabilities(); ApplyCompleted?.Invoke(this, EventArgs.Empty); }
        }
    }

    private bool ReadbackMatches(ControlRequest request) => request.Kind switch
    {
        ControlKind.PerformanceProfile => Profile(LastSnapshot?.Controls?.Performance) == request.Value,
        ControlKind.CoolerBoost => LastSnapshot?.Controls?.Boost is byte boost && ((boost & 0x80) != 0) == (request.Value == "true"),
        ControlKind.AutomaticFans => FanMode(LastSnapshot?.Controls?.FanMode) == request.Value
            && LastSnapshot?.Controls?.Boost is byte modeBoost && (modeBoost & 0x80) == 0,
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

    private Task<bool> StartPoll(bool readback)
    {
        if (_polling) return _sampleTask;
        return _sampleTask = ReadAndRenderAsync(readback);
    }

    private void SetTip(Control control, string? text)
    {
        if (_tips.GetToolTip(control) != text) _tips.SetToolTip(control, text);
    }

    private static void SetText(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }

    private async Task<bool> ReadAndRenderAsync(bool readback)
    {
        if ((!readback && (HasPendingApply || _shutdownRequested)) || _preparing || _suspended || IsDisposed || _shutdown.IsCancellationRequested) return false;
        _polling = true;
        var generation = _sampleGeneration;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _pollCancellation = cancellation;
        var token = cancellation.Token;
        try
        {
            var snapshot = await _bridge.ReadSnapshotAsync(token);
            if (IsDisposed || token.IsCancellationRequested || _suspended || generation != _sampleGeneration) return false;
            using var viewportUpdate = _viewport.PreserveViewport();
            LastSnapshot = snapshot;
            LastError = _applyError;
            IsStale = DateTimeOffset.UtcNow - snapshot.Timestamp > TimeSpan.FromSeconds(45);
            SetText(_machine, snapshot.Identity.Manufacturer == "Micro-Star International Co., Ltd." ? snapshot.Identity.Model
                : snapshot.Identity.Model == "Unknown" ? "Device unidentified" : $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model}");
            _machine.AccessibleDescription = $"BIOS {snapshot.Identity.Bios} · EC {snapshot.Identity.EcFirmware ?? "unidentified"}";
            SetTip(_machine, _machine.AccessibleDescription);
            SetText(_sampling, $"{(IsStale ? "Stale · Last sample" : snapshot.Controls is null ? "Read-only · Updated" : "Telemetry · Updated")} {snapshot.Timestamp.ToLocalTime():HH:mm:ss}");
            SetText(_current, $"Current profile · {DisplayState(Profile(snapshot.Controls?.Performance))}");
            SetText(_currentMode, $"Current fan mode · {DisplayState(FanMode(snapshot.Controls?.FanMode))}");
            SetText(_currentBoost, $"Cooler Boost · {(snapshot.Controls?.Boost is byte boost ? ((boost & 0x80) != 0 ? "On" : "Off") : "Unavailable")}");
            RenderDrafts(snapshot.Controls);
            SetReading(_cpu, "CPU temperature", snapshot.CpuCelsius);
            SetReading(_gpu, "GPU temperature", snapshot.GpuCelsius);
            SetText(_cpuFan, Fan("Fan 1", snapshot.CpuFan));
            SetText(_gpuFan, Fan("Fan 2", snapshot.GpuFan));
            _cpuFan.AccessibleDescription = snapshot.CpuFan.Source; _gpuFan.AccessibleDescription = snapshot.GpuFan.Source;
            SetTip(_cpuFan, snapshot.CpuFan.Source); SetTip(_gpuFan, snapshot.GpuFan.Source);
            SetText(_battery, $"Battery · {(snapshot.BatteryPercent is >= 0 and <= 100 ? $"{snapshot.BatteryPercent}%" : "Unavailable")}\nSource · {(snapshot.OnAcPower is true ? "AC" : snapshot.OnAcPower is false ? "Battery" : "Unavailable")}");
            _chart.SetData(CoolingCurvePlan.HasCompleteTable(snapshot.Controls) ? snapshot.Controls : null);
            _sampling.AccessibleDescription = snapshot.Status;
            _cpu.AccessibleDescription = snapshot.Status;
            _gpu.AccessibleDescription = snapshot.GpuTemperatureSource ?? snapshot.Status;
            SetTip(_cpu, snapshot.Status);
            SetTip(_gpu, _gpu.AccessibleDescription);
            SetTip(_sampling, snapshot.Status);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                var log = LastError != exception.Message;
                LastError = exception.Message;
                MarkStale("Sample failed: " + exception.Message);
                if (_applyError is null && !HasPendingApply) _result.Text = "Refresh failed · " + exception.Message;
                if (log) await Task.Run(() => RuntimeLog.Write("Sample", exception));
            }
        }
        finally { _pollCancellation = null; _polling = false; if (!IsDisposed) RefreshCapabilities(); }
        return false;
    }

    private void RenderDrafts(HardwareControlState? state)
    {
        _renderingState = true;
        try
        {
            if (!_profileDirty) ((ProfileSelector)_hardware[ControlKind.PerformanceProfile][0]).SelectedItem = Profile(state?.Performance);
            if (!_boostDirty && state?.Boost is byte boost) ((CheckBox)_hardware[ControlKind.CoolerBoost][0]).Checked = (boost & 0x80) != 0;
            if (!_fanModeDirty) ((ComboBox)_hardware[ControlKind.AutomaticFans][0]).SelectedItem = FanMode(state?.FanMode);
            RenderCpuCurve(state);
        }
        finally { _renderingState = false; }
        RenderCurveDraftStatus();
    }

    public void UpdateFreshness(DateTimeOffset now)
    {
        if (IsDisposed) return;
        if (LastSnapshot is not null && now - LastSnapshot.Timestamp > TimeSpan.FromSeconds(45))
            MarkStale($"Last sample {LastSnapshot.Timestamp.ToLocalTime():HH:mm:ss}; awaiting fresh data");
    }

    public void SetSuspended(bool suspended)
    {
        if (IsDisposed || _suspended == suspended) return;
        _suspended = suspended;
        _sampleGeneration++;
        if (suspended) _pollCancellation?.Cancel();
        MarkStale(suspended ? "Sampling paused while Windows is suspended" : "Windows resumed; waiting for a fresh sample");
    }

    internal async Task RefreshAfterResumeAsync()
    {
        var generation = _sampleGeneration;
        await _sampleTask;
        if (HasPendingApply) await ActiveApply;
        if (!IsDisposed && !_shutdownRequested && generation == _sampleGeneration && IsStale) await PollAsync();
    }

    internal void BeginHardwareSetup()
    {
        _preparing = true;
        _hardwareSupport.Text = "Preparing Windows hardware support…";
        _sampling.Text = _hardwareSupport.Text;
    }

    internal void FinishHardwareSetup(string status)
    {
        _preparing = false;
        _hardwareSupport.Text = status;
        if (_page == "Settings") LayoutDashboard();
    }

    internal void RequestShutdown(string status)
    {
        _shutdownRequested = true;
        if (!IsApplying) _pollCancellation?.Cancel();
        _result.Text = status;
        RefreshCapabilities();
    }

    internal Task ActiveSample => _sampleTask;

    private void MarkStale(string reason)
    {
        using var viewportUpdate = _viewport.PreserveViewport();
        IsStale = true;
        _sampling.Text = "Read-only · Stale · " + reason;
        SetTip(_sampling, _sampling.Text);
        RenderCurveDraftStatus();
        RefreshCapabilities();
    }

    private static string Profile(byte? value) => value switch { 0xc0 => "High", 0xc1 => "Medium", 0xc4 => "Turbo", 0xc2 => "Eco", _ => "unavailable" };
    private static string FanMode(byte? value) => value switch { 0x0d => "Auto", 0x8d => "Advanced", 0x1d => "Silent", _ => "unavailable" };
    private static string DisplayState(string value) => value == "unavailable" ? "Unavailable" : value;

    private static string Temperature(double? value) => TelemetryRules.Temperature(value) is double valid ? $"{valid:F1} °C" : "Unavailable";

    private static void SetReading(Label label, string name, double? value)
    {
        SetText(label, Temperature(value));
        var accessible = $"{name}: {label.Text}";
        if (label.AccessibleName != accessible) label.AccessibleName = accessible;
    }

    private static string Fan(string name, FanReading reading)
    {
        var measured = reading.Rpm is >= 0 and <= 30000 ? $"{reading.Rpm} RPM reported" : "Measured RPM unavailable";
        var target = reading.TargetPercent is >= 0 and <= 150 ? $" · Target {reading.TargetPercent:0.#}% (not RPM)" : "";
        return reading.Rpm is not (>= 0 and <= 30000) && target.Length == 0 ? $"{name} · Unavailable" : $"{name} · {measured}{target}";
    }
}
