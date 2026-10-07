namespace QuietMSI.UI;

internal sealed class Theme : IDisposable
{
    public Color Background => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(242, 244, 247);
    public Color Surface => SystemInformation.HighContrast ? SystemColors.Window : Color.White;
    public Color Ink => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(34, 42, 53);
    public Color Muted => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(82, 94, 108);
    public Color Header => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(31, 40, 53);
    public Color HeaderInk => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(242, 246, 250);
    public Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(50, 87, 132);
    public Font Body { get; } = new("Segoe UI", 10);
    public Font Heading { get; } = new("Segoe UI", 12, FontStyle.Bold);
    public Font Title { get; } = new("Segoe UI", 19, FontStyle.Bold);
    public Font Reading { get; } = new("Segoe UI", 20, FontStyle.Regular);

    public void Dispose()
    {
        Body.Dispose(); Heading.Dispose(); Title.Dispose(); Reading.Dispose();
    }
}
