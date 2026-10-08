using Microsoft.Win32;

namespace QuietMSI.UI;

internal sealed class ThemeSettings
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietMSI", "appearance.json");
    private readonly string? _path;
    public ThemeSettings(string? path) => _path = path;
    public string Read()
    {
        try { if (_path is not null && File.Exists(_path)) { var value = System.Text.Json.JsonSerializer.Deserialize<string>(File.ReadAllText(_path)); if (value is "Light" or "Dark" or "System") return value; } }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { }
        return "System";
    }
    public bool Write(string value)
    {
        if (_path is null) return true;
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!); File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(value)); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
    public static bool SystemDark()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }
}
