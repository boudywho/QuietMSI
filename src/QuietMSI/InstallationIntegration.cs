using System.Runtime.InteropServices;
using System.Xml.Linq;
using QuietMSI.UI;

namespace QuietMSI;

internal static class InstallationIntegration
{
    internal static void Install()
    {
        Hardware.FirmwareSupport.EnsureInstalled();
        new StartupRegistration().MigrateEnabledAsync().GetAwaiter().GetResult();
    }

    // Remove only marked startup tasks that point to the executable being removed.
    // Firmware support is shared with portable copies and is retained deliberately.
    internal static void Uninstall()
    {
        object? service = null, folder = null, tasks = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            dynamic scheduler = service!; scheduler.Connect();
            try { folder = scheduler.GetFolder("\\QuietMSI"); }
            catch (Exception e) when ((uint)e.HResult is 0x80070002 or 0x80070003) { return; }
            tasks = ((dynamic)folder).GetTasks(1);
            var names = new List<string>();
            for (var i = 1; i <= (int)((dynamic)tasks).Count; i++)
            {
                object task = ((dynamic)tasks).Item(i);
                try
                {
                    if (StartupRegistration.OwnsInstalledTask((string)((dynamic)task).Xml, Environment.ProcessPath!))
                        names.Add((string)((dynamic)task).Name);
                }
                finally { Release(task); }
            }
            foreach (var name in names) ((dynamic)folder).DeleteTask(name, 0);
        }
        finally { Release(tasks); Release(folder); Release(service); }
    }

    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
