using System.Text.Json;
using QuietMSI.Core;
using QuietMSI.Hardware;

namespace QuietMSI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            if (e.ExceptionObject is Exception error) UI.RuntimeLog.Write("Unhandled", error);
        };
        try
        {
            if (args.Length == 1 && args[0] == "--setup-integration") { InstallationIntegration.Install(); return 0; }
            if (args.Length == 1 && args[0] == "--uninstall-integration") { InstallationIntegration.Uninstall(); return 0; }
            if (args.Length == 1 && args[0] == "--read-worker")
            {
                var sample = new WindowsWmiSampler().ReadSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
                using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                stdout.Write(JsonSerializer.Serialize(sample));
                return 0;
            }
            if (args.Length > 0 && args[0] == "--diagnostics")
            {
                if (args.Length != 2) return 2;
                FirmwareSupport.EnsureInstalled();
                var bridge = new WindowsHardwareBridge();
                var snapshot = bridge.ReadSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(Path.GetFullPath(args[1]), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            if (args.Length > 0 && args[0] == "--smoke-test")
            {
                UIHost.Run(new ReadOnlyBridge(), true, args.Length > 1 ? Path.GetFullPath(args[1]) : null);
                return 0;
            }
            using var mutex = new Mutex(true, "Local\\QuietMSI.TrayApplication", out var created);
            if (args.Length == 1 && args[0] == "--install-support")
            {
                FirmwareSupport.EnsureInstalled();
                return 0;
            }
            var startup = args.Length == 1 && args[0] == "--startup";
            if (!created) { if (!startup) UI.AppInstance.ShowExisting(); return 0; }
            try { FirmwareSupport.EnsureInstalled(); UI.HardwareSupportStatus.Text = "Independent firmware support installed · MSI Center services are not required."; }
            catch (Exception error) { UI.RuntimeLog.Write("Hardware support setup", error); UI.HardwareSupportStatus.Text = "Hardware support needs attention · " + error.Message; }
            if (startup) UIHost.RunStartup(new WindowsHardwareBridge());
            else UIHost.Run(new WindowsHardwareBridge());
            return 0;
        }
        catch (Exception e)
        {
            UI.RuntimeLog.Write("Application", e);
            if (args.Length == 0) MessageBox.Show(e.Message + "\n\nDetails saved to " + UI.RuntimeLog.DefaultPath,
                "QuietMSI encountered an error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
