using QuietMSI.Core;

namespace QuietMSI.UI;

public sealed partial class MainForm
{
    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Color or Microsoft.Win32.UserPreferenceCategory.VisualStyle or Microsoft.Win32.UserPreferenceCategory.Accessibility)) return;
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => { if (!IsDisposed) ApplyTheme(); })); }
        catch (InvalidOperationException) { /* Handle was destroyed during shutdown. */ }
    }

    private void ApplyTheme(bool force = false)
    {
        var palette = ((string?)_themeSelector.SelectedItem == "Dark" || ((string?)_themeSelector.SelectedItem == "System" && ThemeSettings.SystemDark()), SystemInformation.HighContrast);
        if (!force && _appliedPalette == palette) return;
        _appliedPalette = palette;
        using var viewportUpdate = _viewport.PreserveViewport();
        _theme.Dark = palette.Item1;
        BackColor = _theme.Background; ForeColor = _theme.Ink;
        void Paint(Control root)
        {
            foreach (Control control in root.Controls)
            {
                control.BackColor = control == _viewport || control == _dashboard ? _theme.Background : _theme.Surface;
                control.ForeColor = control is Label label && label.Font.Size < _theme.Heading.Size ? _theme.Muted : _theme.Ink;
                if (control is Button button) button.UseVisualStyleBackColor = false;
                if (control is TextBox box) box.BorderStyle = BorderStyle.FixedSingle;
                NativeAppearance.Apply(control, _theme.Dark);
                if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
                Paint(control); control.Invalidate();
            }
        }
        NativeAppearance.Apply(this, _theme.Dark);
        Paint(this); _cpu.ForeColor = _gpu.ForeColor = _machine.ForeColor = _theme.Ink;
        _current.ForeColor = _currentMode.ForeColor = _currentBoost.ForeColor = _theme.Accent;
        Invalidate(true);
    }

    private DashboardCard WrapCard(TableLayoutPanel content, string name)
    {
        var card = new DashboardCard(_theme) { Name = name };
        content.Dock = DockStyle.Fill; content.AutoSize = false; content.Padding = Padding.Empty; content.Margin = Padding.Empty;
        card.Controls.Add(content); return card;
    }

    private void Navigate(string page)
    {
        if (_page == page) return;
        _page = page;
        foreach (var (name, button) in _navigation) { button.Selected = name == page; button.Invalidate(); }
        _viewport.AutoScrollPosition = Point.Empty; LayoutDashboard();
    }

    private void FitInitialWindow()
    {
        // The real HWND now has a monitor, DPI and native frame. Size the client only once;
        // later resizes and new telemetry should never resize the window for the user.
        var work = Screen.FromHandle(Handle).WorkingArea;
        var frame = Size - ClientSize;
        var maxWidth = Math.Max(1, work.Width - frame.Width);
        var maxHeight = Math.Max(1, work.Height - frame.Height);
        ClientSize = new Size(Math.Min(D(820), maxWidth), Math.Min(D(650), maxHeight));
        LayoutDashboard();

        var extra = _dashboard.Bottom - _viewport.ClientSize.Height;
        if (extra > 0 && ClientSize.Height < maxHeight)
        {
            ClientSize = new Size(ClientSize.Width, Math.Min(ClientSize.Height + extra, maxHeight));
            LayoutDashboard();
        }

        Location = new Point(work.Left + Math.Max(0, (work.Width - Width) / 2),
            work.Top + Math.Max(0, (work.Height - Height) / 2));
    }

    private void LayoutDashboard()
    {
        if (_dashboardLayout || _sensorCard is null) return;
        using var viewportUpdate = _viewport.PreserveViewport();
        _dashboardLayout = true;
        _viewport.SuspendLayout();
        _dashboard.SuspendLayout();
        try
        {
            var inset = D(18);
            // Reserve scrollbar width even when it isn't visible, so scrolling never toggles the layout breakpoint.
            var width = Math.Max(0, _viewport.Width - 2 * inset - D(20));
            var gap = D(12);
            var wide = width >= D(650);
            _dashboard.SetBounds(inset + _viewport.AutoScrollPosition.X, D(12) + _viewport.AutoScrollPosition.Y, width, _dashboard.Height);
            _sensorCard.Visible = _profileCard.Visible = _coolingCard.Visible = _page == "Overview";
            _chartCard.Visible = _page == "Cooling";
            _editorCard.Visible = _page == "Cooling";
            _appearanceCard.Visible = _startupCard.Visible = _updateCard.Visible = _aboutCard.Visible = _page == "Settings";
            int Fit(DashboardCard card, int x, int y, int w)
            {
                card.Padding = new Padding(D(16));
                card.SetBounds(x, y, Math.Max(0, w), card.Height);
                var content = (TableLayoutPanel)card.Controls[0];
                content.Width = Math.Max(0, w - card.Padding.Horizontal);
                content.PerformLayout();
                card.Height = content.GetPreferredSize(new Size(content.Width, 0)).Height + card.Padding.Vertical;
                card.Visible = true;
                return card.Bottom + gap;
            }
            if (_page == "Overview")
            {
                var left = wide ? (width - gap) / 2 : width;
                _sensorCard.SetBounds(0, 0, width, _sensorCard.Height);
                _sensorCard.Height = _layoutSensors(); _sensorCard.Visible = true;
                var start = _sensorCard.Bottom + gap;
                var y = Fit(_profileCard, 0, start, left);
                Fit(_coolingCard, wide ? left + gap : 0, wide ? start : y, wide ? width - left - gap : width);
                if (wide) _profileCard.Height = _coolingCard.Height = Math.Max(_profileCard.Height, _coolingCard.Height);
            }
            else if (_page == "Cooling")
            {
                _chartCard.SetBounds(0, 0, width, D(224)); _chartCard.Visible = true;
                Fit(_editorCard, 0, _chartCard.Bottom + gap, width);
            }
            else
            {
                var left = wide ? (width - gap) / 2 : width;
                var y = Fit(_appearanceCard, 0, 0, left);
                var startupBottom = Fit(_startupCard, wide ? left + gap : 0, wide ? 0 : y, wide ? width - left - gap : width);
                var secondRow = Math.Max(y, startupBottom);
                y = Fit(_updateCard, 0, secondRow, left);
                Fit(_aboutCard, wide ? left + gap : 0, wide ? secondRow : y, wide ? width - left - gap : width);
            }
            // Visible reports false for every descendant while the tray window is hidden.
            // Measure the selected page explicitly so background samples cannot collapse it.
            var cards = _page == "Overview"
                ? new[] { _sensorCard, _profileCard, _coolingCard }
                : _page == "Cooling" ? new[] { _chartCard, _editorCard } : new[] { _appearanceCard, _startupCard, _updateCard, _aboutCard };
            _dashboard.Height = cards.Max(c => c.Bottom) + D(12);
        }
        finally { _dashboard.ResumeLayout(true); _viewport.ResumeLayout(true); _dashboardLayout = false; }
    }
    private TableLayoutPanel Stack(Color background, Padding padding) => new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Dock = DockStyle.Top, BackColor = background, Padding = padding, Margin = Padding.Empty,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) }
    };

    private Label TextLabel(string text, Font? font = null) => new BufferedLabel()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top, Font = font ?? _theme.Body,
        ForeColor = _theme.Muted, Margin = new Padding(0, 1, 0, 3), UseMnemonic = false, TabStop = false
    };

    private Label StateLabel(string text, int height = 22)
    {
        var label = TextLabel(text, _theme.Small);
        label.AutoSize = false;
        label.Height = height;
        return label;
    }

    private Button NativeButton(string text) => new DashboardButton(_theme)
    {
        Text = text, AccessibleName = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(0, 32), Margin = new Padding(0, 4, 8, 5), UseVisualStyleBackColor = true
    };

    private static FlowLayoutPanel ActionRow(TableLayoutPanel section)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        section.Controls.Add(row);
        return row;
    }

    private void Hardware(Control parent, ControlKind kind, Control control, string name)
    {
        control.Name = "Hardware_" + name;
        control.Enabled = false;
        if (!_hardware.TryGetValue(kind, out var controls)) _hardware[kind] = controls = new();
        controls.Add(control);
        parent.Controls.Add(control);
    }
}
