using System;
using System.Threading;
using System.Windows;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex _mutex = null!;
    private static string _themeSetting = "Dark";

    /// <summary>True when the dark colour tokens are active.</summary>
    public static bool IsDarkTheme { get; private set; } = true;

    /// <summary>Raised after the colours change (theme switch, Windows accent or light/dark change).</summary>
    public static event EventHandler? ThemeChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appName = "TaskTrackerAppMutex_OneApp";
        bool createdNew;

        _mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // App is already running, exit.
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        AccentService.Initialize();
        var settingsManager = new TaskTrackerApp.Data.SettingsManager();
        ApplyTheme(settingsManager.LoadSettings().Theme);
    }

    /// <summary>
    /// Switches the colour tokens ("Dark", "Light" or "System") and re-applies the Windows accent.
    /// Only the colour dictionary is replaced; the shared control styles (Controls.xaml) stay loaded.
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        var app = (App)Application.Current;
        _themeSetting = theme;
        bool dark = theme switch
        {
            "Light" => false,
            "Dark" => true,
            _ => !SystemUsesLightTheme(),
        };

        var colors = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}Theme.xaml")
        };

        var dictionaries = app.Resources.MergedDictionaries;
        int index = -1;
        for (int i = 0; i < dictionaries.Count; i++)
        {
            if (dictionaries[i].Source?.OriginalString.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true)
            {
                index = i;
                break;
            }
        }
        if (index >= 0) dictionaries[index] = colors;
        else dictionaries.Insert(0, colors);

        IsDarkTheme = dark;
        AccentService.Apply(app.Resources, dark);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Re-applies the current theme setting (after a Windows accent or light/dark change).</summary>
    public static void ReapplyTheme() => ApplyTheme(_themeSetting);

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }
}
