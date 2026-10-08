using System.Reflection;

namespace QuietMSI.UI;

public sealed partial class MainForm
{
    private RoundedCard _appearanceCard = null!, _startupCard = null!, _updateCard = null!, _aboutCard = null!;
    private readonly IStartupRegistration _startupRegistration;
    private CheckBox _autoStart = null!;
    private Label _startupStatus = null!;
    private bool _loadingStartup, _changingStartup;
    internal Task StartupLoaded { get; private set; } = Task.CompletedTask;

    private void InitializeSettings()
    {
        var appearance = Stack(_theme.Surface, Padding.Empty);
        appearance.Controls.Add(TextLabel("Appearance", _theme.Heading));
        appearance.Controls.Add(TextLabel("Choose how QuietMSI looks. System follows your Windows theme.", _theme.Small));
        var row = ActionRow(appearance);
        row.Controls.Add(TextLabel("App theme"));
        _themeSelector.Width = D(160); _themeSelector.Margin = new Padding(14, 4, 0, 8);
        row.Controls.Add(_themeSelector);
        _appearanceCard = WrapCard(appearance, "AppearanceCard");
        _appearanceCard.AccessibleName = "Appearance settings";

        var startup = Stack(_theme.Surface, Padding.Empty);
        startup.Controls.Add(TextLabel("Startup", _theme.Heading));
        _autoStart = new CheckBox { Name = "Settings_AutoStart", Text = "Start with Windows", AccessibleName = "Start QuietMSI with Windows", AutoSize = true, Enabled = false, Margin = new Padding(0, 10, 0, 8) };
        startup.Controls.Add(_autoStart);
        startup.Controls.Add(TextLabel("Start quietly in the system tray when you sign in. Your hardware settings are never changed automatically.", _theme.Small));
        _startupStatus = TextLabel("Checking startup…", _theme.Small); _startupStatus.Name = "StartupStatus"; startup.Controls.Add(_startupStatus);
        _autoStart.CheckedChanged += async (_, _) => {
            if (_loadingStartup || _changingStartup) return;
            _changingStartup = true; _autoStart.Enabled = false;
            var desired = _autoStart.Checked;
            try
            {
                var state = await _startupRegistration.SetAsync(desired);
                if (IsDisposed) return;
                RenderStartup(state);
            }
            catch (Exception error)
            {
                if (IsDisposed) return;
                RuntimeLog.Write("Startup setting", error);
                _loadingStartup = true; _autoStart.Checked = !desired; _loadingStartup = false;
                _startupStatus.Text = "Could not change startup · " + error.Message;
            }
            finally { _changingStartup = false; if (!IsDisposed) { _autoStart.Enabled = true; if (_page == "Settings") LayoutDashboard(); } }
        };
        _startupCard = WrapCard(startup, "StartupCard"); _startupCard.AccessibleName = "Startup settings";

        var updates = Stack(_theme.Surface, Padding.Empty);
        updates.Controls.Add(TextLabel("Updates", _theme.Heading));
        updates.Controls.Add(TextLabel("Download a newer installer from GitHub to update. Your settings are preserved.", _theme.Small));
        var updateRow = ActionRow(updates);
        var check = NativeButton("Check for updates"); check.Name = "Settings_CheckUpdates";
        var downloads = NativeButton("Open releases"); downloads.Name = "Settings_OpenReleases";
        updateRow.Controls.AddRange(new Control[] { check, downloads });
        var updateStatus = TextLabel("Checks run only when you request them, including experimental releases.", _theme.Small);
        updateStatus.Name = "UpdateStatus"; updates.Controls.Add(updateStatus);
        var releasePage = UpdateChecker.ReleasesPage;
        check.Click += async (_, _) => {
            check.Enabled = false; updateStatus.Text = "Checking GitHub…";
            try
            {
                var release = await UpdateChecker.CheckAsync(_shutdown.Token);
                if (IsDisposed) return;
                releasePage = release?.Page.AbsoluteUri ?? UpdateChecker.ReleasesPage;
                updateStatus.Text = release is null ? "No newer downloadable release found." : $"Version {release.Version.ToString(3)} is available · Open releases to download.";
            }
            catch (Exception error)
            {
                if (IsDisposed || _shutdown.IsCancellationRequested) return;
                updateStatus.Text = error is OperationCanceledException ? "The update check timed out. Try again later." : "Could not check updates · " + error.Message;
            }
            finally { if (!IsDisposed) { check.Enabled = true; if (_page == "Settings") LayoutDashboard(); } }
        };
        downloads.Click += (_, _) => {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(releasePage) { UseShellExecute = true }); }
            catch (Exception error) { updateStatus.Text = "Could not open GitHub · " + error.Message; LayoutDashboard(); }
        };
        _updateCard = WrapCard(updates, "UpdatesCard"); _updateCard.AccessibleName = "Application updates";

        var about = Stack(_theme.Surface, Padding.Empty);
        about.Controls.Add(TextLabel("About QuietMSI", _theme.Heading));
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        about.Controls.Add(TextLabel($"Version {version?.ToString(3) ?? "development"} · Native Windows app", _theme.Body));
        about.Controls.Add(TextLabel(HardwareSupportStatus.Text, _theme.Small));
        about.Controls.Add(TextLabel("Close or minimize to keep the app in the tray. Use the tray menu to exit.", _theme.Small));
        _aboutCard = WrapCard(about, "AboutCard"); _aboutCard.AccessibleName = "About QuietMSI";
        _dashboard.Controls.AddRange(new Control[] { _appearanceCard, _startupCard, _updateCard, _aboutCard });
    }
    private void RenderStartup(StartupState state)
    {
        _loadingStartup = true;
        try { _autoStart.Checked = state.Enabled; _startupStatus.Text = state.Message; }
        finally { _loadingStartup = false; }
    }
    private async Task LoadStartupAsync()
    {
        try { var state = await _startupRegistration.ReadAsync(); if (!IsDisposed) RenderStartup(state); }
        catch (Exception error) { if (!IsDisposed) _startupStatus.Text = "Startup unavailable · " + error.Message; }
        finally { if (!IsDisposed) { _autoStart.Enabled = true; if (_page == "Settings") LayoutDashboard(); } }
    }
}
