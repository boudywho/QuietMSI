namespace QuietMSI.Core;

public enum MsiReadKind { Firmware, WmiVersion, Temperatures, FanPeriods }
public sealed record MsiReadRequest(string Method, byte[]? Payload)
{
    public static MsiReadRequest Create(MsiReadKind kind) => kind switch
    {
        MsiReadKind.Firmware => new("Get_EC", null),
        MsiReadKind.WmiVersion => new("Get_WMI", null),
        MsiReadKind.Temperatures => new("Get_Temperature", new byte[32]),
        MsiReadKind.FanPeriods => new("Get_Fan", new byte[32]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
