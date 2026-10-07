using System.Text.Json;
using QuietMSI.Core;
using QuietMSI.Hardware;

namespace QuietMSI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
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
            if (!created) return 0;
            UIHost.Run(new WindowsHardwareBridge());
            return 0;
        }
        catch (Exception e)
        {
            if (args.Length == 0) MessageBox.Show(e.Message, "QuietMSI could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
