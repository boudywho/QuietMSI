using QuietMSI.Core;
using QuietMSI.Hardware;
using System.Text.Json;

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
if (string.IsNullOrWhiteSpace(snapshot.Identity.Model) || snapshot.Identity.Model == "Unknown") throw new Exception("Real device identity must be discovered");
if (snapshot.BatteryPercent is < 0 or > 100) throw new Exception("Battery out of bounds");
foreach (var kind in Enum.GetValues<ControlKind>())
{
    var result = await bridge.ApplyAsync(new(kind, "test"), CancellationToken.None);
    if (result.Applied) throw new Exception("Read-only transport attempted a write");
}
Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS Windows bridge discovers real identity and denies every hardware write");
return 0;
