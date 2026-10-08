namespace QuietMSI.UI;

// Native, focusable buttons expose every firmware level without opening a menu.
internal sealed class ProfileSelector : Panel
{
    private readonly DashboardButton[] _buttons;
    private int _selectedIndex = -1;
    public string[] Items { get; } = { "High", "Medium", "Turbo", "Eco" };
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Length) throw new ArgumentOutOfRangeException(nameof(value));
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            for (var i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].Selected = i == value;
                _buttons[i].AccessibleDescription = i == value ? "Selected draft; Apply profile to confirm" : "Select this power level";
                _buttons[i].Invalidate();
            }
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public object? SelectedItem
    {
        get => _selectedIndex >= 0 ? Items[_selectedIndex] : null;
        set => SelectedIndex = Array.IndexOf(Items, value as string);
    }
    public ProfileSelector(Theme theme)
    {
        Height = 42; TabStop = true;
        _buttons = Items.Select((item, index) =>
        {
            var button = new DashboardButton(theme) { Text = item, AccessibleName = item + " performance level", Font = theme.Body, TabIndex = index };
            button.Click += (_, _) => SelectedIndex = index;
            return button;
        }).ToArray();
        Controls.AddRange(_buttons);
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_buttons is null) return;
        for (var i = 0; i < _buttons.Length; i++)
            _buttons[i].SetBounds(i * Width / 4, 0, (i + 1) * Width / 4 - i * Width / 4 - LogicalToDeviceUnits(5), Height);
    }
}
