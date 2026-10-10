using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace QuietMSI.UI;

public sealed record StartupState(bool Enabled, string Message);
public interface IStartupRegistration
{
    Task<StartupState> ReadAsync();
    Task<StartupState> SetAsync(bool enabled);
}

internal sealed class StartupRegistration : IStartupRegistration
{
    private const string Marker = "QuietMSI:Startup:v1";
    private readonly string _executable;
    private readonly string _sid;
    private readonly string _taskSuffix;
    private string TaskName => "Startup-" + _sid + _taskSuffix;
    public StartupRegistration(string? executable = null, Guid? verificationId = null)
    {
        _executable = Path.GetFullPath(executable ?? Environment.ProcessPath ?? throw new InvalidOperationException("Application path unavailable."));
        _sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Current Windows user unavailable.");
        _taskSuffix = verificationId is null ? "" : "-Verification-" + verificationId.Value.ToString("N");
    }
    public Task<StartupState> ReadAsync() => Task.Run(() => WithScheduler(false, false));
    public Task<StartupState> SetAsync(bool enabled) => Task.Run(() => WithScheduler(true, enabled));
    // Preserve the user's opt-in when the installer moves the executable.
    internal Task MigrateEnabledAsync() => Task.Run(() => WithScheduler(true, true, migrateOnly: true));
    internal static bool OwnsInstalledTask(string xml, string executable)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var root = XDocument.Parse(xml).Root;
        return (string?)root?.Element(ns + "RegistrationInfo")?.Element(ns + "Source") == Marker
            && string.Equals((string?)root?.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Command"), executable, StringComparison.OrdinalIgnoreCase);
    }
    internal static string Definition(string executable, string sid)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object value) => new(ns + name, value);
        return new XElement(ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", new object[] { E("Description", "Starts QuietMSI in the tray when this user signs in."), E("Source", Marker) }),
            E("Triggers", new XElement(ns + "LogonTrigger", E("Enabled", "true"), E("UserId", sid), E("Delay", "PT10S"))),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "User"), E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "HighestAvailable"))),
            E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"), E("StartWhenAvailable", "true"), E("Enabled", "true"), E("ExecutionTimeLimit", "PT0S") }),
            new XElement(ns + "Actions", new XAttribute("Context", "User"), E("Exec", new object[] { E("Command", executable), E("Arguments", "--startup"), E("WorkingDirectory", Path.GetDirectoryName(executable)!) })))
            .ToString(SaveOptions.DisableFormatting);
    }
    private StartupState WithScheduler(bool update, bool enabled, bool migrateOnly = false)
    {
        object? service = null, root = null, folder = null, task = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Windows Task Scheduler is unavailable."));
            dynamic scheduler = service!; scheduler.Connect(); root = scheduler.GetFolder("\\");
            try { folder = scheduler.GetFolder("\\QuietMSI"); }
            catch (Exception e) when ((uint)e.HResult is 0x80070002 or 0x80070003)
            {
                if (!update || !enabled || migrateOnly) return new(false, "Off · QuietMSI starts when you open it.");
                folder = ((dynamic)root).CreateFolder("QuietMSI");
            }
            try { task = ((dynamic)folder!).GetTask(TaskName); }
            catch (Exception e) when ((uint)e.HResult is 0x80070002 or 0x80070003) { }
            if (migrateOnly && (task is null || !(bool)((dynamic)task).Enabled))
                return new(false, "Off · QuietMSI starts when you open it.");
            if (task is not null)
            {
                var xml = XDocument.Parse((string)((dynamic)task).Xml);
                XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
                if ((string?)xml.Root?.Element(ns + "RegistrationInfo")?.Element(ns + "Source") != Marker
                    || (string?)xml.Root?.Element(ns + "Principals")?.Element(ns + "Principal")?.Element(ns + "UserId") != _sid)
                    throw new InvalidOperationException("The startup task belongs to another configuration; it was left unchanged.");
                if (!update)
                {
                    var command = (string?)xml.Root?.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Command");
                    var current = string.Equals(command, _executable, StringComparison.OrdinalIgnoreCase);
                    var active = current && (bool)((dynamic)task).Enabled;
                    return new(active, !current ? "Startup points to another folder. Enable it here to update the path." : active ? "On · Starts in the tray after you sign in." : "Off · QuietMSI starts when you open it.");
                }
            }
            if (!update) return new(false, "Off · QuietMSI starts when you open it.");
            if (!enabled)
            {
                if (task is not null) ((dynamic)folder!).DeleteTask(TaskName, 0);
                return new(false, "Off · Automatic startup removed.");
            }
            if (!File.Exists(_executable) || !Path.GetFileName(_executable).Equals("QuietMSI.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Enable startup from the installed QuietMSI executable.");
            var registered = ((dynamic)folder!).RegisterTask(TaskName, Definition(_executable, _sid), 6, _sid, null, 3, null);
            try { if (!(bool)registered.Enabled) throw new InvalidOperationException("Windows did not enable the startup task."); }
            finally { Release(registered); }
            return new(true, "On · Starts in the tray after you sign in.");
        }
        finally { Release(task); Release(folder); Release(root); Release(service); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
