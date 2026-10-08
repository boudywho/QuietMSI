using System.Diagnostics;
using System.Text.Json;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

/// <summary>Killable read-only worker prevents synchronous WMI hangs from owning the UI forever.</summary>
public sealed class WindowsHardwareBridge : ReadOnlyBridge, IControlCapabilityProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<ProcessStartInfo> _worker;
    private readonly TimeSpan _deadline;
    private readonly Action<Process>? _started;
    private bool _quarantined;
    private byte[]? _originalCpuCurve;
    private byte[]? _lastAppliedCpuCurve;

    public WindowsHardwareBridge(Func<ProcessStartInfo>? worker = null, TimeSpan? deadline = null, Action<Process>? started = null)
    {
        _worker = worker ?? ProductWorker;
        _deadline = deadline ?? TimeSpan.FromSeconds(10);
        _started = started;
    }

    private static ProcessStartInfo ProductWorker()
    {
        var info = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable."));
        if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "QuietMSI.dll"));
        info.ArgumentList.Add("--read-worker");
        return info;
    }

    public override async Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Process? worker = null;
        try
        {
            if (_quarantined) throw new InvalidOperationException("A previous telemetry worker could not be terminated. Restart the application; no overlapping reads are allowed.");
            var info = _worker();
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            worker = Process.Start(info) ?? throw new InvalidOperationException("Telemetry worker did not start.");
            _started?.Invoke(worker);
            var output = worker.StandardOutput.ReadToEndAsync();
            var errors = worker.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(_deadline);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try { await worker.WaitForExitAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                Terminate(worker);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Telemetry provider exceeded the 10-second deadline; its read-only worker was terminated.");
            }
            if (worker.ExitCode != 0) throw new InvalidOperationException("Telemetry worker failed: " + (await errors.ConfigureAwait(false)).Trim());
            var text = await output.ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<HardwareSnapshot>(text) ?? throw new InvalidOperationException("Telemetry worker returned no valid snapshot.");
            if (_originalCpuCurve is null && snapshot.Identity.EcFirmware == "16R6EMS1.106" && snapshot.Controls?.CpuCurvePacket is { Length: 8 } baseline)
            {
                _originalCpuCurve = (byte[])baseline.Clone();
                new MsiControlTransport().Record("SessionOriginalCpuCurve", new { snapshot.Identity, table = baseline });
            }
            return snapshot;
        }
        finally
        {
            try
            {
                if (worker is not null)
                {
                    try { if (!worker.HasExited) Terminate(worker); }
                    finally { worker.Dispose(); }
                }
            }
            finally { _gate.Release(); }
        }
    }

    public Capability GetCapability(HardwareSnapshot snapshot, ControlKind kind)
    {
        if (_quarantined || snapshot.Controls is null || !ProtectedTelemetryPolicy.SupportedModel(snapshot.Identity)
            || snapshot.Identity.EcFirmware != "16R6EMS1.106") return new(kind, false, "Matching live firmware and readable control state required.");
        return kind switch
        {
            ControlKind.CoolerBoost => new(kind, true, "Verified mask and real hardware enable/restore test."),
            ControlKind.PerformanceProfile => new(kind, true, "Firmware performance levels; not overclocking or complete MSI scenarios."),
            ControlKind.AutomaticFans => new(kind, true, "Auto/Advanced mode with current validated curve; does not reset factory curves."),
            ControlKind.FanCurve when _originalCpuCurve is not null => new(kind, true, "CPU increases only from captured original curve; temperatures and GPU table unchanged."),
            _ => new(kind, false, "This control's reversible hardware workflow is not yet verified.")
        };
    }

    public override async Task<ControlResult> ApplyAsync(ControlRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind is not (ControlKind.CoolerBoost or ControlKind.PerformanceProfile or ControlKind.AutomaticFans or ControlKind.FanCurve))
            return new(false, "Control not yet verified; no command sent.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_quarantined) return new(false, "Previous indeterminate worker state; restart and inspect hardware before further changes.");
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                try
                {
                    // Mutex acquire/release remain on the same worker thread across the whole transaction.
                    using var ownership = new MachineWriteOwnership();
                    if (request.Kind == ControlKind.FanCurve) return ApplyCpuCurve(request);
                    return new ControlTransaction(new MsiControlTransport()).Apply(request);
                }
                catch(Exception e) { return new ControlResult(false,e.Message); }
            }).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private ControlResult ApplyCpuCurve(ControlRequest request)
    {
        try
        {
            MsiControlStateReader.VerifyIdentity();
            if (_originalCpuCurve is null) throw new InvalidOperationException("No captured session original table; curve change denied.");
            var current = MsiCpuCurve.ReadTable();
            var expected = _lastAppliedCpuCurve ?? _originalCpuCurve;
            if (!CoolingCurvePlan.SameStableFields(current,expected)) throw new InvalidOperationException("Curve differs from captured/last-applied table; another controller may have changed it. No overwrite.");
            var next = request.Value == "Restore" ? (byte[])_originalCpuCurve.Clone() : CoolingCurvePlan.Create(_originalCpuCurve, request.Value);
            next[0]=current[0]; // Preserve the freshly observed dynamic noneditable header.
            MsiCpuCurve.Write(current,next,_originalCpuCurve);
            _lastAppliedCpuCurve = next;
            return new(true, request.Value == "Restore" ? "Captured original CPU table restored and verified; not a factory reset." : "CPU curve saved and verified. Select Advanced to use it; temperature/GPU tables unchanged.");
        }
        catch(Exception e) { try { new MsiControlTransport().Record("CpuCurveFailed",new {error=e.Message}); } catch { } return new(false,e.Message); }
    }

    private void Terminate(Process worker)
    {
        try
        {
            try { if (!worker.HasExited) worker.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            if (!worker.WaitForExit(2000)) throw new InvalidOperationException("Telemetry worker termination failed. Further reads are blocked to avoid overlap.");
        }
        catch
        {
            _quarantined = true;
            throw;
        }
    }
}
