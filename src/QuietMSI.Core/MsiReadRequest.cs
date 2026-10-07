namespace QuietMSI.Core;

public enum MsiReadKind { Firmware, WmiVersion, Temperatures, FanPeriods, ThermalState, PerformanceState, FanModeState, CpuCurve, CpuCurveTemperatures }
public sealed record MsiReadRequest(string Method, byte[]? Payload)
{
    public static MsiReadRequest Create(MsiReadKind kind) => kind switch
    {
        MsiReadKind.Firmware => new("Get_EC", null),
        MsiReadKind.WmiVersion => new("Get_WMI", null),
        MsiReadKind.Temperatures => new("Get_Temperature", new byte[32]),
        MsiReadKind.FanPeriods => new("Get_Fan", new byte[32]),
        MsiReadKind.ThermalState => WithSelector("Get_Thermal", 3),
        MsiReadKind.PerformanceState => WithSelector("Get_AP", 0),
        MsiReadKind.FanModeState => WithSelector("Get_AP", 1),
        MsiReadKind.CpuCurve => WithSelector("Get_Fan", 1),
        MsiReadKind.CpuCurveTemperatures => WithSelector("Get_Temperature", 1),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static MsiReadRequest WithSelector(string method, byte selector)
    {
        var payload = new byte[32]; payload[0] = selector;
        return new(method, payload);
    }
}
