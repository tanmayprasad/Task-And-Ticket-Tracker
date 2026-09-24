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
    public string WidgetMode { get; set; } = WidgetModes.Floating;
    public string? DockEdge { get; set; } = null; // "Left", "Top", "Right", "Bottom"; null = not docked
    public bool IsDetailsMaximized { get; set; } = false;
    public bool CloseToTrayTipShown { get; set; } = false;
}

public static class WidgetModes
{
    public const string Floating = "Floating";
    public const string EdgeDocked = "EdgeDocked";
    public const string TaskbarStrip = "TaskbarStrip";
}
