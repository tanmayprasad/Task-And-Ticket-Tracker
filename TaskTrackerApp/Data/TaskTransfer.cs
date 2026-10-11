using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Data;

/// <summary>The tasks read from an import file, plus anything that had to be fixed or dropped.</summary>
public sealed record ImportFile(List<TaskModel> Tasks, List<string> Warnings);

/// <summary>
/// Result of adding imported tasks to a list: new tasks, existing tasks updated from the file, matches that were
/// already identical, and file rows skipped because the file repeats a ticket number.
/// </summary>
public sealed record MergeResult(List<TaskModel> Tasks, int Added, int Updated, int Unchanged, int Skipped);

/// <summary>
/// Export and import of a profile's tasks. JSON keeps everything; CSV is for spreadsheets (steps go in one cell,
/// one per line, as "[x] text" / "[ ] text"). Pure logic, unit tested.
/// </summary>
public static class TaskTransfer
{
    public const string FormatName = "TaskAndTicketTracker";
    public const int TitleLimit = 100;
    public const int TextLimit = 2000;
    public const int StepLimit = 100;

    private static readonly string[] States = { "To Do", "In progress", "Done" };
    private static readonly string[] CsvColumns = { "Ticket", "Title", "State", "Priority", "Active", "Due", "Description", "Acceptance criteria", "Steps" };

