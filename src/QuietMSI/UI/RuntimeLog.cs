using System.Reflection;
using System.Text;

namespace QuietMSI.UI;

internal static class RuntimeLog
{
    private static readonly object Gate = new();
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietMSI", "logs", "application.log");
    internal static string LogPath { get; set; } = DefaultPath;
    internal static void Write(string operation, Exception exception, string? path = null)
    {
        try
        {
            lock (Gate)
            {
                path ??= LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                    File.Move(path, path + ".previous", overwrite: true);
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} QuietMSI {version} [{operation}] {exception}\n", Encoding.UTF8);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { /* A failed support log must never prevent sampling or shutdown. */ }
    }
}
