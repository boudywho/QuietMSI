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

Test("Firmware parser requires successful exact-length ASCII response", () =>
{
    var bytes = new byte[32]; bytes[0] = 1; bytes[1] = 129;
    System.Text.Encoding.ASCII.GetBytes("16R6EMS1.1060423202316:39:13").CopyTo(bytes, 2);
    Assert(MsiPackageDecoder.Firmware(bytes) == "16R6EMS1.106", "Exact observed firmware must decode");
    bytes[0] = 0; Assert(MsiPackageDecoder.Firmware(bytes) is null, "Firmware with failed status denied");
    Assert(MsiPackageDecoder.Firmware(new byte[2]) is null, "Truncation denied");
    bytes[0] = 1; bytes[5] = 255; Assert(MsiPackageDecoder.Firmware(bytes) is null, "Non-ASCII denied");
});

Test("V2 telemetry validates status and separates period from target", () =>
{
    var temp = new byte[32]; temp[0] = 1; temp[1] = 75; temp[2] = 63;
    Assert(MsiPackageDecoder.Temperatures(temp) == (75d, 63d), "Read established Celsius fields");
    temp[1] = 0; Assert(MsiPackageDecoder.Temperatures(temp).Cpu is null, "Zero temp sentinel unavailable");
    var fan = new byte[32]; fan[0] = 1; fan[2] = 161;
    Assert(MsiPackageDecoder.FanPeriod(fan, 0) == 161, "Big endian period");
    Assert(MsiPackageDecoder.VendorRpm(fan, 0) == 2981, "Vendor reciprocal formula");
    Assert(MsiPackageDecoder.VendorRpm(fan, 1) is null, "Zero second channel does not prove stopped/installed fan");
    fan[0] = 2; Assert(MsiPackageDecoder.VendorRpm(fan, 0) is null, "Unknown status denied");
    Assert(MsiPackageDecoder.VendorRpm(new byte[1], 0) is null, "Short packet denied");
});

Test("Read request allowlist emits fixed zero-filled payloads", () =>
{
    Assert(MsiReadRequest.Create(MsiReadKind.Firmware).Payload is null, "Firmware must have no input");
    Assert(MsiReadRequest.Create(MsiReadKind.WmiVersion).Payload is null, "Version must have no input");
    var r = MsiReadRequest.Create(MsiReadKind.Temperatures);
    Assert(r.Method == "Get_Temperature" && r.Payload is { Length: 32 } && r.Payload.All(x => x == 0), "Only zero selector telemetry request");
    Assert(MsiReadRequest.Create(MsiReadKind.FanPeriods).Method == "Get_Fan", "Only fixed fan getter");
    var rejected = false; try { MsiReadRequest.Create((MsiReadKind)999); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Assert(rejected, "Undefined method cannot be invoked");
});

Test("Protected sensors require exact current firmware and WMI schema", () =>
{
    var id = new DeviceIdentity("Micro-Star International Co., Ltd.", "GF63 Thin 11UCX", "E16R6IMS.112", "16R6EMS1.106");
    var v = new byte[32]; v[0]=1; v[1]=2; v[2]=8;
    Assert(ProtectedTelemetryPolicy.Supports(id,v), "Observed schema supported for reads");
    Assert(!ProtectedTelemetryPolicy.Supports(id with { Model = "Other MSI" },v), "Other model denied");
    Assert(!ProtectedTelemetryPolicy.Supports(id with { EcFirmware = null },v), "Previous firmware cannot stand in for current failure");
    v[2]=9; Assert(!ProtectedTelemetryPolicy.Supports(id,v), "Unknown changed interface denied");
});

Console.WriteLine($"RESULT {passed} passed, {failures.Count} failed");
return failures.Count == 0 ? 0 : 1;