    private sealed class Envelope
    {
        [JsonPropertyName("format")] public string Format { get; set; } = FormatName;
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("profile")] public string? Profile { get; set; }
        [JsonPropertyName("exportedAt")] public DateTime ExportedAt { get; set; }
        [JsonPropertyName("tasks")] public List<TaskModel> Tasks { get; set; } = new();
    }

    // ------------------------------------------------------------------ Export

    public static string ExportJson(IEnumerable<TaskModel> tasks, string profileName, DateTime exportedAt) =>
        JsonSerializer.Serialize(new Envelope { Profile = profileName, ExportedAt = exportedAt, Tasks = tasks.ToList() },
                                 new JsonSerializerOptions { WriteIndented = true });

    /// <summary>CSV text (RFC 4180). Write it with a UTF-8 BOM so Excel reads non-English text correctly.</summary>
    public static string ExportCsv(IEnumerable<TaskModel> tasks)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", CsvColumns)).Append("\r\n");
        foreach (var t in tasks)
        {
            var fields = new[]
            {
                t.VstsNumber,
                t.Title,
                t.State,
                t.Priority.ToString(CultureInfo.InvariantCulture),
                t.IsActive ? "Yes" : "No",
                FormatDue(t.TargetDate),
                t.Description,
                t.AcceptanceCriteria,
                string.Join("\n", t.Steps.Select(s => (s.IsDone ? "[x] " : "[ ] ") + s.Description)),
            };
            sb.Append(string.Join(",", fields.Select(CsvQuote))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Due-all-day tasks are stored at 00:00 and exported as a date only.</summary>
    private static string FormatDue(DateTime? due) =>
        due is not DateTime d ? string.Empty
        : d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string CsvQuote(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    // ------------------------------------------------------------------ Import

    /// <summary>Reads a .json (export envelope, or a bare task list such as tasks.json) or .csv file's text.</summary>
    /// <exception cref="FormatException">The file can't be read as tasks; the message is user-facing.</exception>
    public static ImportFile Import(string content, string extension)
    {
        content = content.TrimStart('﻿');
        var warnings = new List<string>();
        var raw = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ReadCsv(content, warnings) : ReadJson(content);

        var tasks = new List<TaskModel>();
        foreach (var t in raw)
        {
            t.Title = Limit(t.Title?.Trim(), TitleLimit);
            if (t.Title.Length == 0) { warnings.Add("Skipped a task without a title."); continue; }
            t.VstsNumber = Limit(t.VstsNumber?.Trim(), 50);
            t.Description = Limit(t.Description, TextLimit);
            t.AcceptanceCriteria = Limit(t.AcceptanceCriteria, TextLimit);
            t.State = NormalizeState(t.State);
            t.Steps ??= new();
            foreach (var s in t.Steps) s.Description = Limit(s.Description, StepLimit);
            if (t.Id == Guid.Empty) t.Id = Guid.NewGuid();
            tasks.Add(t);
        }
        return new ImportFile(tasks, warnings);
    }

    private static List<TaskModel> ReadJson(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<List<TaskModel>>(content) ?? new();
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("tasks", out _))
                return JsonSerializer.Deserialize<Envelope>(content)?.Tasks ?? new();
        }
        catch (JsonException ex)
        {
            throw new FormatException($"This isn't a valid task file ({ex.Message}).", ex);
        }
        throw new FormatException("This JSON file doesn't contain a task list.");
    }

    private static List<TaskModel> ReadCsv(string content, List<string> warnings)
    {
        var rows = ParseCsv(content);
        if (rows.Count == 0) throw new FormatException("The CSV file is empty.");

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        int title = Col("Title");
        if (title < 0) throw new FormatException("The CSV file needs a \"Title\" column.");
        int ticket = Col("Ticket"), state = Col("State"), active = Col("Active"), due = Col("Due"),
            description = Col("Description"), ac = Col("Acceptance criteria"), steps = Col("Steps");

        var tasks = new List<TaskModel>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            string Get(int c) => c >= 0 && c < row.Count ? row[c] : string.Empty;

            var task = new TaskModel
            {
                Id = Guid.NewGuid(),
                VstsNumber = Get(ticket),
                Title = Get(title),
                State = Get(state),
                IsActive = Get(active).Trim().ToLowerInvariant() is "yes" or "true" or "1" or "y",
                Description = Get(description),
                AcceptanceCriteria = Get(ac),
            };

            var dueText = Get(due).Trim();
            if (dueText.Length > 0)
            {
                if (DateTime.TryParse(dueText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
                    || DateTime.TryParse(dueText, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out d))
                    task.TargetDate = d;
                else
                    warnings.Add($"Row {i + 1}: couldn't read the due date \"{dueText}\", so it was left empty.");
            }

            foreach (var line in Get(steps).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0))
            {
                var text = line.TrimStart();
                bool done = text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase);
                if (done || text.StartsWith("[ ]")) text = text[3..];
                task.Steps.Add(new TicketStep { Description = text.Trim(), IsDone = done });
            }
            tasks.Add(task);
        }
        return tasks;
    }

    /// <summary>RFC 4180 parser: quoted fields may contain commas, quotes ("") and line breaks.</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static string NormalizeState(string? state)
    {
        if (string.Equals(state?.Trim(), "Doing", StringComparison.OrdinalIgnoreCase)) return "In progress";
        return States.FirstOrDefault(s => string.Equals(s, state?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "To Do";
    }

    private static string Limit(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];

    // ------------------------------------------------------------------ Merge

    /// <summary>
    /// Adds imported tasks to a list. An imported task that is already there (same Id, else the same ticket number,
    /// else, for tasks without a ticket, the same title) <b>updates</b> the existing task in place: title, notes,
    /// due date, state and steps come from the file; its Id, ticket number and position stay. Everything else is
    /// appended in file order. When the file repeats a ticket number, the first row wins.
    /// <paramref name="usedIds"/> (ids in other profiles) get a new Id, so a task never exists in two profiles.
    /// </summary>
    public static MergeResult Merge(IReadOnlyList<TaskModel> existing, IEnumerable<TaskModel> incoming, ISet<Guid>? usedIds = null)
    {
        var result = existing.ToList();
        var byId = new Dictionary<Guid, TaskModel>();
        var byTicket = new Dictionary<string, TaskModel>();
        var byTitle = new Dictionary<string, TaskModel>();
        void Index(TaskModel t)
        {
            byId.TryAdd(t.Id, t);
            if (Norm(t.VstsNumber).Length > 0) byTicket.TryAdd(Norm(t.VstsNumber), t);
            else byTitle.TryAdd(Norm(t.Title), t);
        }
        foreach (var t in result) Index(t);

        var ticketsInFile = new HashSet<string>();
        int added = 0, updated = 0, unchanged = 0, skipped = 0;
        foreach (var task in incoming)
        {
            var ticket = Norm(task.VstsNumber);
            if (ticket.Length > 0 && !ticketsInFile.Add(ticket)) { skipped++; continue; }

            var match = byId.GetValueOrDefault(task.Id)
                        ?? (ticket.Length > 0 ? byTicket.GetValueOrDefault(ticket) : byTitle.GetValueOrDefault(Norm(task.Title)));
            if (match == null)
            {
                if (usedIds?.Contains(task.Id) == true || byId.ContainsKey(task.Id)) task.Id = Guid.NewGuid();
                result.Add(task);
                Index(task);
                added++;
            }
            else if (SameContent(match, task)) unchanged++;
            else
            {
                UpdateFrom(match, task);
                updated++;
            }
        }
        return new MergeResult(result, added, updated, unchanged, skipped);
    }

    private static string Norm(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool SameContent(TaskModel a, TaskModel b) =>
        a.Title == b.Title && a.Description == b.Description && a.AcceptanceCriteria == b.AcceptanceCriteria
        && a.TargetDate == b.TargetDate && a.State == b.State
        && a.Steps.Select(s => (s.Description, s.IsDone)).SequenceEqual(b.Steps.Select(s => (s.Description, s.IsDone)));

    private static void UpdateFrom(TaskModel target, TaskModel source)
    {
        target.Title = source.Title;
        target.Description = source.Description;
        target.AcceptanceCriteria = source.AcceptanceCriteria;
        target.TargetDate = source.TargetDate;
        target.State = source.State;
        target.Steps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>(
            source.Steps.Select(s => new TicketStep { Id = s.Id, Description = s.Description, IsDone = s.IsDone }));
        if (target.State != "In progress") target.IsActive = false; // only In progress tasks can be active
    }

    /// <summary>Keeps at most <paramref name="max"/> active tasks: the first ones in list order stay active.</summary>
    public static void CapActive(List<TaskModel> tasks, int max)
    {
        foreach (var task in tasks.Where(t => t.IsActive).Skip(Math.Max(0, max)))
        {
            task.IsActive = false;
            if (task.State == "In progress") task.State = "To Do";
        }
    }

    /// <summary>A file name such as "Tasks - Work - 2026-10-07.json".</summary>
    public static string SuggestedFileName(string profileName, DateTime date, string extension)
    {
        var name = $"Tasks - {profileName} - {date:yyyy-MM-dd}";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name + extension;
    }
}
