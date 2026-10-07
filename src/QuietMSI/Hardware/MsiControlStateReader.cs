using System.Management;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

internal static class MsiControlStateReader
{
    internal static byte[] RequirePacket(MsiReadKind kind)
    {
        var p = MsiWmiReader.Read(kind);
        if (p is not { Length: 32 } || p[0] != 1) throw new InvalidOperationException($"{kind}: getter failed or packet/status malformed.");
        return p;
    }
    internal static HardwareControlState Read()
    {
        var ap = RequirePacket(MsiReadKind.PerformanceState);
        var mode = RequirePacket(MsiReadKind.FanModeState);
        var boost = RequirePacket(MsiReadKind.ThermalState);
        var curve = RequirePacket(MsiReadKind.CpuCurve);
        var temperatures = RequirePacket(MsiReadKind.CpuCurveTemperatures);
        return new(ap[3], mode[1], boost[1], curve.Skip(2).Take(6).Select(x => (int)x).ToArray(),
            new[] { 1,4,5,6,7,8 }.Select(i => (int)temperatures[i]).ToArray()) { CpuCurvePacket = curve.Skip(1).Take(8).ToArray() };
    }
    internal static DeviceIdentity VerifyIdentity()
    {
        using var q = new ManagementObjectSearcher("root\\cimv2", "SELECT Manufacturer,Model FROM Win32_ComputerSystem");
        using var rows = q.Get();
        foreach (ManagementObject row in rows)
        {
            using (row)
            {
                var identity = new DeviceIdentity(row["Manufacturer"]?.ToString() ?? "Unknown", row["Model"]?.ToString() ?? "Unknown", "Not used for EC match", null);
                if (!ProtectedTelemetryPolicy.SupportedModel(identity)) throw new InvalidOperationException("Unsupported model; hardware change denied.");
                identity = identity with { EcFirmware = MsiPackageDecoder.Firmware(RequirePacket(MsiReadKind.Firmware)) };
                if (!ProtectedTelemetryPolicy.Supports(identity, RequirePacket(MsiReadKind.WmiVersion))) throw new InvalidOperationException("Live EC firmware or WMI schema mismatch; change denied.");
                return identity;
            }
        }
        throw new InvalidOperationException("Device identity unavailable.");
    }
}
