using System.Globalization;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class TaskFormatTests
{
    // Thursday 24 Sep 2026, 10:00
    private static readonly DateTime Now = new(2026, 9, 24, 10, 0, 0);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static TaskModel Due(DateTime? due, string state = "To Do") => new() { TargetDate = due, State = state };

    [Test]
    public void NoDueDate_IsEmptyAndNotUrgent()
    {
        Assert.That(TaskFormat.DueText(Due(null), Now, Inv), Is.Empty);
        Assert.That(TaskFormat.Urgency(Due(null), Now), Is.EqualTo(DueUrgency.None));
    }

    [Test]
    public void DueLaterToday_ShowsTodayWithTime()
    {
        var task = Due(Now.Date.AddHours(17));
        Assert.That(TaskFormat.DueText(task, Now, Inv), Is.EqualTo("Today 17:00"));
        Assert.That(TaskFormat.Urgency(task, Now), Is.EqualTo(DueUrgency.Today));
    }

    [Test]
    public void DateOnlyToday_IsNotOverdueDuringTheDay()
    {
        // The picker defaults to 00:00; that means "today", not "overdue since midnight"
        var task = Due(Now.Date);
        Assert.That(TaskFormat.DueText(task, Now, Inv), Is.EqualTo("Today"));
        Assert.That(TaskFormat.Urgency(task, Now), Is.EqualTo(DueUrgency.Today));
    }

    [Test]
    public void PastTime_IsOverdueInHoursThenDays()
    {
        Assert.That(TaskFormat.DueText(Due(Now.AddHours(-3)), Now, Inv), Is.EqualTo("Overdue 3h"));
        Assert.That(TaskFormat.DueText(Due(Now.AddMinutes(-20)), Now, Inv), Is.EqualTo("Overdue"));
        Assert.That(TaskFormat.DueText(Due(Now.Date.AddDays(-2)), Now, Inv), Is.EqualTo("Overdue 2d"));
        Assert.That(TaskFormat.Urgency(Due(Now.AddHours(-3)), Now), Is.EqualTo(DueUrgency.Overdue));
    }

    [Test]
    public void DoneTask_IsNeverUrgent()
    {
        Assert.That(TaskFormat.Urgency(Due(Now.AddDays(-5), "Done"), Now), Is.EqualTo(DueUrgency.None));
    }

    [Test]
    public void UpcomingDates_UseTomorrowWeekdayOrDate()
    {
        Assert.That(TaskFormat.DueText(Due(Now.Date.AddDays(1).AddHours(9)), Now, Inv), Is.EqualTo("Tomorrow 09:00"));
        Assert.That(TaskFormat.DueText(Due(Now.Date.AddDays(2)), Now, Inv), Is.EqualTo("Sat"));
        Assert.That(TaskFormat.DueText(Due(new DateTime(2026, 10, 12)), Now, Inv), Is.EqualTo("12 Oct"));
        Assert.That(TaskFormat.DueText(Due(new DateTime(2027, 1, 5)), Now, Inv), Is.EqualTo("5 Jan 2027"));
        Assert.That(TaskFormat.Urgency(Due(Now.Date.AddDays(2)), Now), Is.EqualTo(DueUrgency.Later));
    }

    [Test]
    public void StepProgress_CountsDoneSteps()
    {
        Assert.That(TaskFormat.StepProgress(null), Is.Empty);
        Assert.That(TaskFormat.StepProgress(new List<TicketStep>()), Is.Empty);
        var steps = new List<TicketStep> { new() { IsDone = true }, new() { IsDone = false }, new() { IsDone = true } };
        Assert.That(TaskFormat.StepProgress(steps), Is.EqualTo("2/3"));
    }
}
