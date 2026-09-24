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
            // Keep the unreadable file for inspection instead of silently overwriting it on the next save
            SafeFile.Quarantine(_settingsFilePath);
            return new SettingsModel();
        }
    }

    public void SaveSettings(SettingsModel settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            SafeFile.WriteAllTextAtomic(_settingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are non-critical (e.g. widget position while dragging); never crash the app over them
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
