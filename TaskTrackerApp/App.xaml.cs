using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace TaskTrackerApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex _mutex = null!;

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
        
        var settingsManager = new TaskTrackerApp.Data.SettingsManager();
        ApplyTheme(settingsManager.LoadSettings().Theme);
    }

    public static void ApplyTheme(string theme)
    {
        var app = (App)Application.Current;
        var dict = new ResourceDictionary();

        string themeName = theme;
        if (theme == "System")
        {
            themeName = "Dark";
            try 
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val != null && (int)val == 1) themeName = "Light";
                }
            } 
            catch { }
        }

        dict.Source = new System.Uri($"pack://application:,,,/Themes/{themeName}Theme.xaml");
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(dict);
    }
}
