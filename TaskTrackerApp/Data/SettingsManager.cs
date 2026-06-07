using System;
using System.IO;
using System.Text.Json;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Data;

public class SettingsManager
{
    private readonly string _settingsFilePath;

    public SettingsManager()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataFolder, "TaskTrackerApp");
        
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public SettingsModel LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsModel();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }
        catch
        {
            return new SettingsModel();
        }
    }

    public void SaveSettings(SettingsModel settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }
}
