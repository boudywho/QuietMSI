using QuietMSI.Core;

var passed = 0;
var failures = new List<string>();
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures.Add(name + ": " + e.Message); Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
void Assert(bool condition, string reason) { if (!condition) throw new Exception(reason); }

Test("Unknown firmware denies Cooler Boost", () =>
{
    var identity = new DeviceIdentity("Micro-Star International Co., Ltd.", "GF63 Thin 11UCX", "E16R6IMS.112", null);
    var cap = HardwarePolicy.GetCapability(identity, ControlKind.CoolerBoost);
    Assert(!cap.CanWrite, "Unknown firmware must be denied");
    Assert(cap.Reason.Contains("firmware", StringComparison.OrdinalIgnoreCase), "Denial must explain missing firmware");
});

Test("Temperature rejects missing and implausible readings", () =>
{
    Assert(TelemetryRules.Temperature(null) is null, "Missing must stay unavailable");
    Assert(TelemetryRules.Temperature(255) is null, "Firmware sentinel cannot be Celsius");
    Assert(TelemetryRules.Temperature(-1) is null, "Negative must be unavailable");
    Assert(TelemetryRules.Temperature(57) == 57, "Valid reading preserved");
});

Test("Fan target is not tachometer RPM", () =>
{
    var fan = FanReading.FromTarget(35, "MSI EC target");
    Assert(fan.TargetPercent == 35 && fan.Rpm is null, "Percentage must not be converted into guessed RPM");
    Assert(FanReading.FromTarget(255, "MSI EC target").TargetPercent is null, "Invalid target rejected");
    Assert(FanReading.FromTarget(0, "MSI EC target").Rpm is null, "Zero target does not imply stopped fan");
});

Test("Read-only bridge rejects every control", () =>
{
    IHardwareBridge bridge = new ReadOnlyBridge();
    foreach (var kind in Enum.GetValues<ControlKind>())
    {
        var result = bridge.ApplyAsync(new ControlRequest(kind, "anything"), CancellationToken.None).GetAwaiter().GetResult();
        Assert(!result.Applied, "No controls may bypass read-only mode");
    }
});

Test("Fan curve validates ordered safe points", () =>
{
    Assert(FanCurve.Validate(new[] { new CurvePoint(40, 30), new CurvePoint(60, 50), new CurvePoint(85, 100) }) is null, "Valid draft accepted");
    Assert(FanCurve.Validate(new[] { new CurvePoint(60, 50), new CurvePoint(40, 60) }) is not null, "Descending temperatures denied");
    Assert(FanCurve.Validate(new[] { new CurvePoint(40, 50), new CurvePoint(60, 30) }) is not null, "Decreasing fan effort denied");
    Assert(FanCurve.Validate(new[] { new CurvePoint(40, 0), new CurvePoint(60, 101) }) is not null, "Unsafe percentages denied");
    Assert(FanCurve.Validate(Array.Empty<CurvePoint>()) is not null, "Empty denied");
});

Console.WriteLine($"RESULT {passed} passed, {failures.Count} failed");
return failures.Count == 0 ? 0 : 1;
