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

    /// <summary>Position in the list as currently shown (1…n after filters). Display only, never saved.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int ListPosition { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsActive { get; set; }
    
    public System.Collections.ObjectModel.ObservableCollection<TicketStep> Steps { get; set; } = new();
}

public class TicketStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}
