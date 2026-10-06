using System.Runtime.InteropServices;
using System.Text;
using static InternetIPTaskbar.NativeMethods;

namespace InternetIPTaskbar;

internal enum TaskbarEdge { Bottom, Top, Left, Right }

internal readonly record struct TaskbarInfo(
    Rectangle Bounds,
    Rectangle Notify,
    Rectangle Monitor,
    TaskbarEdge Edge,
    bool AutoHide,
    bool Visible)
{
    public bool Horizontal => Edge is TaskbarEdge.Bottom or TaskbarEdge.Top;
}

internal static class Taskbar
{
    /// <summary>Position of the primary taskbar, or null when Explorer is not available.</summary>
    public static TaskbarInfo? Query()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var r)) return null;

        var bounds = r.ToRectangle();
        var monitor = Screen.FromRectangle(bounds).Bounds;

        var notify = Rectangle.Empty;
        var notifyWnd = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notifyWnd != IntPtr.Zero && GetWindowRect(notifyWnd, out var nr)) notify = nr.ToRectangle();

        TaskbarEdge edge;
        if (bounds.Width >= bounds.Height)
            edge = bounds.Top + bounds.Height / 2 > monitor.Top + monitor.Height / 2 ? TaskbarEdge.Bottom : TaskbarEdge.Top;
        else
            edge = bounds.Left + bounds.Width / 2 < monitor.Left + monitor.Width / 2 ? TaskbarEdge.Left : TaskbarEdge.Right;

        var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        bool autoHide = ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;

        // An auto-hidden taskbar slides almost completely off the screen edge.
        var shown = Rectangle.Intersect(bounds, monitor);
        int thickness = edge is TaskbarEdge.Bottom or TaskbarEdge.Top ? shown.Height : shown.Width;
        bool visible = !autoHide || thickness >= 16;

        return new TaskbarInfo(bounds, notify, monitor, edge, autoHide, visible);
    }
}

internal static class FullscreenDetector
{
    static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    /// <summary>True when a full-screen application (game, video, presentation) is on top.</summary>
    public static bool IsActive(IntPtr self, Rectangle monitor)
    {
        // 3 = full-screen Direct3D, 4 = presentation mode
        if (SHQueryUserNotificationState(out int state) == 0 && state is 3 or 4) return true;

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == self || IsZoomed(hwnd)) return false;

        var cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        if (ShellClasses.Contains(cls.ToString())) return false;

        if (!GetWindowRect(hwnd, out var r)) return false;
        var rect = r.ToRectangle();
        return rect.Left <= monitor.Left && rect.Top <= monitor.Top
            && rect.Right >= monitor.Right && rect.Bottom >= monitor.Bottom;
    }
}
