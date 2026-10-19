using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TaskTrackerApp;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class DoneLogTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 14, 0, 0);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static TaskModel Task(string title, string state = "In progress", DateTime? completed = null, params (string, bool, DateTime?)[] steps) =>
        new()
        {
            Title = title,
            State = state,
            CompletedAt = completed,
            Steps = new ObservableCollection<TicketStep>(
                steps.Select(s => new TicketStep { Description = s.Item1, IsDone = s.Item2, CompletedAt = s.Item3 }))
        };

    [Test]
    public void DoneTask_ShowsStepsTickedThatDay()
    {
        var t = Task("Ship", "Done", Now.AddHours(-1), ("a", true, Now.AddHours(-3)), ("b", true, Now.AddHours(-2)));
        var days = DoneLog.Build(new[] { t }, Now, Culture);

        Assert.That(days, Has.Count.EqualTo(1));
        Assert.That(days[0].Header, Is.EqualTo("Today"));
        var entry = days[0].Entries.Single();
        Assert.That(entry.TaskDone, Is.True);
        Assert.That(entry.Steps.Select(s => s.Description), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void DoneTask_WithStepTickedEarlier_SplitsAcrossDays()
    {
        var t = Task("Ship", "Done", Now, ("early", true, Now.AddDays(-1)));
        var days = DoneLog.Build(new[] { t }, Now, Culture);

        Assert.That(days.Select(d => d.Header), Is.EqualTo(new[] { "Today", "Yesterday" }));
        Assert.That(days[0].Entries.Single().TaskDone, Is.True);
        Assert.That(days[0].Entries.Single().Steps, Is.Empty);
        Assert.That(days[1].Entries.Single().TaskDone, Is.False);
        Assert.That(days[1].Entries.Single().Steps.Single().Description, Is.EqualTo("early"));
    }

    [Test]
    public void OpenTask_ShowsOnlyStepsDoneThatDay()
    {
        var t = Task("Work", "In progress", null,
            ("today", true, Now.AddHours(-1)), ("yesterday", true, Now.AddDays(-1)), ("open", false, null));
        var days = DoneLog.Build(new[] { t }, Now, Culture);

        Assert.That(days.Select(d => d.Header), Is.EqualTo(new[] { "Today", "Yesterday" }));
        Assert.That(days[0].Entries[0].Steps.Single().Description, Is.EqualTo("today"));
        Assert.That(days[1].Entries[0].Steps.Single().Description, Is.EqualTo("yesterday"));
    }

    [Test]
    public void ItemsOutsideWindow_AndUnstamped_AreIgnored()
    {
        var old = Task("Old", "Done", Now.AddDays(-5));
        var legacy = Task("Legacy", "Done", null);
        var edge = Task("Edge", "Done", Now.AddDays(-4));

        var days = DoneLog.Build(new[] { old, legacy, edge }, Now, Culture);

        Assert.That(days, Has.Count.EqualTo(1));
        Assert.That(days[0].Entries.Single().Task.Title, Is.EqualTo("Edge"));
    }

    [Test]
    public void Days_AreNewestFirst()
    {
        var a = Task("A", "Done", Now.AddDays(-3));
        var b = Task("B", "Done", Now);
        var c = Task("C", "Done", Now.AddDays(-1));

        var days = DoneLog.Build(new[] { a, b, c }, Now, Culture);

        Assert.That(days.Select(d => d.Entries[0].Task.Title), Is.EqualTo(new[] { "B", "C", "A" }));
    }

    [Test]
    public void SyncCompletion_StampsOnce_AndClearsOnReopen()
    {
        var step = new TicketStep { IsDone = true };
        step.SyncCompletion(Now);
        var stamped = step.CompletedAt;
        step.SyncCompletion(Now.AddHours(3));
        Assert.That(step.CompletedAt, Is.EqualTo(stamped));

        step.IsDone = false;
        step.SyncCompletion(Now);
        Assert.That(step.CompletedAt, Is.Null);
    }

    [Test]
    public void DayHeader_FormatsOlderDays()
    {
        Assert.That(DoneLog.DayHeader(new DateTime(2026, 10, 12), Now, Culture), Is.EqualTo("Monday, 12 Oct"));
    }

    [Test]
    public void StepsText_ShowsWholeTaskProgress()
    {
        var withSteps = Task("A", "In progress", null, ("x", true, Now), ("y", true, Now), ("z", false, null));
        var noSteps = Task("B", "Done", Now);

        var days = DoneLog.Build(new[] { withSteps, noSteps }, Now, Culture);
        var entries = days[0].Entries;

        Assert.That(entries.Single(e => e.Task == withSteps).StepsText, Is.EqualTo("2/3 Steps"));
        Assert.That(entries.Single(e => e.Task == noSteps).StepsText, Is.Empty);
    }

    [Test]
    public void CountText_SummarisesTasksAndSteps()
    {
        var done = Task("A", "Done", Now, ("x", true, Now), ("y", true, Now));
        var open = Task("B", "In progress", null, ("z", true, Now));
        var onlyStep = Task("C", "In progress", null, ("w", true, Now));

        var today = DoneLog.Build(new[] { done, open, onlyStep }, Now, Culture)[0];
        Assert.That(today.CountText, Is.EqualTo("1 task \u00b7 4 steps"));

        var single = DoneLog.Build(new[] { Task("D", "In progress", null, ("s", true, Now)) }, Now, Culture)[0];
        Assert.That(single.CountText, Is.EqualTo("1 step"));
    }
}
