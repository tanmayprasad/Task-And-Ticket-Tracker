using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

/// <summary>Pure parsing helpers for fast task entry. Unit-tested.</summary>
public static class TaskInput
{
    // "#<anything> title"  → explicit ticket
    private static readonly Regex HashTicket = new(@"^#(?<ticket>\S+)\s+(?<title>.+)$", RegexOptions.Compiled);

    // "12345 title" (3+ digits) or "AB-12 title" (Jira/ADO style key) → ticket without '#'
    private static readonly Regex BareTicket = new(@"^(?<ticket>\d{3,}|[A-Za-z][A-Za-z0-9]*-\d+)\s+(?<title>.+)$", RegexOptions.Compiled);

    // Leading list markers when pasting steps: "- ", "* ", "• ", "1. ", "1) ", "[ ] ", "[x] "
    private static readonly Regex ListMarker = new(@"^\s*(?:[-*•]\s+|\d+[.)]\s+|\[[ xX]?\]\s+)", RegexOptions.Compiled);

    /// <summary>
    /// Splits quick-add text into an optional ticket number and a title.
    /// "#123 Fix login" → ("123", "Fix login"); "AB-12 Fix" → ("AB-12", "Fix"); "Call Bob" → (null, "Call Bob").
    /// Short bare numbers ("3 bugs to fix") are kept as part of the title.
    /// </summary>
    public static (string? Ticket, string Title) ParseQuickAdd(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0) return (null, string.Empty);

        var match = HashTicket.Match(text);
        if (!match.Success) match = BareTicket.Match(text);
        if (!match.Success) return (null, text);

        return (match.Groups["ticket"].Value, match.Groups["title"].Value.Trim());
    }

    /// <summary>One step per non-empty line, with bullets/numbering removed and each step capped at <paramref name="maxLength"/>.</summary>
    public static List<string> SplitStepLines(string? text, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        return text
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(line => ListMarker.Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Length > maxLength ? line[..maxLength] : line)
            .ToList();
    }

    /// <summary>The next auto-generated ticket number: "i" + (highest existing iN + 1).</summary>
    public static string NextAutoTicket(IEnumerable<TaskModel> tasks)
    {
        int max = 0;
        foreach (var t in tasks)
        {
            var n = t.VstsNumber;
            if (!string.IsNullOrEmpty(n) && n.StartsWith("i", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(n.AsSpan(1), out int num) && num > max)
            {
                max = num;
            }
        }
        return $"i{max + 1}";
    }

    /// <summary>The Monday of next week (used by the "Next week" due-date chip).</summary>
    public static DateTime NextWeekStart(DateTime today)
    {
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.Date.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);
    }
}
