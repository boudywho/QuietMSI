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

Test("Control plans preserve bits and deny unknown states", () =>
{
    var state = new HardwareControlState(0xc1,0x0d,0x06);
    var boost = VerifiedControlPlan.Create(new(ControlKind.CoolerBoost,"true"),state);
    Assert(boost.Count==1 && boost[0].Offset==0x98 && boost[0].Before==6 && boost[0].After==0x86, "Boost mask must preserve six");
    var profile=VerifiedControlPlan.Create(new(ControlKind.PerformanceProfile,"Medium"),state);
    Assert(profile[0].Offset==0xd2 && profile[0].After==0xc1,"Medium encoding exact");
    var auto=VerifiedControlPlan.Create(new(ControlKind.AutomaticFans,"Auto"),state with {FanMode=0x8d,Boost=0x86});
    Assert(auto.Any(x=>x.Offset==0xd4&&x.After==0x0d) && auto.Any(x=>x.Offset==0x98&&x.After==6),"Auto clears only verified mode flags and boost");
    bool denied=false;try{VerifiedControlPlan.Create(new(ControlKind.CoolerBoost,"true"),state with {Boost=null});}catch(InvalidOperationException){denied=true;}
    Assert(denied,"Missing pre-read denies write");
    denied=false;try{VerifiedControlPlan.Create(new(ControlKind.PerformanceProfile,"overclock"),state);}catch(ArgumentException){denied=true;}
    Assert(denied,"Unsupported profile denied");
});

Test("Transaction refuses changed pre-read and logs before write", () =>
{
    var t = new FakeControlTransport(new HardwareControlState(0xc1,0x0d,6));
    var result = new ControlTransaction(t).Apply(new(ControlKind.CoolerBoost,"true"));
    Assert(result.Applied && t.State.Boost==0x86 && t.Events.IndexOf("Pending")<t.Events.IndexOf("Write"),"Read log write read order");
    var changed = new FakeControlTransport(new HardwareControlState(0xc1,0x0d,6)) { Conflict=true };
    result = new ControlTransaction(changed).Apply(new(ControlKind.CoolerBoost,"true"));
    Assert(!result.Applied && !changed.Events.Contains("Write"),"Conflict must deny before write");
    var bad = new FakeControlTransport(new HardwareControlState(0xc1,0x0d,6)) { FailPost=true };
    result = new ControlTransaction(bad).Apply(new(ControlKind.CoolerBoost,"true"));
    Assert(!result.Applied && bad.Events.Count(x=>x=="Write")==1,"No retry after mismatch");
});

Test("Advanced mode requires safe existing six-slot curve", () =>
{
    var state=new HardwareControlState(0xc1,0x0d,6,new[]{38,43,48,54,60,70},new[]{0,55,64,73,76,82});
    var plan=VerifiedControlPlan.Create(new(ControlKind.AutomaticFans,"Advanced"),state);
    Assert(plan[0].Offset==0xd4&&plan[0].After==0x8d,"Mode enables only advanced bit");
    bool denied=false;try{VerifiedControlPlan.Create(new(ControlKind.AutomaticFans,"Advanced"),state with {CpuCurve=new[]{0,20,30,40,50,60}});}catch(InvalidOperationException){denied=true;}
    Assert(denied,"Potential fan-stop curve denies mode switch");
});

Test("Cooling-only curve preserves noneditable bytes and original floor", () =>
{
    var original=new byte[]{60,38,43,48,54,60,70,85};
    var p=CoolingCurvePlan.Create(original,"38,43,49,54,60,70");
    Assert(p[0]==60&&p[7]==85&&p[3]==49,"Only six editable slots");
    bool denied=false;try{CoolingCurvePlan.Create(original,"37,43,48,54,60,70");}catch(ArgumentException){denied=true;}
    Assert(denied,"Lower than original denied");
    denied=false;try{CoolingCurvePlan.Create(original,"38,43,48,54,60,90");}catch(ArgumentException){denied=true;}
    Assert(denied,"Above preserved terminal cap denied");
});

Test("Advanced rejects changed curve before mode write", () =>
{
    var t=new FakeControlTransport(new(0xc1,0x0d,6,new[]{38,43,48,54,60,70},new[]{0,55,64,73,76,82}));
    t.OnRead=(n,s)=>n>1?s with {CpuCurve=new[]{0,0,0,0,0,0}}:s;
    var r=new ControlTransaction(t).Apply(new(ControlKind.AutomaticFans,"Advanced"));
    Assert(!r.Applied&&!t.Events.Contains("Write"),"Changed dependency must deny before enabling Advanced");
});
Test("Multi-register final drift cannot report success", () =>
{
    var t=new FakeControlTransport(new(0xc1,0x8d,0x86));
    t.OnRead=(n,s)=>n>=4?s with {FanMode=0x8d}:s;
    var r=new ControlTransaction(t).Apply(new(ControlKind.AutomaticFans,"Auto"));
    Assert(!r.Applied,"Final full requested state must match");
});
Test("Curve match permits dynamic header but not editable drift", () =>
{
 var a=new byte[]{60,38,43,48,54,60,70,85};var b=(byte[])a.Clone();b[0]=70;
 Assert(CoolingCurvePlan.SameStableFields(a,b),"Dynamic header is not curve drift");b[3]=49;
 Assert(!CoolingCurvePlan.SameStableFields(a,b),"Editable field change remains conflict");
});
Test("Contradictory setter status is rejected", () =>
{
    var p=new byte[32];p[0]=1;
    Assert(!WmiResultRules.SetterSucceeded(false,p),"Explicit false overrides packet status");
    Assert(WmiResultRules.SetterSucceeded(null,p),"Absent optional return preserves established packet contract");
});

Console.WriteLine($"RESULT {passed} passed, {failures.Count} failed");
return failures.Count == 0 ? 0 : 1;
