using QuietMSI.UI;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Expected QuietMSI executable and result file.");
var verificationId = Guid.NewGuid();
var backend = new StartupRegistration(args[0], verificationId);
var migrationFolder = Path.Combine(Path.GetTempPath(), "QuietMSI-Migration-" + verificationId.ToString("N"));
var result = new Dictionary<string, object>();
try
{
    var before = await backend.ReadAsync();
    if (before.Enabled) throw new Exception("Verification task unexpectedly exists.");
    await backend.MigrateEnabledAsync();
    if ((await backend.ReadAsync()).Enabled) throw new Exception("Installation enabled absent startup without opt-in.");
    var enabled = await backend.SetAsync(true);
    var readback = await backend.ReadAsync();
    if (!enabled.Enabled || !readback.Enabled) throw new Exception("Real Task Scheduler registration was not enabled.");
    result["EnabledReadback"] = readback;
    Directory.CreateDirectory(migrationFolder);
    var migratedPath = Path.Combine(migrationFolder, "QuietMSI.exe");
    File.Copy(args[0], migratedPath);
    var migrated = new StartupRegistration(migratedPath, verificationId);
    await migrated.MigrateEnabledAsync();
    if (!(await migrated.ReadAsync()).Enabled || (await backend.ReadAsync()).Enabled)
        throw new Exception("Startup migration did not replace the old executable path.");
    result["MigrationVerified"] = true;
    await backend.SetAsync(false);
    var disabled = await backend.ReadAsync();
    if (disabled.Enabled) throw new Exception("Real Task Scheduler task was not removed.");
    result["DisabledReadback"] = disabled;
    result["Success"] = true;
}
catch (Exception error) { result["Success"] = false; result["Error"] = error.ToString(); }
finally
{
    try { await backend.SetAsync(false); result["Cleanup"] = true; }
    catch (Exception error) { result["Cleanup"] = false; result["CleanupError"] = error.ToString(); }
    File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    var temporaryExecutable = Path.Combine(migrationFolder, "QuietMSI.exe");
    if (File.Exists(temporaryExecutable)) File.Delete(temporaryExecutable);
    if (Directory.Exists(migrationFolder)) Directory.Delete(migrationFolder);
}
return (bool)result["Success"] && (bool)result["Cleanup"] ? 0 : 1;
