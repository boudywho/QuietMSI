namespace QuietMSI.Core;

public static class TelemetryRules
{
    public static double? Temperature(double? value) => value is >= 0 and <= 125 ? value : null;
}
