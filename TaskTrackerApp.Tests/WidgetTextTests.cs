using TaskTrackerApp.Models;
using TaskTrackerApp.Widgets;

namespace TaskTrackerApp.Tests;

public class WidgetTextTests
{
    private static TaskModel Task(string ticket, string title, params (string Text, bool Done)[] steps)
    {
        var task = new TaskModel { VstsNumber = ticket, Title = title };
        foreach (var (text, done) in steps) task.Steps.Add(new TicketStep { Description = text, IsDone = done });
        return task;
    }

    [Test]
    public void CompactText_NoSteps_ShowsTicketAndTaskName()
    {
        Assert.That(WidgetText.CompactText(Task("123", "Fix login")), Is.EqualTo("#123 · Fix login"));
    }

    [Test]
    public void CompactText_WithOpenSteps_ShowsTicketAndFirstOpenStep_NotTaskName()
    {
        var task = Task("123", "Fix login", ("Reproduce", true), ("Write test", false), ("Patch", false));
        Assert.That(WidgetText.CompactText(task), Is.EqualTo("#123 · Write test"));
    }

    [Test]
    public void CompactText_AllStepsDone_FallsBackToTaskName()
    {
        var task = Task("123", "Fix login", ("Reproduce", true));
        Assert.That(WidgetText.CompactText(task), Is.EqualTo("#123 · Fix login"));
    }

    [Test]
    public void CompactText_NoTicketNumber_OmitsPrefix()
    {
        Assert.That(WidgetText.CompactText(Task("  ", "Fix login", ("Patch", false))), Is.EqualTo("Patch"));
    }

    [Test]
    public void DetailText_WithOpenStep_IncludesTitleAndStepPosition()
    {
        var task = Task("123", "Fix login", ("Reproduce", true), ("Write test", false));
        Assert.That(WidgetText.DetailText(task), Is.EqualTo("#123  Fix login\nStep 2/2: Write test"));
    }
}
