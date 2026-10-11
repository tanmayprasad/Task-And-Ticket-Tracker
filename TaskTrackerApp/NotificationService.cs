using System;
using System.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TaskTrackerApp;

public class NotificationService
{
    private readonly ContextAwareEngine _contextEngine;
    private readonly Action<string, string> _showToastAction;
    private readonly System.Windows.Threading.DispatcherTimer _pollingTimer;
    // Every profile's tasks with its own reminder settings: reminders fire for all profiles, not just the open one
    private readonly Dictionary<Guid, (TaskTrackerApp.Models.ProfileModel Profile, List<TaskTrackerApp.Models.TaskModel> Tasks)> _profiles = new();
    private HashSet<Guid> _notifiedTasks = new();

    public NotificationService(ContextAwareEngine contextEngine, Action<string, string> showToastAction)
    {
        _contextEngine = contextEngine;
        _showToastAction = showToastAction;
        
        // Poll every 1 minute. A DispatcherTimer runs on the UI thread, so the task list is never
        // read here while MainWindow is modifying it.
        _pollingTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _pollingTimer.Tick += PollingTimer_Tick;
        _pollingTimer.Start();
    }

    /// <summary>Sets a profile's tasks and reminder settings (the profile object is read on every check).</summary>
    public void UpdateProfile(TaskTrackerApp.Models.ProfileModel profile, List<TaskTrackerApp.Models.TaskModel> tasks)
    {
        _profiles[profile.Id] = (profile, tasks);
    }

    /// <summary>Drops profiles that no longer exist.</summary>
    public void KeepProfiles(IEnumerable<Guid> profileIds)
    {
        var keep = profileIds.ToHashSet();
        foreach (var id in _profiles.Keys.Where(id => !keep.Contains(id)).ToList()) _profiles.Remove(id);
    }

    private void PollingTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        bool nameProfiles = _profiles.Count > 1;

        foreach (var (profile, tasks) in _profiles.Values)
        {
            if (!profile.NotificationsEnabled) continue;
            double hours = profile.AlertTimerHours;

            foreach (var task in tasks)
            {
                if (task.TargetDate.HasValue && !_notifiedTasks.Contains(task.Id) && task.State != "Done")
                {
                    var timeUntilDue = task.TargetDate.Value - now;

                    // Alert window: between N-0.1 and N hours away
                    if (timeUntilDue.TotalHours <= hours && timeUntilDue.TotalHours > (hours - 0.1))
                    {
                        string where = nameProfiles ? $" · {profile.Name}" : string.Empty;
                        ShowTaskReminder($"Upcoming Deadline ({hours} Hours)", $"Task: {task.Title}{where}");
                        _notifiedTasks.Add(task.Id);
                    }
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
