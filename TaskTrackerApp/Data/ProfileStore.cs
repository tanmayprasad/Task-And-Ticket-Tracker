using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Data;

/// <summary>
/// Profiles and the active profile (profiles.json). The first profile, "Default", uses the app data folder's own
/// tasks.json, so existing data is never moved and older versions still find it. Later profiles live in
/// profiles\&lt;id&gt;\. Deleted profiles are moved to profiles\.deleted\, never erased.
/// </summary>
public class ProfileStore
{
    public const int MaxNameLength = 40;

    private readonly string _root;
    private readonly string _filePath;
    private ProfilesFile _data;

    /// <param name="root">The app data folder (defaults to %LOCALAPPDATA%\TaskTrackerApp).</param>
    /// <param name="seed">Focus settings for the first profile (from settings.json) when profiles.json doesn't exist yet.</param>
    public ProfileStore(string? root = null, SettingsModel? seed = null)
    {
        _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTrackerApp");
        Directory.CreateDirectory(_root);
        _filePath = Path.Combine(_root, "profiles.json");
        _data = Load(seed ?? new SettingsModel());
    }

    public IReadOnlyList<ProfileModel> Profiles => _data.Profiles;

    public ProfileModel Active => _data.Profiles.FirstOrDefault(p => p.Id == _data.ActiveProfileId) ?? _data.Profiles[0];

    public ProfileModel? Find(Guid id) => _data.Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>The folder holding the profile's tasks.json.</summary>
    public string FolderOf(ProfileModel profile) =>
        string.IsNullOrEmpty(profile.Folder) ? _root : Path.Combine(_root, profile.Folder);

    public TaskRepository RepositoryFor(ProfileModel profile) => new(FolderOf(profile));

    public void SetActive(Guid id)
    {
        if (Find(id) == null || _data.ActiveProfileId == id) return;
        _data.ActiveProfileId = id;
        Save();
    }

    /// <summary>Creates a profile; its focus settings are copied from the most recently created profile.</summary>
    public ProfileModel Create(string name)
    {
        name = ValidateName(name, except: null);
        var template = _data.Profiles.OrderByDescending(p => p.CreatedAt).First();
        var id = Guid.NewGuid();
        var profile = new ProfileModel
        {
            Id = id,
            Name = name,
            CreatedAt = DateTime.Now,
            Folder = Path.Combine("profiles", id.ToString("N")),
            MaxActiveTasks = template.MaxActiveTasks,
            NotificationsEnabled = template.NotificationsEnabled,
            AlertTimerHours = template.AlertTimerHours,
        };
        Directory.CreateDirectory(FolderOf(profile));
        _data.Profiles.Add(profile);
        Save();
        return profile;
    }

    public void Rename(Guid id, string name)
    {
        var profile = Find(id) ?? throw new ArgumentException("Unknown profile.");
        profile.Name = ValidateName(name, except: id);
        Save();
    }

    /// <summary>Saves a change to a profile's focus settings.</summary>
    public void Update(Guid id, Action<ProfileModel> change)
    {
        var profile = Find(id);
        if (profile == null) return;
        change(profile);
        Save();
    }

    /// <summary>
    /// Removes a profile and moves its task files to profiles\.deleted\&lt;name&gt;-&lt;timestamp&gt;\.
    /// The last profile can't be deleted; deleting the active one makes the first remaining profile active.
    /// Returns the folder the files were moved to.
    /// </summary>
    public string Delete(Guid id)
    {
        var profile = Find(id) ?? throw new ArgumentException("Unknown profile.");
        if (_data.Profiles.Count == 1) throw new InvalidOperationException("You can't delete the only profile.");

        var target = Path.Combine(_root, "profiles", ".deleted", $"{SafeFileName(profile.Name)}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(target);
        var folder = FolderOf(profile);
        // The Default profile shares the app data folder with settings.json, so only its task files move
        foreach (var file in new[] { "tasks.json", "tasks.json.bak" })
        {
            var source = Path.Combine(folder, file);
            if (File.Exists(source)) File.Move(source, Path.Combine(target, file));
        }
        if (!string.IsNullOrEmpty(profile.Folder) && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);

        _data.Profiles.Remove(profile);
        if (_data.ActiveProfileId == id) _data.ActiveProfileId = _data.Profiles[0].Id;
        Save();
        return target;
    }

    /// <summary>Trims and checks a profile name; throws <see cref="ArgumentException"/> with a user-facing message.</summary>
    public string ValidateName(string? name, Guid? except)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0) throw new ArgumentException("Enter a name for the profile.");
        if (name.Length > MaxNameLength) throw new ArgumentException($"Use at most {MaxNameLength} characters.");
        if (_data.Profiles.Any(p => p.Id != except && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A profile called \"{name}\" already exists.");
        return name;
    }

    private ProfilesFile Load(SettingsModel seed)
    {
        foreach (var path in new[] { _filePath, SafeFile.BackupPathFor(_filePath) })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var data = JsonSerializer.Deserialize<ProfilesFile>(File.ReadAllText(path));
                if (data is { Profiles.Count: > 0 })
                {
                    if (data.Profiles.All(p => p.Id != data.ActiveProfileId)) data.ActiveProfileId = data.Profiles[0].Id;
                    return data;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                if (path == _filePath) SafeFile.Quarantine(_filePath); // keep it for inspection; fall back to the backup
            }
        }

        // First run of a version with profiles (or both files unreadable): the existing tasks become "Default"
        var first = new ProfileModel
        {
            Name = "Default",
            CreatedAt = DateTime.Now,
            Folder = string.Empty,
            MaxActiveTasks = seed.MaxActiveTasks,
            NotificationsEnabled = seed.NotificationsEnabled,
            AlertTimerHours = seed.AlertTimerHours,
        };
        _data = new ProfilesFile { ActiveProfileId = first.Id, Profiles = { first } };
        Save();
        return _data;
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            SafeFile.WriteAllTextAtomic(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save profiles: {ex.Message}");
        }
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
