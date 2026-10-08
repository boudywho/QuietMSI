using System.Xml.Linq;
using QuietMSI.UI;
using QuietMSI.Core;

internal static partial class Program
{
    private sealed class StartupFixture : IStartupRegistration
    {
        internal TaskCompletionSource<StartupState> Initial = new();
        internal TaskCompletionSource<StartupState> Change = new();
        internal List<bool> Requests = new();
        public Task<StartupState> ReadAsync() => Initial.Task;
        public Task<StartupState> SetAsync(bool enabled) { Requests.Add(enabled); return Change.Task; }
    }
    private static void TestSettings()
    {
        var startup = new StartupFixture(); var bridge = new SyntheticControlBridge();
        using var form = new MainForm(bridge, null, startup);
        form.Show(); UiEvents(); SelectPage(form, "Settings");
        var toggle = Descendants(form).OfType<CheckBox>().Single(c => c.Name == "Settings_AutoStart");
        Check(!toggle.Enabled && !toggle.Checked, "Settings: wait for the actual Windows startup state before allowing edits.");
        startup.Initial.SetResult(new(false, "Off")); Pump(form.StartupLoaded); UiEvents();
        Check(toggle.Enabled && !toggle.Checked && startup.Requests.Count == 0, "Settings: loading must never register startup.");
        var theme = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ThemeMode");
        Check(theme.Visible && theme.Parent!.Parent!.Parent!.Name == "AppearanceCard", "Settings: theme must live in the Appearance card.");
        toggle.Checked = true; UiEvents();
        Check(!toggle.Enabled && startup.Requests.SequenceEqual(new[] { true }), "Settings: serialize a pending startup update.");
        startup.Change.SetResult(new(true, "On")); UiEvents();
        Check(toggle.Checked && toggle.Enabled, "Settings: display verified startup registration.");
        startup.Change = new(); toggle.Checked = false;
        startup.Change.SetException(new IOException("fixture scheduler failure")); UiEvents();
        Check(toggle.Checked && toggle.Enabled && Descendants(form).Single(c => c.Name == "StartupStatus").Text.Contains("fixture scheduler failure"), "Settings: failed removal must preserve the actual setting and expose the error.");
        foreach (var size in new[] { new Size(440, 600), new Size(760, 760) })
        {
            form.ClientSize = size; UiEvents();
            var cards = Descendants(form).Where(c => c.Name is "AppearanceCard" or "StartupCard" or "UpdatesCard" or "AboutCard").OrderBy(c => c.Top).ToArray();
            Check(cards.All(c => c.Visible && c.Width > 0) && cards.Zip(cards.Skip(1)).All(pair => pair.First.Bottom <= pair.Second.Top), "Settings: cards must remain distinct and reachable at compact/wide sizes.");
        }
        SelectPage(form, "Overview"); Check(!theme.Visible, "Settings: theme must not remain in navigation.");
        Check(bridge.Requests.Count == 0, "Settings: no hardware effects from startup, themes or navigation.");
        Console.WriteLine("PASS Settings (held loading/update, enable verification, failed removal rollback, dedicated theme card, compact/wide; no real task or hardware changes)");
    }
    private static void TestStartupDefinition()
    {
        var executable = @"C:\Apps & Tools\QuietMSI\QuietMSI.exe"; var sid = "S-1-5-21-123-456-789-1001";
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var xml = XDocument.Parse(StartupRegistration.Definition(executable, sid));
        Check(StartupRegistration.OwnsInstalledTask(xml.ToString(), executable.ToUpperInvariant())
            && !StartupRegistration.OwnsInstalledTask(xml.ToString(), @"C:\Portable\QuietMSI.exe")
            && !StartupRegistration.OwnsInstalledTask(xml.ToString().Replace("QuietMSI:Startup:v1", "Other"), executable),
            "Uninstall: match owned task and exact executable path; preserve portable/foreign tasks.");
        string Value(string name) => xml.Descendants(ns + name).Last().Value;
        Check(Value("Command") == executable && Value("Arguments") == "--startup" && Value("UserId") == sid, "Startup: escaped absolute executable path and current user scope.");
        Check(Value("LogonType") == "InteractiveToken" && Value("RunLevel") == "HighestAvailable", "Startup: elevated interactive user without password storage.");
        Check(Value("Source") == "QuietMSI:Startup:v1", "Startup: persist an ownership marker in Source; Windows replaces the URI with its own task path.");
        Check(Value("DisallowStartIfOnBatteries") == "false" && Value("StopIfGoingOnBatteries") == "false" && Value("ExecutionTimeLimit") == "PT0S", "Startup: tray app must continue on battery and after 72 hours.");
        Check(!xml.Descendants(ns + "Password").Any() && Value("MultipleInstancesPolicy") == "IgnoreNew", "Startup: no credentials or overlapping instances.");
        Console.WriteLine("PASS StartupDefinition (logon/elevation/user/path/battery/no execution limit; no registered task)");
        using var tray = new TrayContext(new ReadOnlyBridge(), startInTray: true);
        Check(!tray.Window.Visible && tray.Window.IsHandleCreated && tray.PollIntervalMilliseconds == 30000, "Startup: start hidden with a message receiver and slower tray polling.");
        SendMessage(tray.Window.Handle, AppInstance.ShowMessage, IntPtr.Zero, IntPtr.Zero); UiEvents();
        Check(tray.Window.Visible && tray.PollIntervalMilliseconds == 5000, "Startup: a normal second launch must reopen an app that started in the tray.");
        tray.ExitApplication(); UiEvents();
        Console.WriteLine("PASS StartupTray (initial hidden HWND, 30-second polling, second-launch reopen, explicit exit)");
    }
}
