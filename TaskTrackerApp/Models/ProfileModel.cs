using System;
using System.Collections.Generic;

namespace TaskTrackerApp.Models;

/// <summary>
/// A profile: its own task list plus its own focus settings (active-task limit and reminders).
/// Appearance and widget settings stay shared in <see cref="SettingsModel"/>.
/// </summary>
public class ProfileModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Folder of this profile's tasks.json, relative to the app data folder. "" = the app data folder itself.</summary>
    public string Folder { get; set; } = string.Empty;

    public int MaxActiveTasks { get; set; } = 2;
    public bool NotificationsEnabled { get; set; } = true;
    public double AlertTimerHours { get; set; } = 4.0;
}

/// <summary>Contents of profiles.json.</summary>
public class ProfilesFile
{
    public Guid ActiveProfileId { get; set; }
    public List<ProfileModel> Profiles { get; set; } = new();
}
