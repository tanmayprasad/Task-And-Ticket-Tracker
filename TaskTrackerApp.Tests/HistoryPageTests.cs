using System.Collections.ObjectModel;
using System.Globalization;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

/// <summary>History page rules beyond the basics in DoneLogTests: window, status, step progress, ordering.</summary>
public class HistoryPageTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 14, 0, 0);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static TaskModel Task(string title, string state, DateTime? completed, params (bool done, DateTime? at)[] steps) => new()
    {
        Title = title,
        State = state,
        CompletedAt = completed,
        Steps = new ObservableCollection<TicketStep>(steps.Select((s, i) =>
            new TicketStep { Description = $"{title}-{i}", IsDone = s.done, CompletedAt = s.at })),
    };

    [Test]
    public void ShowsExactlyTodayYesterdayAndThreeEarlierDays()
    {
        var tasks = Enumerable.Range(0, 7).Select(d => Task($"d{d}", "Done", Now.AddDays(-d))).ToList();

        var days = DoneLog.Build(tasks, Now, Culture);

        Assert.That(days, Has.Count.EqualTo(5));
        Assert.That(days[0].Header, Is.EqualTo("Today"));
        Assert.That(days[1].Header, Is.EqualTo("Yesterday"));
        Assert.That(days.Select(d => d.Date), Is.Ordered.Descending);
        Assert.That(days[^1].Date, Is.EqualTo(Now.Date.AddDays(-4)));
    }

    [Test]
    public void DaysWithNothingDone_AreLeftOut()
    {
        var days = DoneLog.Build(new[] { Task("a", "Done", Now), Task("b", "Done", Now.AddDays(-3)) }, Now, Culture);

        Assert.That(days.Select(d => d.Header), Is.EqualTo(new[] { "Today", "Monday, 12 Oct" }));
    }

    [Test]
    public void EmptyHistory_ProducesNoDays()
    {
        Assert.That(DoneLog.Build(Array.Empty<TaskModel>(), Now, Culture), Is.Empty);
        Assert.That(DoneLog.Build(new[] { Task("open", "To Do", null, (false, null)) }, Now, Culture), Is.Empty);
    }

    [Test]
    public void EntryExposesCurrentTaskStatus_NotTheStatusOnThatDay()
    {
        // A step ticked yesterday on a task that has since been finished: yesterday's row still shows "Done"
        var t = Task("a", "Done", Now, (true, Now.AddDays(-1)));
        var yesterday = DoneLog.Build(new[] { t }, Now, Culture)[1];

        Assert.That(yesterday.Entries.Single().Task.State, Is.EqualTo("Done"));
        Assert.That(yesterday.Entries.Single().TaskDone, Is.False);
    }

    [Test]
    public void DoneTaskWithTickedSteps_ListsTheStepsAndNotTheUntickedOnes()
    {
        var t = Task("a", "Done", Now, (true, Now.AddHours(-2)), (true, Now.AddHours(-1)), (false, null));
        var entry = DoneLog.Build(new[] { t }, Now, Culture)[0].Entries.Single();

        Assert.That(entry.TaskDone, Is.True);
        Assert.That(entry.Steps, Has.Count.EqualTo(2));
        Assert.That(entry.StepsText, Is.EqualTo("2/3 Steps"));
    }

    [Test]
    public void StepsText_IsEmptyForTasksWithoutSteps_SoTheLabelIsHidden()
    {
        var entry = DoneLog.Build(new[] { Task("a", "Done", Now) }, Now, Culture)[0].Entries.Single();
        Assert.That(entry.StepsText, Is.Empty);
    }

    [Test]
    public void WithinADay_MostRecentlyCompletedComesFirst()
    {
        var older = Task("older", "Done", Now.AddHours(-5));
        var newer = Task("newer", "Done", Now.AddHours(-1));
        var stepOnly = Task("stepOnly", "In progress", null, (true, Now.AddHours(-3)));

        var titles = DoneLog.Build(new[] { older, stepOnly, newer }, Now, Culture)[0].Entries.Select(e => e.Task.Title);

        Assert.That(titles, Is.EqualTo(new[] { "newer", "stepOnly", "older" }));
    }

    [Test]
    public void ReopenedTask_DisappearsFromHistory()
    {
        var t = Task("a", "Done", Now);
        t.State = "In progress";
        t.SyncCompletion(Now);

        Assert.That(DoneLog.Build(new[] { t }, Now, Culture), Is.Empty);
    }

    [Test]
    public void CountText_LeavesOutZeroParts()
    {
        var onlyTask = DoneLog.Build(new[] { Task("a", "Done", Now) }, Now, Culture)[0];
        Assert.That(onlyTask.CountText, Is.EqualTo("1 task"));

        var two = DoneLog.Build(new[] { Task("a", "Done", Now), Task("b", "Done", Now) }, Now, Culture)[0];
        Assert.That(two.CountText, Is.EqualTo("2 tasks"));
    }

    [Test]
    public void CompletionTimeInTheFuture_IsShownAsToday()
    {
        var t = Task("a", "Done", Now.AddHours(30)); // clock moved back or imported data
        var days = DoneLog.Build(new[] { t }, Now, Culture);

        Assert.That(days.Single().Header, Is.EqualTo("Today"));
    }
}
