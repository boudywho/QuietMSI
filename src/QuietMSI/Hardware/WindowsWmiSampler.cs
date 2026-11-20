using System.Management;
using System.Runtime.InteropServices;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

/// <summary>Windows battery API and bounded standard WMI queries only. No writable transport exists.</summary>
public sealed class WindowsWmiSampler : ReadOnlyBridge
{
    public override Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ReadAsync(cancellationToken), cancellationToken);

    private static async Task<HardwareSnapshot> ReadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        // Every killable sample owns fresh identification; failures are never cached across refresh/resume.
        DeviceIdentity identity;
        var verified = false;
        try
        {
            var system = QueryFirst("SELECT Manufacturer,Model FROM Win32_ComputerSystem");
            var bios = QueryFirst("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            identity = new(Value(system, "Manufacturer"), Value(system, "Model"), Value(bios, "SMBIOSBIOSVersion"), null);
            if (ProtectedTelemetryPolicy.SupportedModel(identity))
            {
                try
                {
                    identity = identity with { EcFirmware = MsiPackageDecoder.Firmware(MsiWmiReader.ReadFirmwarePackage()) };
                    verified = ProtectedTelemetryPolicy.Supports(identity, MsiWmiReader.Read(MsiReadKind.WmiVersion));
                }
                catch (Exception e) when (IsProviderError(e)) { warnings.Add("Firmware info: " + e.Message); }
            }
        }
        catch (Exception e) when (IsProviderError(e))
        {
            warnings.Add("Device identity: " + e.Message);
            identity = new("Unknown", "Unknown", "Unknown", null);
        }
        token.ThrowIfCancellationRequested();
        var (battery, ac) = ReadPower();
        double? cpu = null, gpu = null;
        var cpuFan = FanReading.FromTarget(null, "Not sampled");
        var gpuFan = FanReading.FromTarget(null, "Not sampled");
        if (verified)
        {
            try { (cpu, gpu) = MsiPackageDecoder.Temperatures(MsiWmiReader.Read(MsiReadKind.Temperatures)); }
            catch (Exception e) when (IsProviderError(e)) { warnings.Add("MSI temperature getter: " + e.Message); }
            token.ThrowIfCancellationRequested();
            try
            {
                var periods = MsiWmiReader.Read(MsiReadKind.FanPeriods);
                cpuFan = new(null, MsiPackageDecoder.VendorRpm(periods, 0), "MSI WMI vendor-reported RPM; not independently calibrated");
                gpuFan = new(null, null, "Second tachometer channel not validated on this machine");
            }
            catch (Exception e) when (IsProviderError(e)) { warnings.Add("MSI fan getter: " + e.Message); }
        }
        else if (identity.Manufacturer.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Protected MSI telemetry needs matching live firmware/WMI identity and Windows permission. No automatic elevation is performed.");
        }
        var gpuSource = gpu is not null ? "MSI WMI temperature getter" : null;
        if (gpu is null)
        {
            var reading = await NvidiaGpuTemperature.ReadAsync(token).ConfigureAwait(false);
            gpu = reading.Celsius;
            gpuSource = reading.Source;
            warnings.Add("GPU temperature: " + reading.Source);
        }
        token.ThrowIfCancellationRequested();
        HardwareControlState? controls = null;
        if (verified)
        {
            try { controls = MsiControlStateReader.Read(); }
            catch (Exception e) when (IsProviderError(e)) { warnings.Add("Control-state read: " + e.Message); }
        }
        warnings.Add("Fan RPM uses the vendor period formula; independent HWiNFO/LibreHardwareMonitor validation remains outstanding.");
        warnings.Add("Hardware changes require explicit Apply, fresh identity/state checks and journaled read-back. CPU curve edits are cooling-only; GPU/temperature tables are unchanged.");
        return new(identity, DateTimeOffset.UtcNow, cpu, gpu, cpuFan, gpuFan, battery, ac,
            cpu is not null ? "MSI WMI telemetry · GPU: " + gpuSource
                : gpu is not null ? "Windows power telemetry · GPU: " + gpuSource
                : "Windows power telemetry; MSI sensors unavailable", warnings) { Controls = controls, GpuTemperatureSource = gpuSource };
    }

    private static Dictionary<string, object?>? QueryFirst(string query)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope("root\\cimv2"), new ObjectQuery(query),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3), ReturnImmediately = false, Rewindable = false });
        using var results = searcher.Get();
        foreach (ManagementObject obj in results)
        {
            using (obj)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData p in obj.Properties) row[p.Name] = p.Value;
                return row;
            }
        }
        return null;
    }
    private static string Value(Dictionary<string, object?>? row, string name) => row?.GetValueOrDefault(name)?.ToString() ?? "Unknown";
    private static bool IsProviderError(Exception e) => e is ManagementException or COMException or UnauthorizedAccessException or InvalidOperationException or FormatException;

    private static (int?, bool?) ReadPower()
    {
        if (!GetSystemPowerStatus(out var status)) return (null, null);
        return (status.BatteryLifePercent <= 100 && status.BatteryFlag != 128 ? status.BatteryLifePercent : null,
            status.ACLineStatus == 255 ? null : status.ACLineStatus == 1);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
