using System.Runtime.InteropServices;

namespace QuietMSI.UI;

internal static class AppInstance
{
    internal static readonly int ShowMessage = RegisterWindowMessage("QuietMSI.RestoreWindow.v1");
    internal static void ShowExisting() => PostMessage(new IntPtr(0xffff), ShowMessage, IntPtr.Zero, IntPtr.Zero);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
