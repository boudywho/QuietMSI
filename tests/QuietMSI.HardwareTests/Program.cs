using QuietMSI.Core;
using QuietMSI.Hardware;
using System.Text.Json;

// Explicit elevated acceptance mode only; normal integration tests remain read-only.
if (args.Length == 2 && args[0] == "--standalone-control-probe")
{
    using var ownership = new MachineWriteOwnership();
    var transport = new MsiControlTransport();
    transport.VerifyIdentity();
    var before = transport.ReadState();
    var performance = before.Performance ?? throw new Exception("Missing performance state");
    if (performance is not (0xc0 or 0xc1 or 0xc2 or 0xc4)) throw new Exception("Unknown performance encoding");
    var fresh = transport.ReadState();
    if (fresh.Performance != performance) throw new Exception("State changed before same-value probe");
    transport.Record("Pending", new { Test = "Standalone same-value Set_Data", Before = performance, After = performance });
    transport.Write(new(0xd2, performance, performance));
    var afterRegister = transport.ReadState();
    if (afterRegister.Performance != performance || afterRegister.FanMode != before.FanMode || afterRegister.Boost != before.Boost)
        throw new Exception("Same-value register probe changed state; inspect hardware, no guessed rollback");
    var table = MsiCpuCurve.ReadTable();
    MsiCpuCurve.Write(table, (byte[])table.Clone(), (byte[])table.Clone());
    var after = transport.ReadState();
    if (after.Performance != before.Performance || after.FanMode != before.FanMode || after.Boost != before.Boost
        || !after.CpuCurve!.SequenceEqual(before.CpuCurve!) || !after.CpuBreakpoints!.SequenceEqual(before.CpuBreakpoints!))
        throw new Exception("Same-value setter probe changed stable state; inspect hardware, no guessed rollback");
    transport.Record("Verified", new { Test = "Standalone same-value Set_Data and Set_Fan", final = after });
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { Success = true, Identity = MsiControlStateReader.VerifyIdentity(), Before = before, After = after,
        Setters = new[] { "Set_Data: same current performance byte", "Set_Fan: same current CPU table" }, Note = "Exact current values only; stable profile/mode/boost/curve/temperature points remained unchanged; no retry or guessed rollback" }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

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
if (args.Contains("--gpu-valid")) { Console.WriteLine("44"); return 0; }
if (args.Contains("--gpu-invalid")) { Console.WriteLine("N/A"); return 0; }
if (args.Contains("--gpu-error")) { Console.WriteLine("44"); return 1; }

foreach (byte unvalidatedMode in new byte[] { 0x1d, 0x9d })
foreach (byte approvedMode in new byte[] { 0x0d, 0x8d })
{
    var rejected = false;
    try { new MsiControlTransport().Write(new(0xd4, unvalidatedMode, approvedMode)); }
    catch (InvalidOperationException e) when (e.Message == "Register transformation is not allowlisted.") { rejected = true; }
    if (!rejected) throw new Exception("Unvalidated fan-mode bit reached firmware transport");
}
Console.WriteLine("PASS firmware transport refuses unvalidated fan-mode bit before WMI access");

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
foreach (var invalid in new[] { "", "N/A", "0", "126", "-1", "44\n45", "44.5", "44 C" })
    if (NvidiaGpuTemperature.Parse(invalid) is not null) throw new Exception("Invalid NVIDIA temperature accepted: " + invalid);
if (NvidiaGpuTemperature.Parse("44\r\n") != 44) throw new Exception("NVIDIA integer temperature failed to parse");
var gpuReading = await NvidiaGpuTemperature.ReadAsync(CancellationToken.None, () => Worker("--gpu-valid"));
if (gpuReading.Celsius != 44 || !gpuReading.Source.Contains("NVIDIA driver")) throw new Exception("Valid NVIDIA worker/source failed");
foreach (var flag in new[] { "--gpu-invalid", "--gpu-error" })
    if ((await NvidiaGpuTemperature.ReadAsync(CancellationToken.None, () => Worker(flag))).Celsius is not null) throw new Exception("Invalid NVIDIA worker must remain unavailable");
var missing = await NvidiaGpuTemperature.ReadAsync(CancellationToken.None, () => new System.Diagnostics.ProcessStartInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
if (missing.Celsius is not null) throw new Exception("Missing NVIDIA executable must remain unavailable");
var timedGpu = await NvidiaGpuTemperature.ReadAsync(CancellationToken.None, () => Worker("--blocked-worker"), TimeSpan.FromMilliseconds(400), p => workerPid = p.Id);
if (timedGpu.Celsius is not null || !timedGpu.Source.Contains("timed out") || !WorkerStopped()) throw new Exception("NVIDIA timeout must stop its worker");
using (var gpuCancel = new CancellationTokenSource(400))
{
    var gpuCancelled = false;
    try { await NvidiaGpuTemperature.ReadAsync(gpuCancel.Token, () => Worker("--blocked-worker"), onStarted: p => workerPid = p.Id); }
    catch (OperationCanceledException) { gpuCancelled = true; }
    if (!gpuCancelled || !WorkerStopped()) throw new Exception("NVIDIA cancellation must stop its worker");
}
Console.WriteLine("PASS NVIDIA GPU fallback: valid/invalid/nonzero exit/missing tool/timeout/cancellation");
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
