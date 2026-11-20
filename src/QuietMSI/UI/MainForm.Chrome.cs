namespace QuietMSI.UI;

public sealed partial class MainForm
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeAppearance.Apply(this, _theme.Dark);
    }

    protected override void WndProc(ref Message message)
    {
        if (AppInstance.ShowMessage != 0 && message.Msg == AppInstance.ShowMessage)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
    }
}
