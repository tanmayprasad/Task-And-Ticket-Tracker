using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

/// <summary>One task on one day of the History page: whether it was finished that day, plus the steps ticked that day.</summary>
public sealed record DoneLogEntry(TaskModel Task, bool TaskDone, IReadOnlyList<TicketStep> Steps)
{
    /// <summary>Whole-task progress such as "2/3 Steps"; empty when the task has no steps.</summary>
    public string StepsText => TaskFormat.StepProgress(Task.Steps) is { Length: > 0 } progress ? progress + " Steps" : string.Empty;
}

/// <summary>Everything completed on one calendar day.</summary>
public sealed record DoneLogDay(DateTime Date, string Header, IReadOnlyList<DoneLogEntry> Entries)
{
    /// <summary>"2 tasks · 5 steps": what was finished that day; a zero part is left out.</summary>
    public string CountText
    {
        get
        {
            int tasks = Entries.Count(e => e.TaskDone);
            int steps = Entries.Sum(e => e.Steps.Count);
            var parts = new List<string>();
            if (tasks > 0) parts.Add(tasks == 1 ? "1 task" : $"{tasks} tasks");
            if (steps > 0) parts.Add(steps == 1 ? "1 step" : $"{steps} steps");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>Pure builder for the Done log page (today, yesterday and the days before). Unit-tested.</summary>
public static class DoneLog
{
    public const int DefaultDays = 5;

    public static string DayHeader(DateTime date, DateTime now, CultureInfo culture)
    {
        if (date.Date == now.Date) return "Today";
        if (date.Date == now.Date.AddDays(-1)) return "Yesterday";
        return date.ToString(date.Year == now.Year ? "dddd, d MMM" : "dddd, d MMM yyyy", culture);
    }

    public static IReadOnlyList<DoneLogDay> Build(IEnumerable<TaskModel> tasks, DateTime now, CultureInfo culture, int days = DefaultDays)
    {
        var first = now.Date.AddDays(-(days - 1));
        var perDay = new Dictionary<DateTime, List<DoneLogEntry>>();

        void Add(DateTime day, DoneLogEntry entry)
        {
            if (!perDay.TryGetValue(day, out var list)) perDay[day] = list = new List<DoneLogEntry>();
            list.Add(entry);
        }

        bool InWindow(DateTime d) => d.Date >= first; // later than today (clock change, import) is clamped to today
        DateTime DayOf(DateTime d) => d.Date > now.Date ? now.Date : d.Date;

        foreach (var task in tasks)
        {
            var stepsByDay = (task.Steps ?? new())
                .Where(s => s.IsDone && s.CompletedAt is DateTime d && InWindow(d))
                .GroupBy(s => DayOf(s.CompletedAt!.Value))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TicketStep>)g.OrderBy(s => s.CompletedAt).ToList());

            DateTime? doneDay = task.State == "Done" && task.CompletedAt is DateTime at && InWindow(at) ? DayOf(at) : null;

            // One entry per day this task had something completed: the task itself, ticked steps, or both
            foreach (var day in stepsByDay.Keys.Concat(doneDay is DateTime dd ? new[] { dd } : Array.Empty<DateTime>()).Distinct())
            {
                stepsByDay.TryGetValue(day, out var steps);
                Add(day, new DoneLogEntry(task, doneDay == day, steps ?? Array.Empty<TicketStep>()));
            }
        }

        static DateTime? Latest(DoneLogEntry e) =>
            new[] { e.TaskDone ? e.Task.CompletedAt : null, e.Steps.Max(s => s.CompletedAt) }.Max();

        return perDay
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new DoneLogDay(kv.Key, DayHeader(kv.Key, now, culture),
                kv.Value.OrderByDescending(Latest).ToList()))
            .ToList();
    }
}
