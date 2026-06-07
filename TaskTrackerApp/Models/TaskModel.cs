using System;

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
    public DateTime? TargetDate { get; set; }
    public bool IsActive { get; set; }
}
