using System.Drawing.Imaging;
using QuietMSI.Core;
using QuietMSI.UI;

namespace QuietMSI;

public static class UIHost
{
    /// <summary>Run from the application's STA entry point. Smoke never reads the supplied bridge.</summary>
    public static void Run(IHardwareBridge bridge, bool smokeTest = false, string? screenshotPath = null)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("QuietMSI UI must run on an STA thread.");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        if (!smokeTest)
        {
            using var context = new TrayContext(bridge);
            context.ShowWindow();
            Application.Run(context);
            return;
        }

        using var form = new MainForm(new ReadOnlyBridge());
        using var deadline = new System.Windows.Forms.Timer { Interval = 8000 };
        Exception? failure = null;
        deadline.Tick += (_, _) =>
        {
            failure = new TimeoutException("QuietMSI UI smoke did not finish within 8 seconds.");
            form.Close();
        };
        form.Shown += async (_, _) =>
        {
            try
            {
                await form.PollAsync();
                var hardware = Descendants(form).Where(control => control.Name.StartsWith("Hardware_", StringComparison.Ordinal)).ToArray();
                if (hardware.Length != 6 || hardware.Any(control => control.Enabled))
                    throw new InvalidOperationException("Read-only UI smoke found missing or enabled hardware controls.");
                if (form.LastSnapshot?.CpuCelsius is not null || form.LastSnapshot?.GpuCelsius is not null)
                    throw new InvalidOperationException("Read-only smoke must not invent sensor values.");
                if (screenshotPath is not null)
                {
                    form.Refresh();
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(screenshotPath, ImageFormat.Png);
                }
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                deadline.Stop();
                if (!form.IsDisposed) form.BeginInvoke((Action)form.Close);
            }
        };
        deadline.Start();
        Application.Run(form);
        if (failure is not null) throw new InvalidOperationException("QuietMSI UI smoke failed.", failure);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
