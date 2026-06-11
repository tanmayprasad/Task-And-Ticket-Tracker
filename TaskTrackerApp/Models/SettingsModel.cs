namespace TaskTrackerApp.Models;

public class SettingsModel
{
    public bool NotificationsEnabled { get; set; } = true;
    public double AlertTimerHours { get; set; } = 4.0;
    public string Theme { get; set; } = "Dark"; // "Light", "Dark", "System"
    public int WidgetTextSize { get; set; } = 24;
    public bool WidgetTextBold { get; set; } = true;
    public int MaxActiveTasks { get; set; } = 2;
    public double? WidgetLeft { get; set; } = null;
    public double? WidgetTop { get; set; } = null;
    public double WidgetOpacity { get; set; } = 0.95;
    public string WidgetTheme { get; set; } = "Dark";
    public bool IsDetailsMaximized { get; set; } = false;
}
