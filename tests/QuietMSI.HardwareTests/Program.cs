using QuietMSI.Core;
using QuietMSI.Hardware;
using System.Text.Json;

if (args.Contains("--ownership-probe"))
{
    using var first=new MachineWriteOwnership();
    var blocked=Task.Run(()=> {try{using var second=new MachineWriteOwnership();return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();
    if(!blocked)throw new Exception("Machine ownership did not block competing writer");
    Console.WriteLine("PASS machine-wide admin ownership denies competing thread");return 0;
}
if (args.Contains("--read-worker"))
{
    Console.WriteLine(JsonSerializer.Serialize(await new WindowsWmiSampler().ReadSnapshotAsync(CancellationToken.None)));
    return 0;
}
if (args.Contains("--blocked-worker")) { await Task.Delay(Timeout.Infinite); return 0; }

System.Diagnostics.ProcessStartInfo Worker(string flag)
{
    var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!);
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        info.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
    info.ArgumentList.Add(flag);
    return info;
}
int workerPid = 0;
bool WorkerStopped() { try { using var p = System.Diagnostics.Process.GetProcessById(workerPid); return p.HasExited; } catch (ArgumentException) { return true; } }
var deadlineBridge = new WindowsHardwareBridge(() => Worker("--blocked-worker"), TimeSpan.FromMilliseconds(400), p => workerPid = p.Id);
var timedOut = false;
try { await deadlineBridge.ReadSnapshotAsync(CancellationToken.None); } catch (TimeoutException) { timedOut = true; }
if (!timedOut || workerPid == 0 || !WorkerStopped()) throw new Exception("Deadline must terminate worker before releasing read ownership");
Console.WriteLine("PASS blocked-provider deadline terminates worker");
using var cancel = new CancellationTokenSource(400);
var cancelled = false;
try { await deadlineBridge.ReadSnapshotAsync(cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
if (!cancelled || !WorkerStopped()) throw new Exception("Cancellation must terminate worker");
Console.WriteLine("PASS cancellation terminates worker");
var bridge = new WindowsHardwareBridge(() => Worker("--read-worker"));
if (args.Contains("--protected"))
{
    try
    {
        var package = MsiWmiReader.ReadFirmwarePackage();
        if (MsiPackageDecoder.Firmware(package) != "16R6EMS1.106") throw new Exception("Observed protected firmware failed to decode");
        Console.WriteLine("PASS protected firmware getter");
    }
    catch (Exception e) { Console.WriteLine(e.ToString()); return 1; }
}
var snapshot = await bridge.ReadSnapshotAsync(CancellationToken.None);
if (args.Contains("--curve-approved"))
{
    if(snapshot.Controls?.CpuCurve is not {Count:6} original || snapshot.Controls.FanMode!=0x0d) throw new Exception("Original curve/Auto state unavailable; no change");
    var next=original.ToArray();next[2]++;
    var result=await bridge.ApplyAsync(new(ControlKind.FanCurve,string.Join(',',next)),CancellationToken.None);
    Console.WriteLine("CURVE APPLY "+JsonSerializer.Serialize(result));if(!result.Applied)throw new Exception(result.Message);
    var changed=await bridge.ReadSnapshotAsync(CancellationToken.None);
    if(!changed.Controls!.CpuCurve!.SequenceEqual(next))throw new Exception("Curve readback mismatch; stopped");
    result=await bridge.ApplyAsync(new(ControlKind.FanCurve,"Restore"),CancellationToken.None);
    Console.WriteLine("CURVE RESTORE "+JsonSerializer.Serialize(result));if(!result.Applied)throw new Exception(result.Message);
    var restored=await bridge.ReadSnapshotAsync(CancellationToken.None);
    if(!restored.Controls!.CpuCurve!.SequenceEqual(original)||restored.Controls.FanMode!=snapshot.Controls.FanMode)throw new Exception("Original curve/mode not restored");
    Console.WriteLine("PASS actual C# cooling-only curve Apply/readback/original restoration");return 0;
}
if (args.Contains("--workflow-approved"))
{
    if (snapshot.Identity.EcFirmware != "16R6EMS1.106" || snapshot.Controls is not { } original)
        throw new Exception("Live protected state unavailable; test denied before change");
    if (original.Performance != 0xc1 || original.FanMode != 0x0d || (original.Boost & 0x80) != 0)
        throw new Exception("Original state differs from approved Medium/Auto/boost-off workflow; no changes");
    Console.WriteLine("ORIGINAL " + JsonSerializer.Serialize(original));
    foreach (var command in new[] {
        new ControlRequest(ControlKind.CoolerBoost,"true"), new(ControlKind.CoolerBoost,"false"),
        new(ControlKind.PerformanceProfile,"High"), new(ControlKind.PerformanceProfile,"Medium"),
        new(ControlKind.AutomaticFans,"Advanced"), new(ControlKind.AutomaticFans,"Auto") })
    {
        var result=await bridge.ApplyAsync(command,CancellationToken.None);
        Console.WriteLine("APPLY " + JsonSerializer.Serialize(new {command,result}));
        if(!result.Applied) throw new Exception("Workflow stopped on failed read-back; no retry: "+result.Message);
        var check=await bridge.ReadSnapshotAsync(CancellationToken.None);
        Console.WriteLine("READBACK " + JsonSerializer.Serialize(new {check.Controls,check.CpuFan,check.CpuCelsius}));
        if(check.Controls is null) throw new Exception("Post-Apply protected state unavailable");
        var expected=command.Kind switch {
            ControlKind.CoolerBoost => (check.Controls.Boost & 0x80) == (command.Value=="true"?0x80:0),
            ControlKind.PerformanceProfile => check.Controls.Performance==(command.Value=="High"?0xc0:0xc1),
            _ => check.Controls.FanMode==(command.Value=="Advanced"?0x8d:0x0d) };
        if(!expected) throw new Exception("Actual bridge snapshot disagrees with Apply; stopped");
    }
    var restored=(await bridge.ReadSnapshotAsync(CancellationToken.None)).Controls;
    if(restored is null || restored.Performance!=original.Performance || restored.FanMode!=original.FanMode || restored.Boost!=original.Boost
        || !restored.CpuCurve!.SequenceEqual(original.CpuCurve!)) throw new Exception("Final original state not restored");
    Console.WriteLine("PASS actual C# Apply/readback workflow; original state and curve restored");
    return 0;
}
if (string.IsNullOrWhiteSpace(snapshot.Identity.Model) || snapshot.Identity.Model == "Unknown") throw new Exception("Real device identity must be discovered");
if (snapshot.BatteryPercent is < 0 or > 100) throw new Exception("Battery out of bounds");
foreach (var kind in new[] {ControlKind.ChargeLimit,ControlKind.GpuMux})
{
    var result = await bridge.ApplyAsync(new(kind, "test"), CancellationToken.None);
    if (result.Applied) throw new Exception("Read-only transport attempted a write");
}
Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS Windows bridge discovers real identity and denies unsupported controls; no live write test");
return 0;
