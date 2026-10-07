using System.Diagnostics;
using System.Text.Json;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

/// <summary>Killable read-only worker prevents synchronous WMI hangs from owning the UI forever.</summary>
public sealed class WindowsHardwareBridge : ReadOnlyBridge
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<ProcessStartInfo> _worker;
    private readonly TimeSpan _deadline;
    private readonly Action<Process>? _started;
    private bool _quarantined;

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
            return JsonSerializer.Deserialize<HardwareSnapshot>(text) ?? throw new InvalidOperationException("Telemetry worker returned no valid snapshot.");
        }
        finally
        {
            if (worker is not null)
            {
                try { if (!worker.HasExited) Terminate(worker); }
                finally { worker.Dispose(); }
            }
            _gate.Release();
        }
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
