using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Data;

public class TaskRepository
{
    private readonly string _filePath;
    
    public TaskRepository()
    {
        // Store tasks in AppData to avoid permissions issues
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "TaskTrackerApp");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "tasks.json");
    }

    public List<TaskModel> LoadTasks()
    {
        if (!File.Exists(_filePath))
        {
            return new List<TaskModel>();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<TaskModel>>(json) ?? new List<TaskModel>();
        }
        catch
        {
            return new List<TaskModel>();
        }
    }

    public void SaveTasks(List<TaskModel> tasks)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(tasks, options);
        File.WriteAllText(_filePath, json);
    }
}
