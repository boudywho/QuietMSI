using System.Management;
using System.Runtime.InteropServices;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

/// <summary>Windows battery API and bounded standard WMI queries only. No writable transport exists.</summary>
public sealed class WindowsWmiSampler : ReadOnlyBridge
{
    private readonly SemaphoreSlim _sampleGate = new(1, 1);
    private DeviceIdentity? _identity;
    private string? _identityWarning;
    private bool _verifiedV2;

    public override async Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        await _sampleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => Read(cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { _sampleGate.Release(); }
    }

    private HardwareSnapshot Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        // Every killable sample owns fresh identification; failures are never cached across refresh/resume.
        _identity = null; _identityWarning = null; _verifiedV2 = false;
        if (_identity is null)
        {
            try
            {
                var system = Query("root\\cimv2", "SELECT Manufacturer,Model FROM Win32_ComputerSystem").FirstOrDefault();
                var bios = Query("root\\cimv2", "SELECT SMBIOSBIOSVersion FROM Win32_BIOS").FirstOrDefault();
                _identity = new(Value(system, "Manufacturer"), Value(system, "Model"), Value(bios, "SMBIOSBIOSVersion"), null);
                if (ProtectedTelemetryPolicy.SupportedModel(_identity))
                {
                    try
                    {
                        _identity = _identity with { EcFirmware = MsiPackageDecoder.Firmware(MsiWmiReader.ReadFirmwarePackage()) };
                        var version = MsiWmiReader.Read(MsiReadKind.WmiVersion);
                        _verifiedV2 = ProtectedTelemetryPolicy.Supports(_identity, version);
                    }
                    catch (Exception e) when (IsProviderError(e)) { _identityWarning = "Firmware info: " + e.Message; }
                }
            }
            catch (Exception e) when (IsProviderError(e))
            {
                _identityWarning = "Device identity: " + e.Message;
                _identity = new("Unknown", "Unknown", "Unknown", null);
            }
        }
        if (_identityWarning is not null) warnings.Add(_identityWarning);
        token.ThrowIfCancellationRequested();
        var (battery, ac) = ReadPower();
        double? cpu = null, gpu = null;
        var cpuFan = FanReading.FromTarget(null, "Not sampled");
        var gpuFan = FanReading.FromTarget(null, "Not sampled");
        if (_verifiedV2)
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
        else if (_identity.Manufacturer.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Protected MSI telemetry needs matching live firmware/WMI identity and Windows permission. No automatic elevation is performed.");
        }
        warnings.Add("Fan RPM uses the vendor period formula; independent HWiNFO/LibreHardwareMonitor validation remains outstanding.");
        warnings.Add("Hardware writes are disabled. Exact control payload and pre/post validation are not fully verified.");
        return new(_identity, DateTimeOffset.UtcNow, cpu, gpu, cpuFan, gpuFan, battery, ac,
            cpu is not null || gpu is not null ? "Read-only MSI WMI telemetry" : "Windows power telemetry; MSI sensors unavailable", warnings);
    }

    private static List<Dictionary<string, object?>> Query(string scope, string query)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3), ReturnImmediately = false, Rewindable = false });
        using var results = searcher.Get();
        var rows = new List<Dictionary<string, object?>>();
        foreach (ManagementObject obj in results)
        {
            using (obj)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData p in obj.Properties) row[p.Name] = p.Value;
                rows.Add(row);
            }
        }
        return rows;
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
