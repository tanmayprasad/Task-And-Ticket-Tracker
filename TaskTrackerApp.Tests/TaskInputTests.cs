using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class TaskInputTests
{
    [TestCase("#123 Fix login", "123", "Fix login")]
    [TestCase("#AB-12 Fix login", "AB-12", "Fix login")]
    [TestCase("AB-12 Fix login", "AB-12", "Fix login")]
    [TestCase("48213 Review PR", "48213", "Review PR")]
    [TestCase("  #7   Call Bob  ", "7", "Call Bob")]
    public void ParseQuickAdd_WithTicket_SplitsTicketAndTitle(string input, string ticket, string title)
    {
        Assert.That(TaskInput.ParseQuickAdd(input), Is.EqualTo(((string?)ticket, title)));
    }

    [TestCase("Call Bob", "Call Bob")]
    [TestCase("3 bugs to fix", "3 bugs to fix")]     // short bare numbers stay in the title
    [TestCase("#123", "#123")]                       // a ticket alone is not enough: keep it as the title
    [TestCase("Fix AB-12 today", "Fix AB-12 today")] // only a leading key counts
    public void ParseQuickAdd_WithoutTicket_KeepsWholeTitle(string input, string title)
    {
        Assert.That(TaskInput.ParseQuickAdd(input), Is.EqualTo(((string?)null, title)));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ParseQuickAdd_Empty_ReturnsEmptyTitle(string? input)
    {
        Assert.That(TaskInput.ParseQuickAdd(input).Title, Is.Empty);
    }

    [Test]
    public void SplitStepLines_StripsBulletsNumbersAndBlankLines()
    {
        var text = "- Reproduce\r\n* Write test\n\n• Patch\n1. Review\n2) Merge\n[ ] Deploy\n[x] Verify\n   ";
        Assert.That(TaskInput.SplitStepLines(text),
            Is.EqualTo(new[] { "Reproduce", "Write test", "Patch", "Review", "Merge", "Deploy", "Verify" }));
    }

    [Test]
    public void SplitStepLines_CapsLength()
    {
        var steps = TaskInput.SplitStepLines(new string('a', 150), maxLength: 100);
        Assert.That(steps.Single().Length, Is.EqualTo(100));
    }

    [Test]
    public void NextAutoTicket_UsesHighestINumber()
    {
        var tasks = new[] { "i3", "I17", "ABC-5", "", "i2x", "i9" }.Select(n => new TaskModel { VstsNumber = n });
        Assert.That(TaskInput.NextAutoTicket(tasks), Is.EqualTo("i18"));
        Assert.That(TaskInput.NextAutoTicket(Array.Empty<TaskModel>()), Is.EqualTo("i1"));
    }

    [TestCase(2026, 9, 24, 2026, 9, 28)] // Thursday → next Monday
    [TestCase(2026, 9, 28, 2026, 10, 5)] // Monday → the Monday after
    [TestCase(2026, 9, 27, 2026, 9, 28)] // Sunday → tomorrow (Monday)
    public void NextWeekStart_IsTheComingMonday(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.That(TaskInput.NextWeekStart(new DateTime(y, m, d)), Is.EqualTo(new DateTime(ey, em, ed)));
    }
}
