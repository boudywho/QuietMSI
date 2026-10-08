using System.Diagnostics;
using System.Globalization;

namespace QuietMSI.Hardware;

internal static class NvidiaGpuTemperature
{
    internal sealed record Reading(double? Celsius, string Source);

    // Only the driver-installed System32 executable is trusted by this elevated app.
    internal static async Task<Reading> ReadAsync(CancellationToken token, Func<ProcessStartInfo>? worker = null, TimeSpan? deadline = null, Action<Process>? onStarted = null)
    {
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (worker is null && !File.Exists(path)) return new(null, "NVIDIA temperature reader is not installed");
        var info = worker?.Invoke() ?? new ProcessStartInfo(path);
        if (worker is null)
        {
            info.ArgumentList.Add("--query-gpu=temperature.gpu");
            info.ArgumentList.Add("--format=csv,noheader,nounits");
            info.ArgumentList.Add("--id=0");
        }
        info.UseShellExecute = false; info.CreateNoWindow = true;
        info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using var process = new Process { StartInfo = info };
        var started = false;
        try
        {
            started = process.Start();
            if (!started) return new(null, "NVIDIA temperature query could not start");
            onStarted?.Invoke(process);
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(deadline ?? TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            try { await process.WaitForExitAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { return new(null, "NVIDIA temperature query timed out"); }
            var text = await output.ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            var temperature = process.ExitCode == 0 ? Parse(text) : null;
            return new(temperature, temperature is not null ? "NVIDIA driver · nvidia-smi temperature.gpu" : "NVIDIA driver did not report a valid GPU temperature");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        { return new(null, "NVIDIA temperature query unavailable: " + e.Message); }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(500)) throw new InvalidOperationException("NVIDIA temperature worker could not be stopped.");
            }
        }
    }

    internal static double? Parse(string output) => int.TryParse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        && value is > 0 and <= 125 ? value : null;
}
