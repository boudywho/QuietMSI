namespace QuietMSI.Core;

public sealed record CurvePoint(int Celsius, int Percent);
public static class FanCurve
{
    // Draft validation is not evidence that a firmware accepts the curve.
    public static string? Validate(IReadOnlyList<CurvePoint> points)
    {
        if (points.Count is < 2 or > 8) return "Use between two and eight points.";
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.Celsius is < 20 or > 100 || p.Percent is < 20 or > 100) return "Points must be 20–100 °C and 20–100%.";
            if (i > 0 && (p.Celsius <= points[i - 1].Celsius || p.Percent < points[i - 1].Percent))
                return "Temperatures must increase and fan effort must not decrease.";
        }
        return null;
    }
}
