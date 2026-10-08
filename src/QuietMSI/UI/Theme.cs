namespace QuietMSI.UI;

internal sealed class Theme : IDisposable
{
    public bool Dark { get; set; }
    public Color Background => Pick(Color.FromArgb(245, 246, 248), Color.FromArgb(20, 23, 29), SystemColors.Window);
    public Color Surface => Pick(Color.White, Color.FromArgb(28, 32, 39), SystemColors.Window);
    public Color ControlSurface => Pick(Color.FromArgb(242, 244, 246), Color.FromArgb(38, 43, 51), SystemColors.Window);
    public Color Ink => Pick(Color.FromArgb(17, 30, 48), Color.FromArgb(235, 242, 247), SystemColors.WindowText);
    public Color Muted => Pick(Color.FromArgb(85, 103, 123), Color.FromArgb(169, 186, 201), SystemColors.WindowText);
    public Color Border => Pick(Color.FromArgb(226, 229, 233), Color.FromArgb(48, 54, 63), SystemColors.WindowText);
    public Color Selected => Pick(Color.FromArgb(230, 247, 246), Color.FromArgb(32, 65, 68), SystemColors.Window);
    public Color Header => Surface;
    public Color HeaderInk => Ink;
    public Color Accent => Pick(Color.FromArgb(0, 127, 125), Color.FromArgb(73, 207, 197), SystemColors.Highlight);
    public Color AccentInk => Pick(Color.White, Color.FromArgb(15, 34, 36), SystemColors.HighlightText);
    public Color Warning => Pick(Color.FromArgb(176, 92, 0), Color.FromArgb(255, 181, 85), SystemColors.WindowText);
    private Color Pick(Color light, Color dark, Color contrast) => SystemInformation.HighContrast ? contrast : Dark ? dark : light;
    public Font Small { get; } = new("Segoe UI", 9);
    public Font Body { get; } = new("Segoe UI", 10);
    public Font Heading { get; } = new("Segoe UI", 13, FontStyle.Bold);
    public Font Title { get; } = new("Segoe UI", 17, FontStyle.Bold);
    public Font CompactReading { get; } = new("Segoe UI", 18, FontStyle.Bold);
    public Font Reading { get; } = new("Segoe UI", 26, FontStyle.Bold);

    public void Dispose()
    {
        Small.Dispose(); Body.Dispose(); Heading.Dispose(); Title.Dispose(); Reading.Dispose(); CompactReading.Dispose();
    }
}
