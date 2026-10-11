using System.IO;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Tests;

public class ProfileStoreTests
{
    private string _dir = null!;

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

    [Test]
    public void FirstRun_CreatesDefaultOnTheExistingTasksFile_WithFocusSettingsFromSettings()
    {
        new TaskRepository(_dir).SaveTasks([new TaskModel { Title = "Existing" }]);

        var store = new ProfileStore(_dir, new SettingsModel { MaxActiveTasks = 3, AlertTimerHours = 1, NotificationsEnabled = false });

        Assert.That(store.Profiles, Has.Count.EqualTo(1));
        var profile = store.Active;
        Assert.That(profile.Name, Is.EqualTo("Default"));
        Assert.That(store.FolderOf(profile), Is.EqualTo(_dir)); // tasks.json is not moved
        Assert.That(store.RepositoryFor(profile).LoadTasks().Single().Title, Is.EqualTo("Existing"));
        Assert.That((profile.MaxActiveTasks, profile.AlertTimerHours, profile.NotificationsEnabled), Is.EqualTo((3, 1.0, false)));
        Assert.That(File.Exists(Path.Combine(_dir, "profiles.json")), Is.True);
    }

    [Test]
    public void Reload_KeepsProfilesAndTheActiveOne()
    {
        var store = new ProfileStore(_dir);
        var work = store.Create("Work");
        store.SetActive(work.Id);

        var reloaded = new ProfileStore(_dir);

        Assert.That(reloaded.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "Default", "Work" }));
        Assert.That(reloaded.Active.Id, Is.EqualTo(work.Id));
    }

    [Test]
    public void Create_CopiesFocusSettingsFromTheNewestProfile_AndGetsItsOwnFolder()
    {
        var store = new ProfileStore(_dir);
        var first = store.Create("Work");
        store.Update(first.Id, p => { p.MaxActiveTasks = 5; p.AlertTimerHours = 24; p.NotificationsEnabled = false; });

        var second = store.Create("Side project");

        Assert.That((second.MaxActiveTasks, second.AlertTimerHours, second.NotificationsEnabled), Is.EqualTo((5, 24.0, false)));
        Assert.That(store.FolderOf(second), Is.Not.EqualTo(store.FolderOf(first)));
        Assert.That(Directory.Exists(store.FolderOf(second)), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("default")] // case-insensitive duplicate
    public void InvalidNames_AreRejected(string name)
    {
        var store = new ProfileStore(_dir);

        Assert.Throws<ArgumentException>(() => store.Create(name));
    }

    [Test]
    public void Rename_ToItsOwnNameInAnotherCase_IsAllowed()
    {
        var store = new ProfileStore(_dir);
        var id = store.Active.Id;

        store.Rename(id, "  DEFAULT ");

        Assert.That(store.Active.Name, Is.EqualTo("DEFAULT"));
    }

    [Test]
    public void Delete_MovesTheTaskFileAside_AndSwitchesAwayFromTheActiveProfile()
    {
        var store = new ProfileStore(_dir);
        var work = store.Create("Work");
        store.RepositoryFor(work).SaveTasks([new TaskModel { Title = "Keep me" }]);
        store.SetActive(work.Id);

        var movedTo = store.Delete(work.Id);

        Assert.That(store.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "Default" }));
        Assert.That(store.Active.Name, Is.EqualTo("Default"));
        Assert.That(File.Exists(Path.Combine(movedTo, "tasks.json")), Is.True);
        Assert.That(movedTo, Does.Contain(Path.Combine("profiles", ".deleted")));
    }

    [Test]
    public void Delete_TheOnlyProfile_IsRefused()
    {
        var store = new ProfileStore(_dir);

        Assert.Throws<InvalidOperationException>(() => store.Delete(store.Active.Id));
    }

    [Test]
    public void CorruptProfilesFile_FallsBackToTheBackup()
    {
        var store = new ProfileStore(_dir);
        store.Create("Work");
        store.Create("Home"); // the previous version (Default + Work) is now profiles.json.bak
        File.WriteAllText(Path.Combine(_dir, "profiles.json"), "{ not json");

        var reloaded = new ProfileStore(_dir);

        Assert.That(reloaded.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "Default", "Work" }));
    }
}
