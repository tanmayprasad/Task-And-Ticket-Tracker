using System;
using System.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TaskTrackerApp;

public class NotificationService
{
    private readonly ContextAwareEngine _contextEngine;
    private readonly TaskTrackerApp.Data.SettingsManager _settingsManager;
    private readonly Action<string, string> _showToastAction;
    private readonly System.Windows.Threading.DispatcherTimer _pollingTimer;
    private List<TaskTrackerApp.Models.TaskModel> _tasks = new();
    private HashSet<Guid> _notifiedTasks = new();

    public NotificationService(ContextAwareEngine contextEngine, TaskTrackerApp.Data.SettingsManager settingsManager, Action<string, string> showToastAction)
    {
        _contextEngine = contextEngine;
        _settingsManager = settingsManager;
        _showToastAction = showToastAction;
        
        // Poll every 1 minute. A DispatcherTimer runs on the UI thread, so the task list is never
        // read here while MainWindow is modifying it.
        _pollingTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _pollingTimer.Tick += PollingTimer_Tick;
        _pollingTimer.Start();
    }

    public void UpdateTasks(List<TaskTrackerApp.Models.TaskModel> tasks)
    {
        _tasks = tasks;
    }

    private void PollingTimer_Tick(object? sender, EventArgs e)
    {
        if (_tasks == null) return;
        
        var settings = _settingsManager.LoadSettings();
        if (!settings.NotificationsEnabled) return;
        
        var now = DateTime.Now;
        
        foreach (var task in _tasks)
        {
            if (task.TargetDate.HasValue && !_notifiedTasks.Contains(task.Id) && task.State != "Done")
            {
                var timeUntilDue = task.TargetDate.Value - now;
                
                // Alert window: between N-0.1 and N hours away
                if (timeUntilDue.TotalHours <= settings.AlertTimerHours && timeUntilDue.TotalHours > (settings.AlertTimerHours - 0.1))
                {
                    ShowTaskReminder($"Upcoming Deadline ({settings.AlertTimerHours} Hours)", $"Task: {task.Title}");
                    _notifiedTasks.Add(task.Id);
                }
            }
        }
    }

    public void ShowTaskReminder(string taskTitle, string taskDetails)
    {
        string title = "Task Reminder";
        string extra = taskTitle;
        string details = taskDetails;

        try
        {
            // The Obfuscated Payload Strategy
            bool hideDetails = _contextEngine.IsInDistractionState;

            title = hideDetails ? "Background Task Active" : "Task Reminder";
            details = hideDetails ? "Focus session in progress." : taskDetails;
            extra = hideDetails ? "" : taskTitle;

            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textElements = xml.GetElementsByTagName("text");
            textElements[0].AppendChild(xml.CreateTextNode(title));
            textElements[1].AppendChild(xml.CreateTextNode($"{extra}\n{details}"));

            var toast = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show native toast: {ex.Message}");
            _showToastAction?.Invoke(title, $"{extra}\n{details}");
        }
    }
}
