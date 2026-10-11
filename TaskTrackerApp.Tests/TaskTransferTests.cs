using System.Text.Json;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class TaskTransferTests
{
    private static TaskModel Sample() => new()
    {
        VstsNumber = "AB-12",
        Title = "Fix \"login\", then deploy",
        Description = "Line one\nLine two, with a comma",
        AcceptanceCriteria = "Users can log in",
        State = "In progress",
        IsActive = true,
        Priority = 1,
        TargetDate = new DateTime(2026, 10, 9, 14, 30, 0),
        Steps =
        [
            new TicketStep { Description = "Reproduce", IsDone = true },
            new TicketStep { Description = "Write the fix", IsDone = false },
        ],
    };

    [Test]
    public void Json_RoundTrip_KeepsEverything()
    {
        var original = Sample();
        var json = TaskTransfer.ExportJson([original], "Work", new DateTime(2026, 10, 7));

        var task = TaskTransfer.Import(json, ".json").Tasks.Single();

        Assert.That(JsonSerializer.Serialize(task), Is.EqualTo(JsonSerializer.Serialize(original)));
        Assert.That(json, Does.Contain("\"profile\": \"Work\""));
    }

    [Test]
    public void Json_BareListSuchAsTasksJson_IsAccepted()
    {
        var json = JsonSerializer.Serialize(new List<TaskModel> { Sample(), new() { Title = "Second" } });

        var file = TaskTransfer.Import(json, ".json");

        Assert.That(file.Tasks.Select(t => t.Title), Is.EqualTo(new[] { Sample().Title, "Second" }));
    }

    [Test]
    public void Csv_RoundTrip_KeepsFieldsWithCommasQuotesAndLineBreaks_AndSteps()
    {
        var csv = "﻿" + TaskTransfer.ExportCsv([Sample()]);

        var task = TaskTransfer.Import(csv, ".csv").Tasks.Single();

        Assert.That(task.VstsNumber, Is.EqualTo("AB-12"));
        Assert.That(task.Title, Is.EqualTo(Sample().Title));
        Assert.That(task.Description, Is.EqualTo(Sample().Description));
        Assert.That(task.State, Is.EqualTo("In progress"));
        Assert.That(task.IsActive, Is.True);
        Assert.That(task.TargetDate, Is.EqualTo(new DateTime(2026, 10, 9, 14, 30, 0)));
        Assert.That(task.Steps.Select(s => (s.Description, s.IsDone)), Is.EqualTo(new[] { ("Reproduce", true), ("Write the fix", false) }));
    }

    [Test]
    public void Csv_AllDayDueDate_IsExportedAsADateOnly()
    {
        var task = new TaskModel { Title = "All day", TargetDate = new DateTime(2026, 10, 9) };

        var csv = TaskTransfer.ExportCsv([task]);

        Assert.That(csv, Does.Contain(",2026-10-09,"));
        Assert.That(TaskTransfer.Import(csv, ".csv").Tasks.Single().TargetDate, Is.EqualTo(new DateTime(2026, 10, 9)));
    }

    [Test]
    public void Csv_FromASpreadsheet_MatchesHeadersInAnyCaseAndOrder_AndFixesBadValues()
    {
        const string csv = "title,STATE,due,Notes\r\nCall Bob,Doing,not a date,ignored\r\nShip it,Blocked,,\r\n,To Do,,\r\n";

        var file = TaskTransfer.Import(csv, ".csv");

        Assert.That(file.Tasks.Select(t => (t.Title, t.State)), Is.EqualTo(new[] { ("Call Bob", "In progress"), ("Ship it", "To Do") }));
        Assert.That(file.Tasks[0].TargetDate, Is.Null);
        Assert.That(file.Warnings, Has.Count.EqualTo(2)); // the bad date and the row without a title
    }

    [Test]
    public void Csv_WithoutATitleColumn_IsRejected()
    {
        Assert.Throws<FormatException>(() => TaskTransfer.Import("Ticket,State\r\n1,To Do\r\n", ".csv"));
    }

    [Test]
    public void Json_ThatIsNotATaskList_IsRejected()
    {
        Assert.Throws<FormatException>(() => TaskTransfer.Import("{ \"hello\": 1 }", ".json"));
        Assert.Throws<FormatException>(() => TaskTransfer.Import("not json", ".json"));
    }

    [Test]
    public void LongTexts_AreCutToTheFormLimits()
    {
        var json = JsonSerializer.Serialize(new List<TaskModel> { new() { Title = new string('t', 300), Description = new string('d', 5000) } });

        var task = TaskTransfer.Import(json, ".json").Tasks.Single();

        Assert.That((task.Title.Length, task.Description.Length), Is.EqualTo((TaskTransfer.TitleLimit, TaskTransfer.TextLimit)));
    }

    [Test]
    public void Merge_SameTicketNumber_UpdatesTheExistingTaskInPlace()
    {
        var existing = new List<TaskModel>
        {
            new() { VstsNumber = "100", Title = "Old title", Description = "old", State = "To Do" },
            new() { VstsNumber = "200", Title = "Second" },
        };
        var originalId = existing[0].Id;
        var incoming = new List<TaskModel>
        {
            new() { VstsNumber = " 100 ", Title = "New title", Description = "new", State = "Done", Steps = [new TicketStep { Description = "s1" }] },
            new() { VstsNumber = "300", Title = "Third" },
        };

        var result = TaskTransfer.Merge(existing, incoming);

        Assert.That((result.Added, result.Updated, result.Unchanged, result.Skipped), Is.EqualTo((1, 1, 0, 0)));
        Assert.That(result.Tasks.Select(t => t.VstsNumber), Is.EqualTo(new[] { "100", "200", "300" })); // position kept
        var updated = result.Tasks[0];
        Assert.That((updated.Id, updated.Title, updated.Description, updated.State), Is.EqualTo((originalId, "New title", "new", "Done")));
        Assert.That(updated.Steps.Single().Description, Is.EqualTo("s1"));
    }

    [Test]
    public void Merge_UpdateToAStateOtherThanInProgress_DeactivatesTheTask()
    {
        var existing = new List<TaskModel> { new() { VstsNumber = "1", Title = "A", IsActive = true, State = "In progress" } };

        var result = TaskTransfer.Merge(existing, [new TaskModel { VstsNumber = "1", Title = "A", State = "To Do" }]);

        Assert.That(result.Tasks.Single().IsActive, Is.False);
    }

    [Test]
    public void Merge_IdenticalTask_CountsAsUnchanged_AndSameIdMatchesToo()
    {
        var existing = new List<TaskModel> { new() { VstsNumber = "1", Title = "One" } };
        var sameId = new TaskModel { Id = existing[0].Id, VstsNumber = "1", Title = "One" };

        var result = TaskTransfer.Merge(existing, [sameId]);

        Assert.That((result.Added, result.Updated, result.Unchanged), Is.EqualTo((0, 0, 1)));
        Assert.That(result.Tasks, Has.Count.EqualTo(1));
    }

    [Test]
    public void Merge_TaskWithoutTicket_MatchesByTitle()
    {
        var existing = new List<TaskModel> { new() { Title = "Call Bob", Description = "x" } };

        var result = TaskTransfer.Merge(existing, [new TaskModel { Title = " call bob ", Description = "y" }]);

        Assert.That((result.Added, result.Updated), Is.EqualTo((0, 1)));
        Assert.That(result.Tasks.Single().Description, Is.EqualTo("y"));
    }

    [Test]
    public void Merge_RepeatedTicketInTheFile_FirstRowWins()
    {
        var incoming = new List<TaskModel> { new() { VstsNumber = "7", Title = "First" }, new() { VstsNumber = "7", Title = "Second" } };

        var result = TaskTransfer.Merge([], incoming);

        Assert.That((result.Added, result.Skipped), Is.EqualTo((1, 1)));
        Assert.That(result.Tasks.Single().Title, Is.EqualTo("First"));
    }

    [Test]
    public void Merge_GivesANewIdToTasksWhoseIdIsUsedInAnotherProfile()
    {
        var task = new TaskModel { Title = "Shared" };
        var oldId = task.Id;

        var result = TaskTransfer.Merge([], [task], new HashSet<Guid> { oldId });

        Assert.That(result.Tasks.Single().Id, Is.Not.EqualTo(oldId));
    }

    [Test]
    public void CapActive_KeepsTheFirstActiveTasks()
    {
        var tasks = Enumerable.Range(1, 4).Select(i => new TaskModel { Title = $"T{i}", IsActive = true, State = "In progress" }).ToList();

        TaskTransfer.CapActive(tasks, 2);

        Assert.That(tasks.Select(t => t.IsActive), Is.EqualTo(new[] { true, true, false, false }));
        Assert.That(tasks[3].State, Is.EqualTo("To Do"));
    }

    [Test]
    public void SuggestedFileName_RemovesCharactersWindowsDoesNotAllow()
    {
        Assert.That(TaskTransfer.SuggestedFileName("Work/Home", new DateTime(2026, 10, 7), ".csv"), Is.EqualTo("Tasks - Work_Home - 2026-10-07.csv"));
    }
}
