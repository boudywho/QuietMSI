namespace QuietMSI.UI;

internal sealed class Theme : IDisposable
{
    public bool Dark { get; set; }
    public Color Background => Pick(Color.FromArgb(237, 242, 244), Color.FromArgb(13, 20, 24), SystemColors.Window);
    public Color Surface => Pick(Color.White, Color.FromArgb(23, 34, 39), SystemColors.Window);
    public Color ControlSurface => Pick(Color.FromArgb(234, 240, 242), Color.FromArgb(31, 46, 52), SystemColors.Window);
    public Color Ink => Pick(Color.FromArgb(18, 34, 40), Color.FromArgb(233, 243, 244), SystemColors.WindowText);
    public Color Muted => Pick(Color.FromArgb(70, 91, 98), Color.FromArgb(169, 192, 196), SystemColors.WindowText);
    public Color Border => Pick(Color.FromArgb(202, 216, 220), Color.FromArgb(52, 71, 77), SystemColors.WindowText);
    public Color Selected => Pick(Color.FromArgb(214, 239, 230), Color.FromArgb(33, 67, 60), SystemColors.Highlight);
    public Color SelectedInk => SystemInformation.HighContrast ? SystemColors.HighlightText : Accent;
    public Color Accent => Pick(Color.FromArgb(0, 103, 77), Color.FromArgb(115, 230, 190), SystemColors.Highlight);
    public Color AccentInk => Pick(Color.White, Color.FromArgb(13, 33, 28), SystemColors.HighlightText);
    private Color Pick(Color light, Color dark, Color contrast) => SystemInformation.HighContrast ? contrast : Dark ? dark : light;
    public Font Small { get; } = new("Segoe UI", 9);
    public Font Body { get; } = new("Segoe UI", 10);
    public Font Heading { get; } = new("Segoe UI", 13, FontStyle.Bold);
    public Font Title { get; } = new("Segoe UI", 17, FontStyle.Bold);
    public Font CompactReading { get; } = new("Segoe UI", 18, FontStyle.Bold);

    public void Dispose()
    {
        Small.Dispose(); Body.Dispose(); Heading.Dispose(); Title.Dispose(); CompactReading.Dispose();
    }
}
