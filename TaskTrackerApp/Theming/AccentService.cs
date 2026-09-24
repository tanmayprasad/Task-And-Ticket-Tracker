using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace TaskTrackerApp.Theming;

/// <summary>
/// Applies the Windows accent colour to the app and keeps the app in sync when the user changes
/// the accent colour or light/dark mode in Windows Settings.
/// </summary>
public static class AccentService
{
    private static UISettings? _uiSettings;
    private static DispatcherTimer? _refreshTimer;

    /// <summary>Starts listening for Windows colour/theme changes. Call once on the UI thread.</summary>
    public static void Initialize()
    {
        try { _uiSettings = new UISettings(); }
        catch { _uiSettings = null; } // very old Windows: keep the fallback accent

        // Accent and light/dark changes both raise UserPreferenceChanged; debounce them into one refresh
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _refreshTimer.Tick += (s, e) =>
        {
            _refreshTimer.Stop();
            App.ReapplyTheme();
        };
        SystemEvents.UserPreferenceChanged += (s, e) =>
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => { _refreshTimer.Stop(); _refreshTimer.Start(); }));
    }

    /// <summary>The Windows accent palette, or null when it can't be read.</summary>
    public static AccentPalette? ReadSystemAccent()
    {
        var ui = _uiSettings;
        if (ui == null) return null;
        try
        {
            Color Get(UIColorType type)
            {
                var c = ui.GetColorValue(type);
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return new AccentPalette(Get(UIColorType.Accent), Get(UIColorType.AccentLight1), Get(UIColorType.AccentLight2),
                Get(UIColorType.AccentLight3), Get(UIColorType.AccentDark1), Get(UIColorType.AccentDark2));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the accent brushes for the given theme into <paramref name="resources"/> (the application's top-level
    /// resources, which take precedence over the theme dictionary). Without a system accent, the theme's indigo stays.
    /// </summary>
    public static void Apply(ResourceDictionary resources, bool dark)
    {
        string[] keys = { "PrimaryBackground", "PrimaryColor", "PrimaryHover", "AccentText", "TextOnAccent", "ActiveRowBackground" };
        var palette = ReadSystemAccent();
        if (palette is not AccentPalette p)
        {
            foreach (var key in keys) resources.Remove(key);
            return;
        }

        var colors = AccentMath.ForTheme(p, dark);
        resources["PrimaryBackground"] = Frozen(colors.Fill);
        resources["PrimaryColor"] = Frozen(colors.Fill);
        resources["PrimaryHover"] = Frozen(colors.Hover);
        resources["AccentText"] = Frozen(colors.Text);
        resources["TextOnAccent"] = Frozen(colors.OnAccent);
        resources["ActiveRowBackground"] = Frozen(colors.RowTint);
    }

    /// <summary>The current accent fill as a colour (used by windows that set colours in code, e.g. the widget).</summary>
    public static Color CurrentAccent(bool dark)
    {
        if (ReadSystemAccent() is AccentPalette p) return AccentMath.ForTheme(p, dark).Fill;
        return dark ? Color.FromRgb(0xA5, 0xA7, 0xFA) : Color.FromRgb(0x4F, 0x46, 0xE5);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
