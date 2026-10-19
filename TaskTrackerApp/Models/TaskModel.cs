using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskTrackerApp.Models;

public class TaskModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VstsNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public string State { get; set; } = "To Do"; // "To Do", "Doing", "Done"
    public int Priority { get; set; } = 1;

    /// <summary>Position in the list as currently shown (1…n after filters). Display only, never saved.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int ListPosition { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsActive { get; set; }

    /// <summary>When the task was marked Done. Null for open tasks and for tasks finished before this was recorded.</summary>
    public DateTime? CompletedAt { get; set; }

    public System.Collections.ObjectModel.ObservableCollection<TicketStep> Steps { get; set; } = new();

    /// <summary>
    /// Copies the steps edited in the form, stamping a completion time only for real transitions:
    /// newly ticked steps get <paramref name="now"/>, unticked steps lose it, and steps that were already done
    /// before completion times were recorded stay unstamped.
    /// </summary>
    public static System.Collections.ObjectModel.ObservableCollection<TicketStep> ApplyEditedSteps(
        IEnumerable<TicketStep> edited, IEnumerable<TicketStep>? previous, DateTime now)
    {
        var before = new Dictionary<Guid, TicketStep>();
        foreach (var p in previous ?? Array.Empty<TicketStep>()) before.TryAdd(p.Id, p); // tolerate duplicate ids in hand-edited files
        return new(edited.Select(s =>
        {
            var copy = new TicketStep { Id = s.Id, IsDone = s.IsDone, Description = s.Description, CompletedAt = s.CompletedAt };
            bool wasDone = before.TryGetValue(s.Id, out var prev) && prev.IsDone;
            if (wasDone != copy.IsDone || !copy.IsDone) copy.SyncCompletion(now);
            return copy;
        }));
    }

    /// <summary>
    /// Drops completion times that no longer match the data: a task that is not Done, or a step that is not ticked,
    /// has none. Catches every path that reopens something without going through <see cref="SyncCompletion"/>.
    /// </summary>
    public void ClearStaleCompletion()
    {
        if (State != "Done") CompletedAt = null;
        foreach (var step in Steps ?? new())
            if (!step.IsDone) step.CompletedAt = null;
    }

    /// <summary>Keeps <see cref="CompletedAt"/> in step with <see cref="State"/>: stamps it on the move to Done, clears it on reopen.</summary>
    public void SyncCompletion(DateTime now)
    {
        if (State == "Done") CompletedAt ??= now;
        else CompletedAt = null;
    }
}

public class TicketStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public bool IsDone { get; set; }

    /// <summary>When the step was ticked. Null while open and for steps finished before this was recorded.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Keeps <see cref="CompletedAt"/> in step with <see cref="IsDone"/>.</summary>
    public void SyncCompletion(DateTime now)
    {
        if (IsDone) CompletedAt ??= now;
        else CompletedAt = null;
    }
}
