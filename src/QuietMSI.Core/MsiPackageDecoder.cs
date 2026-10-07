using System.Text;
using System.Text.RegularExpressions;

namespace QuietMSI.Core;

public static class MsiPackageDecoder
{
    public static (double? Cpu, double? Gpu) Temperatures(byte[]? package)
    {
        if (package is not { Length: 32 } || package[0] != 1) return (null, null);
        return (package[1] == 0 ? null : TelemetryRules.Temperature(package[1]), package[2] == 0 ? null : TelemetryRules.Temperature(package[2]));
    }

    public static int? FanPeriod(byte[]? package, int channel)
    {
        if (package is not { Length: 32 } || package[0] != 1 || channel is < 0 or > 1) return null;
        var offset = 1 + channel * 2;
        var period = (package[offset] << 8) | package[offset + 1];
        return period == 0 ? null : period;
    }

    // Vendor-reported conversion, not an independently calibrated physical measurement.
    public static int? VendorRpm(byte[]? package, int channel)
    {
        var period = FanPeriod(package, channel);
        if (period is null) return null;
        var rpm = 480000 / period.Value;
        return rpm is > 0 and <= 12000 ? rpm : null;
    }

    public static string? Firmware(byte[]? package)
    {
        if (package is not { Length: 32 } || package[0] != 1) return null;
        var span = package.AsSpan(2, 12);
        foreach (var b in span) if (b is < 32 or > 126) return null;
        var text = Encoding.ASCII.GetString(span).TrimEnd('\0', ' ');
        return Regex.IsMatch(text, "^[0-9A-Z]{4}EMS[0-9]\\.[0-9A-Z]{3}$", RegexOptions.CultureInvariant) ? text : null;
    }
}
