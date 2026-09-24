using System;
using System.IO;

namespace TaskTrackerApp.Data;

/// <summary>
/// Crash-safe file helpers: writes go to a temp file first and are swapped in atomically,
/// keeping the previous good version as "&lt;file&gt;.bak".
/// </summary>
public static class SafeFile
{
    public static string BackupPathFor(string path) => path + ".bak";

    public static void WriteAllTextAtomic(string path, string contents)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, BackupPathFor(path), ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    /// <summary>
    /// Moves an unreadable file aside (e.g. "tasks.corrupt-20260924-101500.json") so it is never
    /// overwritten by the next save. Returns the new path, or null if the move failed.
    /// </summary>
    public static string? Quarantine(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var target = Path.Combine(dir, $"{name}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}{ext}");
            File.Move(path, target);
            return target;
        }
        catch
        {
            return null;
        }
    }
}
