using System.IO;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class TaskRepositoryTests
{
    private string _dir = null!;
    private string TasksFile => Path.Combine(_dir, "tasks.json");

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TaskTrackerTests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static List<TaskModel> Tasks(params string[] titles) =>
        titles.Select(t => new TaskModel { Title = t }).ToList();

    [Test]
    public void Save_ThenLoad_RoundTripsTasksAndStepIds()
    {
        var repo = new TaskRepository(_dir);
        var task = new TaskModel { Title = "A" };
        var step = new TicketStep { Description = "step" };
        task.Steps.Add(step);

        repo.SaveTasks(new List<TaskModel> { task });
        var loaded = repo.LoadTasks();

        Assert.That(loaded.Single().Title, Is.EqualTo("A"));
        Assert.That(loaded.Single().Steps.Single().Id, Is.EqualTo(step.Id));
        Assert.That(repo.LoadWarning, Is.Null);
    }

    [Test]
    public void Save_KeepsPreviousVersionAsBackup()
    {
        var repo = new TaskRepository(_dir);
        repo.SaveTasks(Tasks("first"));
        repo.SaveTasks(Tasks("second"));

        Assert.That(File.Exists(TasksFile + ".bak"), Is.True);
        Assert.That(File.ReadAllText(TasksFile + ".bak"), Does.Contain("first"));
        Assert.That(File.Exists(TasksFile + ".tmp"), Is.False);
    }

    [Test]
    public void Load_CorruptFile_RestoresBackupAndQuarantinesCorruptFile()
    {
        var repo = new TaskRepository(_dir);
        repo.SaveTasks(Tasks("good"));
        repo.SaveTasks(Tasks("good", "newer"));
        File.WriteAllText(TasksFile, "{ not valid json");

        var loaded = repo.LoadTasks();

        Assert.That(loaded.Select(t => t.Title), Is.EqualTo(new[] { "good" }));
        Assert.That(repo.LoadWarning, Does.Contain("backup was restored"));
        Assert.That(Directory.GetFiles(_dir, "tasks.corrupt-*.json"), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(TasksFile), Does.Contain("good"));
    }

    [Test]
    public void Load_CorruptFileWithoutBackup_ReturnsEmptyButPreservesCorruptFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(TasksFile, "garbage");
        var repo = new TaskRepository(_dir);

        var loaded = repo.LoadTasks();

        Assert.That(loaded, Is.Empty);
        Assert.That(repo.LoadWarning, Does.Contain("no usable backup"));
        var quarantined = Directory.GetFiles(_dir, "tasks.corrupt-*.json");
        Assert.That(quarantined, Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(quarantined[0]), Is.EqualTo("garbage"));

        // The next save must not destroy the preserved file
        repo.SaveTasks(Tasks("fresh"));
        Assert.That(File.ReadAllText(quarantined[0]), Is.EqualTo("garbage"));
    }
}
