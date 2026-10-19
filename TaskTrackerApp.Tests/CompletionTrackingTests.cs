using System.IO;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;
using TaskTrackerApp.Widgets;

namespace TaskTrackerApp.Tests;

/// <summary>
/// Bug: switching between active tasks showed step 1 even when it was already marked.
/// Feature: tasks and steps record when they were completed (feeds the History page).
/// </summary>
public class CompletionTrackingTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 14, 0, 0);

    private static TaskModel TaskWithSteps(params bool[] done) => new()
    {
        Title = "T",
        Steps = new(done.Select((d, i) => new TicketStep { Description = $"s{i}", IsDone = d })),
    };

    // ---- Bug: visible step after switching tasks

    [Test]
    public void FirstIncompleteStepIndex_SkipsMarkedSteps()
    {
        Assert.That(WidgetText.FirstIncompleteStepIndex(TaskWithSteps(true, true, false, false)), Is.EqualTo(2));
    }

    [Test]
    public void FirstIncompleteStepIndex_IsZeroWhenFirstStepIsOpen()
    {
        Assert.That(WidgetText.FirstIncompleteStepIndex(TaskWithSteps(false, true)), Is.EqualTo(0));
    }

    [Test]
    public void FirstIncompleteStepIndex_FindsHoleBeforeLaterDoneSteps()
    {
        Assert.That(WidgetText.FirstIncompleteStepIndex(TaskWithSteps(true, false, true)), Is.EqualTo(1));
    }

    [Test]
    public void FirstIncompleteStepIndex_FallsBackToZero_WhenAllDoneOrNoSteps()
    {
        Assert.That(WidgetText.FirstIncompleteStepIndex(TaskWithSteps(true, true)), Is.EqualTo(0));
        Assert.That(WidgetText.FirstIncompleteStepIndex(TaskWithSteps()), Is.EqualTo(0));
    }

    [Test]
    public void FirstIncompleteStepIndex_MatchesTheStepTheWidgetShows()
    {
        var task = TaskWithSteps(true, false, false);
        Assert.That(task.Steps[WidgetText.FirstIncompleteStepIndex(task)], Is.SameAs(WidgetText.CurrentStep(task)));
    }

    // ---- Feature: completion timestamps

    [Test]
    public void Task_SyncCompletion_StampsOnDone_KeepsFirstStamp_ClearsOnReopen()
    {
        var task = new TaskModel { State = "Done" };
        task.SyncCompletion(Now);
        Assert.That(task.CompletedAt, Is.EqualTo(Now));

        task.SyncCompletion(Now.AddHours(5));
        Assert.That(task.CompletedAt, Is.EqualTo(Now), "re-syncing must not move the time");

        task.State = "In progress";
        task.SyncCompletion(Now);
        Assert.That(task.CompletedAt, Is.Null);
    }

    [Test]
    public void ApplyEditedSteps_StampsNewlyTickedStep()
    {
        var id = Guid.NewGuid();
        var previous = new[] { new TicketStep { Id = id, Description = "a", IsDone = false } };
        var edited = new[] { new TicketStep { Id = id, Description = "a", IsDone = true } };

        var result = TaskModel.ApplyEditedSteps(edited, previous, Now);

        Assert.That(result.Single().CompletedAt, Is.EqualTo(Now));
    }

    [Test]
    public void ApplyEditedSteps_ClearsStampOfUntickedStep()
    {
        var id = Guid.NewGuid();
        var previous = new[] { new TicketStep { Id = id, IsDone = true, CompletedAt = Now.AddDays(-1) } };
        var edited = new[] { new TicketStep { Id = id, IsDone = false, CompletedAt = Now.AddDays(-1) } };

        Assert.That(TaskModel.ApplyEditedSteps(edited, previous, Now).Single().CompletedAt, Is.Null);
    }

    [Test]
    public void ApplyEditedSteps_KeepsOriginalTime_WhenStepStaysDone()
    {
        var id = Guid.NewGuid();
        var earlier = Now.AddDays(-2);
        var previous = new[] { new TicketStep { Id = id, IsDone = true, CompletedAt = earlier } };
        var edited = new[] { new TicketStep { Id = id, IsDone = true, CompletedAt = earlier } };

        Assert.That(TaskModel.ApplyEditedSteps(edited, previous, Now).Single().CompletedAt, Is.EqualTo(earlier));
    }

    [Test]
    public void ApplyEditedSteps_DoesNotInventATimeForStepsDoneBeforeTracking()
    {
        var id = Guid.NewGuid();
        var previous = new[] { new TicketStep { Id = id, IsDone = true, CompletedAt = null } };
        var edited = new[] { new TicketStep { Id = id, IsDone = true, CompletedAt = null } };

        Assert.That(TaskModel.ApplyEditedSteps(edited, previous, Now).Single().CompletedAt, Is.Null,
            "a legacy done step must not suddenly appear as finished today");
    }

    [Test]
    public void ApplyEditedSteps_StampsBrandNewStepAddedAlreadyDone()
    {
        var edited = new[] { new TicketStep { IsDone = true } };
        Assert.That(TaskModel.ApplyEditedSteps(edited, null, Now).Single().CompletedAt, Is.EqualTo(Now));
    }

    // ---- Persistence

    [Test]
    public void CompletedAt_RoundTripsThroughTheRepository()
    {
        var dir = Path.Combine(Path.GetTempPath(), "TaskTrackerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new TaskRepository(dir);
            var task = new TaskModel { Title = "A", State = "Done", CompletedAt = Now };
            task.Steps.Add(new TicketStep { Description = "s", IsDone = true, CompletedAt = Now.AddHours(-1) });

            repo.SaveTasks(new List<TaskModel> { task });
            var loaded = repo.LoadTasks().Single();

            Assert.That(loaded.CompletedAt, Is.EqualTo(Now));
            Assert.That(loaded.Steps.Single().CompletedAt, Is.EqualTo(Now.AddHours(-1)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void LegacyTasksFile_WithoutCompletedAt_LoadsWithNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "TaskTrackerTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "tasks.json"),
                "[{\"Id\":\"" + Guid.NewGuid() + "\",\"Title\":\"Old\",\"State\":\"Done\",\"Steps\":[{\"Id\":\"" + Guid.NewGuid() + "\",\"Description\":\"x\",\"IsDone\":true}]}]");

            var loaded = new TaskRepository(dir).LoadTasks().Single();

            Assert.That(loaded.Title, Is.EqualTo("Old"));
            Assert.That(loaded.CompletedAt, Is.Null);
            Assert.That(loaded.Steps.Single().CompletedAt, Is.Null);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void Import_Merge_CarriesCompletionTimesToTheUpdatedTask()
    {
        var existing = new TaskModel { VstsNumber = "1", Title = "A", State = "In progress" };
        var incoming = new TaskModel
        {
            VstsNumber = "1", Title = "A", State = "Done", CompletedAt = Now,
            Steps = { new TicketStep { Description = "s", IsDone = true, CompletedAt = Now } },
        };

        TaskTransfer.Merge(new[] { existing }, new[] { incoming });

        Assert.That(existing.State, Is.EqualTo("Done"));
        Assert.That(existing.CompletedAt, Is.EqualTo(Now));
        Assert.That(existing.Steps.Single().CompletedAt, Is.EqualTo(Now));
    }

    // ---- Bug: minimizing to the widget showed an already done step

    [Test]
    public void StepIndexOnRefresh_DefaultsToTheFirstOpenStep()
    {
        var task = TaskWithSteps(true, true, false);
        // stale index 0 (a done step) from before the steps were ticked in the main window
        Assert.That(WidgetText.StepIndexOnRefresh(task, currentIndex: 0, userPickedStep: false), Is.EqualTo(2));
    }

    [Test]
    public void StepIndexOnRefresh_KeepsTheStepTheUserPagedTo()
    {
        var task = TaskWithSteps(true, true, false);
        Assert.That(WidgetText.StepIndexOnRefresh(task, currentIndex: 1, userPickedStep: true), Is.EqualTo(1));
    }

    [Test]
    public void StepIndexOnRefresh_IgnoresAPickedIndexThatNoLongerExists()
    {
        var task = TaskWithSteps(false, false);
        Assert.That(WidgetText.StepIndexOnRefresh(task, currentIndex: 5, userPickedStep: true), Is.EqualTo(0));
    }

    // ---- Review follow-ups

    [Test]
    public void ClearStaleCompletion_DropsTimesOfReopenedTasksAndUntickedSteps()
    {
        var task = new TaskModel { State = "In progress", CompletedAt = Now };
        task.Steps.Add(new TicketStep { IsDone = false, CompletedAt = Now });
        task.Steps.Add(new TicketStep { IsDone = true, CompletedAt = Now });

        task.ClearStaleCompletion();

        Assert.That(task.CompletedAt, Is.Null);
        Assert.That(task.Steps[0].CompletedAt, Is.Null);
        Assert.That(task.Steps[1].CompletedAt, Is.EqualTo(Now), "a ticked step keeps its time");
    }

    [Test]
    public void ReopenedThenFinishedAgain_GetsANewCompletionTime()
    {
        var task = new TaskModel { State = "Done", CompletedAt = Now.AddDays(-2) };
        task.State = "In progress";
        task.ClearStaleCompletion();
        task.State = "Done";
        task.SyncCompletion(Now);

        Assert.That(task.CompletedAt, Is.EqualTo(Now));
    }

    [Test]
    public void ApplyEditedSteps_ToleratesDuplicateStepIds()
    {
        var id = Guid.NewGuid();
        var previous = new[] { new TicketStep { Id = id }, new TicketStep { Id = id } };
        var edited = new[] { new TicketStep { Id = id, IsDone = true } };

        Assert.DoesNotThrow(() => TaskModel.ApplyEditedSteps(edited, previous, Now));
    }

    [Test]
    public void CsvStyleImport_KeepsRecordedCompletionTimes()
    {
        var stepAt = Now.AddHours(-3);
        var existing = new TaskModel
        {
            VstsNumber = "1", Title = "A", State = "Done", CompletedAt = Now.AddHours(-2),
            Steps = { new TicketStep { Description = "s", IsDone = true, CompletedAt = stepAt } },
        };
        // a CSV row: same ticket, edited title, no times and a fresh step id
        var incoming = new TaskModel
        {
            VstsNumber = "1", Title = "A (edited)", State = "Done",
            Steps = { new TicketStep { Description = "s", IsDone = true } },
        };

        TaskTransfer.Merge(new[] { existing }, new[] { incoming });

        Assert.That(existing.Title, Is.EqualTo("A (edited)"));
        Assert.That(existing.CompletedAt, Is.EqualTo(Now.AddHours(-2)));
        Assert.That(existing.Steps.Single().CompletedAt, Is.EqualTo(stepAt));
    }

    [Test]
    public void CsvStyleImport_ThatFinishesATask_StampsIt()
    {
        var existing = new TaskModel { VstsNumber = "1", Title = "A", State = "In progress" };
        var incoming = new TaskModel { VstsNumber = "1", Title = "A", State = "Done" };

        TaskTransfer.Merge(new[] { existing }, new[] { incoming });

        Assert.That(existing.CompletedAt, Is.Not.Null);
    }

    [Test]
    public void StepIndexOnRefresh_AfterAutoAdvance_FollowsTheFirstOpenStep()
    {
        var task = TaskWithSteps(true, true, false);
        // the user had paged to step 1 (done); after ticking and auto-advancing the flag is cleared
        Assert.That(WidgetText.StepIndexOnRefresh(task, currentIndex: 1, userPickedStep: false), Is.EqualTo(2));
    }
}
