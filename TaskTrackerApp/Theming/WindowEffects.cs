using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace TaskTrackerApp.Theming;

/// <summary>
/// Windows 11 window material: Mica backdrop, rounded corners and a title bar that follows the app theme.
/// Mica is drawn by the Desktop Window Manager, so it costs the app no memory or CPU. On Windows 10 the
/// window simply keeps its solid theme background.
/// </summary>
public static class WindowEffects
{
    private const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x00080000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    /// <summary>
    /// The rectangle Windows uses for the native minimize/maximize/close buttons, in device-independent units and
    /// relative to the window's top-left corner. Lets the app line its own title-bar controls up with them.
    /// </summary>
    public static bool TryGetCaptionButtonBounds(Window window, out Rect bounds)
    {
        bounds = Rect.Empty;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            if (DwmGetWindowAttribute(hwnd, DWMWA_CAPTION_BUTTON_BOUNDS, out var r, Marshal.SizeOf<RECT>()) != 0) return false;
            if (r.Right <= r.Left || r.Bottom <= r.Top) return false;
            var dpi = VisualTreeHelper.GetDpi(window);
            bounds = new Rect(r.Left / dpi.DpiScaleX, r.Top / dpi.DpiScaleY, (r.Right - r.Left) / dpi.DpiScaleX, (r.Bottom - r.Top) / dpi.DpiScaleY);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// True once the window uses Mica. Windows then draws its own (native) caption buttons in the extended frame,
    /// so the window must hide any custom minimize/maximize/close buttons. Native buttons also bring Snap Layouts.
    /// </summary>
    public static readonly DependencyProperty UsesSystemCaptionButtonsProperty =
        DependencyProperty.RegisterAttached("UsesSystemCaptionButtons", typeof(bool), typeof(WindowEffects), new PropertyMetadata(false));

    public static bool GetUsesSystemCaptionButtons(Window window) => (bool)window.GetValue(UsesSystemCaptionButtonsProperty);

    /// <summary>System backdrops (Mica) need Windows 11 22H2 (build 22621) or later.</summary>
    public static bool IsMicaSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Applies the effects when the window's handle is created and keeps them in sync with theme changes.</summary>
    /// <param name="customCaption">
    /// The window draws its own minimize / maximize / close buttons (e.g. a taller title bar). Windows then doesn't
    /// draw the native ones, even with Mica; the window must report its maximize button for Snap Layouts itself
    /// and open the system menu on Alt+Space (<see cref="ShowSystemMenu"/>).
    /// </param>
    public static void Attach(Window window, bool customCaption = false)
    {
        EventHandler onThemeChanged = (s, e) => UpdateDarkMode(window);
        window.SourceInitialized += (s, e) => Apply(window, customCaption);
        App.ThemeChanged += onThemeChanged;
        window.Closed += (s, e) => App.ThemeChanged -= onThemeChanged;
    }

    private static void Apply(Window window, bool customCaption)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        SetAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        UpdateDarkMode(window);

        if (!IsMicaSupported) return;

        // Let DWM draw behind the whole window, then make WPF's own background transparent
        var chrome = WindowChrome.GetWindowChrome(window);
        if (chrome != null)
        {
            var updated = (WindowChrome)chrome.Clone();
            updated.GlassFrameThickness = new Thickness(-1);
            updated.UseAeroCaptionButtons = !customCaption;
            WindowChrome.SetWindowChrome(window, updated);
        }
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (customCaption)
        {
            // Without WS_SYSMENU, DWM doesn't draw its caption buttons in the extended frame. Alt+F4, the taskbar
            // menu and minimize-from-taskbar keep working; the window re-adds Alt+Space itself (ShowSystemMenu).
            var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style & ~WS_SYSMENU));
        }

        if (SetAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW))
        {
            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;
            if (!customCaption) window.SetValue(UsesSystemCaptionButtonsProperty, true);
        }
    }

    /// <summary>Opens the window's system menu (Restore, Move, Size, Minimize, Maximize, Close) under the title bar icon.</summary>
    public static void ShowSystemMenu(Window window)
    {
        var origin = window.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(window);
        SystemCommands.ShowSystemMenu(window, new Point(origin.X / dpi.DpiScaleX + 8, origin.Y / dpi.DpiScaleY + 40));
    }

    private static void UpdateDarkMode(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero) SetAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, App.IsDarkTheme ? 1 : 0);
    }

    private static bool SetAttribute(IntPtr hwnd, int attribute, int value)
    {
        try { return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0; }
        catch { return false; } // dwmapi attribute unknown on older Windows
    }
}
