using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Data;

public class TaskRepository
{
    private readonly string _filePath;

    /// <summary>
    /// Set by <see cref="LoadTasks"/> when tasks.json could not be read. Describes what happened
    /// (file moved aside, backup restored) so the UI can inform the user. Null when the load was clean.
    /// </summary>
    public string? LoadWarning { get; private set; }

    public TaskRepository(string? dataFolder = null)
    {
        // Store tasks in AppData to avoid permissions issues
        var appFolder = dataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTrackerApp");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "tasks.json");
    }

    public List<TaskModel> LoadTasks()
    {
        LoadWarning = null;

        if (!File.Exists(_filePath))
        {
            return new List<TaskModel>();
        }

        if (TryRead(_filePath, out var tasks))
        {
            return tasks;
        }

        // The main file is unreadable. Move it aside so the next save can't overwrite it,
        // then fall back to the last good version.
        var quarantined = SafeFile.Quarantine(_filePath);
        var backupPath = SafeFile.BackupPathFor(_filePath);

        if (File.Exists(backupPath) && TryRead(backupPath, out var backupTasks))
        {
            File.Copy(backupPath, _filePath, overwrite: true);
            LoadWarning = $"Your task file could not be read, so the most recent backup was restored.\n\nThe unreadable file was kept at:\n{quarantined ?? _filePath}";
            return backupTasks;
        }

        LoadWarning = $"Your task file could not be read and no usable backup was found. Starting with an empty list.\n\nThe unreadable file was kept at:\n{quarantined ?? _filePath}";
        return new List<TaskModel>();
    }

    public void SaveTasks(List<TaskModel> tasks)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(tasks, options);
        SafeFile.WriteAllTextAtomic(_filePath, json);
    }

    private static bool TryRead(string path, out List<TaskModel> tasks)
    {
        try
        {
            var json = File.ReadAllText(path);
            tasks = JsonSerializer.Deserialize<List<TaskModel>>(json) ?? new List<TaskModel>();
            return true;
        }
        catch
        {
            tasks = new List<TaskModel>();
            return false;
        }
    }
}
