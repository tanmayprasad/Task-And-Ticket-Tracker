using System.Linq;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Widgets;

/// <summary>Formatting shared by the compact widget surfaces (taskbar strip, dock tab).</summary>
public static class WidgetText
{
    /// <summary>The first step that is not done yet, or null when there are no open steps.</summary>
    public static TicketStep? CurrentStep(TaskModel task) =>
        task.Steps?.FirstOrDefault(s => !s.IsDone);

    /// <summary>"#123" or empty when the task has no ticket number.</summary>
    public static string TicketLabel(TaskModel task) =>
        string.IsNullOrWhiteSpace(task.VstsNumber) ? string.Empty : $"#{task.VstsNumber.Trim()}";

    /// <summary>
    /// One-line summary: "#123 · current step" when an open step exists, otherwise "#123 · task name".
    /// The ticket prefix is dropped when there is no ticket number.
    /// </summary>
    public static string CompactText(TaskModel task)
    {
        var body = CurrentStep(task)?.Description ?? task.Title;
        var ticket = TicketLabel(task);
        return ticket.Length == 0 ? body : $"{ticket} · {body}";
    }

    /// <summary>Full detail for tooltips: ticket, title and the current step on separate lines.</summary>
    public static string DetailText(TaskModel task)
    {
        var ticket = TicketLabel(task);
        var header = ticket.Length == 0 ? task.Title : $"{ticket}  {task.Title}";
        var step = CurrentStep(task);
        if (step == null) return header;

        int done = task.Steps.Count(s => s.IsDone);
        return $"{header}\nStep {done + 1}/{task.Steps.Count}: {step.Description}";
    }
}
