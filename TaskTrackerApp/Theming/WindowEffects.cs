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
    public static void Attach(Window window)
    {
        EventHandler onThemeChanged = (s, e) => UpdateDarkMode(window);
        window.SourceInitialized += (s, e) => Apply(window);
        App.ThemeChanged += onThemeChanged;
        window.Closed += (s, e) => App.ThemeChanged -= onThemeChanged;
    }

    private static void Apply(Window window)
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
            updated.UseAeroCaptionButtons = true;
            WindowChrome.SetWindowChrome(window, updated);
        }
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (SetAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW))
        {
            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;
            window.SetValue(UsesSystemCaptionButtonsProperty, true);
        }
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
