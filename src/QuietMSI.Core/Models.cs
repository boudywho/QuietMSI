namespace QuietMSI.Core;

public sealed record DeviceIdentity(string Manufacturer, string Model, string Bios, string? EcFirmware);
public enum ControlKind { PerformanceProfile, CoolerBoost, ChargeLimit, FanCurve, AutomaticFans, GpuMux }
public sealed record Capability(ControlKind Kind, bool CanWrite, string Reason);
public sealed record ControlRequest(ControlKind Kind, string Value);
public sealed record ControlResult(bool Applied, string Message);
public sealed record HardwareSnapshot(DeviceIdentity Identity, DateTimeOffset Timestamp, double? CpuCelsius, double? GpuCelsius,
    FanReading CpuFan, FanReading GpuFan, int? BatteryPercent, bool? OnAcPower, string Status, IReadOnlyList<string> Diagnostics);
public sealed record FanReading(double? TargetPercent, int? Rpm, string Source)
{
    public static FanReading FromTarget(double? target, string source) => new(target is >= 0 and <= 150 ? target : null, null, source);
}
