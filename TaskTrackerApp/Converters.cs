using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

public enum DueUrgency
{
    None,
    Later,
    Today,
    Overdue
}

/// <summary>Pure formatting for the task list's secondary line (due date and step progress). Unit-tested.</summary>
public static class TaskFormat
{
    // The date picker defaults the time to 00:00; treat that as "date only" so a task due today
    // isn't shown as overdue from midnight onwards.
    private static bool IsDateOnly(DateTime due) => due.TimeOfDay == TimeSpan.Zero;

    public static DueUrgency Urgency(TaskModel task, DateTime now)
    {
        if (task.State == "Done" || task.TargetDate is not DateTime due) return DueUrgency.None;

        bool overdue = IsDateOnly(due) ? now.Date > due.Date : now > due;
        if (overdue) return DueUrgency.Overdue;
        return due.Date == now.Date ? DueUrgency.Today : DueUrgency.Later;
    }

    public static string DueText(TaskModel task, DateTime now, CultureInfo culture)
    {
        if (task.TargetDate is not DateTime due) return string.Empty;

        string time = IsDateOnly(due) ? string.Empty : " " + due.ToString("HH:mm", culture);

        if (Urgency(task, now) == DueUrgency.Overdue)
        {
            var late = IsDateOnly(due) ? now.Date - due.Date : now - due;
            if (late.TotalDays >= 1) return $"Overdue {(int)late.TotalDays}d";
            if (late.TotalHours >= 1) return $"Overdue {(int)late.TotalHours}h";
            return "Overdue";
        }

        if (due.Date == now.Date) return "Today" + time;
        if (due.Date == now.Date.AddDays(1)) return "Tomorrow" + time;
        if (due.Date > now.Date && due.Date < now.Date.AddDays(7)) return due.ToString("ddd", culture) + time;

        string format = due.Year == now.Year ? "d MMM" : "d MMM yyyy";
        return due.ToString(format, culture) + time;
    }

    /// <summary>"2/5" when the task has steps, otherwise empty.</summary>
    public static string StepProgress(IList<TicketStep>? steps) =>
        steps == null || steps.Count == 0 ? string.Empty : $"{steps.Count(s => s.IsDone)}/{steps.Count}";
}

/// <summary>TaskModel → relative due text ("Today 17:00", "Overdue 2d", "12 Oct").</summary>
public class DueTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TaskModel task ? TaskFormat.DueText(task, DateTime.Now, CultureInfo.CurrentCulture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>TaskModel → <see cref="DueUrgency"/> name, for colouring the due text in DataTriggers.</summary>
public class DueUrgencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TaskModel task ? TaskFormat.Urgency(task, DateTime.Now).ToString() : nameof(DueUrgency.None);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>TaskModel → "2/5" step progress, or empty when the task has no steps.</summary>
public class StepProgressConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TaskModel task ? TaskFormat.StepProgress(task.Steps) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Text length → Visible only when it reaches 80% of the limit given as ConverterParameter,
/// so character counters don't clutter the form until they matter.
/// </summary>
public class NearLimitVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int length && int.TryParse(parameter?.ToString(), out int limit) && limit > 0)
        {
            return length >= limit * 0.8 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound double is below the numeric ConverterParameter (used for narrow-column layouts).</summary>
public class LessThanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double actual && double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double limit))
        {
            return actual < limit;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
