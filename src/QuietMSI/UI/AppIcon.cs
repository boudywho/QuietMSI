namespace QuietMSI.UI;

internal static class AppIcon
{
    public static Icon Load(int size = 32)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("QuietMSI.AppIcon.ico")
            ?? throw new InvalidOperationException("The QuietMSI app icon is missing.");
        using var icon = new Icon(stream, new Size(size, size));
        return (Icon)icon.Clone();
    }
}
