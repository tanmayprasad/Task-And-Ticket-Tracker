using System;
using System.Runtime.InteropServices;

namespace TaskTrackerApp.Widgets;

/// <summary>
/// Finds the primary Windows taskbar and computes where the taskbar strip should sit: vertically centred in the
/// taskbar, right-aligned just left of the notification area (system tray). Physical pixels throughout.
/// </summary>
internal static class TaskbarLocator
{
    private const int GapDip = 8;
    private const int VerticalPaddingDip = 4;

    public readonly record struct Placement(PixelRect Strip, PixelRect Taskbar, IntPtr Monitor);

    /// <summary>
    /// Computes the strip rectangle. Returns false when there is no taskbar or it is vertical (left/right),
    /// which the strip does not support.
    /// </summary>
    public static bool TryGetPlacement(int stripWidthDip, double scale, out Placement placement)
    {
        placement = default;

        var tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return false;

        var abd = new NativeMethods.APPBARDATA { cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(), hWnd = tray };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref abd);
        if (abd.uEdge == NativeMethods.ABE_LEFT || abd.uEdge == NativeMethods.ABE_RIGHT) return false;

        var taskbar = NativeMethods.GetWindowPixelRect(tray);
        if (taskbar.IsEmpty) return false;

        int gap = (int)Math.Round(GapDip * scale);
        int padding = (int)Math.Round(VerticalPaddingDip * scale);
        int width = (int)Math.Round(stripWidthDip * scale);
        int height = Math.Max(20, taskbar.Height - 2 * padding);

        // Anchor to the left edge of the notification area; fall back to a fixed offset from the right if it
        // can't be found or reports a nonsensical rect
        int anchorRight = taskbar.Right - (int)Math.Round(260 * scale);
        var notify = NativeMethods.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify != IntPtr.Zero)
        {
            var notifyRect = NativeMethods.GetWindowPixelRect(notify);
            if (!notifyRect.IsEmpty && notifyRect.X > taskbar.X && notifyRect.X <= taskbar.Right)
            {
                anchorRight = notifyRect.X - gap;
            }
        }

        var strip = new PixelRect(anchorRight - width, taskbar.Y + (taskbar.Height - height) / 2, width, height);
        placement = new Placement(strip, taskbar, NativeMethods.MonitorFromWindow(tray, NativeMethods.MONITOR_DEFAULTTONEAREST));
        return true;
    }

    /// <summary>True when the taskbar is actually on screen (false while an auto-hide taskbar is slid away).</summary>
    public static bool IsTaskbarShown(in Placement placement)
    {
        if (!NativeMethods.TryGetMonitorInfo(placement.Monitor, out var info)) return true;
        var monitor = info.rcMonitor.ToPixelRect();
        var tb = placement.Taskbar;
        int visibleHeight = Math.Min(tb.Bottom, monitor.Bottom) - Math.Max(tb.Y, monitor.Y);
        return visibleHeight >= tb.Height * 0.9;
    }

    /// <summary>
    /// True when the foreground window covers the whole monitor the taskbar is on (video, game, F11 browser,
    /// slideshow). The taskbar is hidden then, so the strip must hide too.
    /// </summary>
    public static bool IsFullscreenAppActive(in Placement placement, IntPtr ownWindow)
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownWindow || fg == NativeMethods.GetShellWindow() || fg == NativeMethods.GetDesktopWindow())
            return false;

        // Desktop, taskbar and shell surfaces (Start, Search, notification centre are full-screen CoreWindows)
        var cls = NativeMethods.GetWindowClass(fg);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow")
            return false;
        if (!NativeMethods.IsWindowVisible(fg) || NativeMethods.IsCloaked(fg)) return false;

        if (NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST) != placement.Monitor) return false;
        if (!NativeMethods.TryGetMonitorInfo(placement.Monitor, out var info)) return false;

        var fgRect = NativeMethods.GetWindowPixelRect(fg);
        return fgRect.Contains(info.rcMonitor.ToPixelRect());
    }
}
