namespace TaskTrackerApp.Tests;

public class StartupLauncherTests
{
    private const string Exe = @"C:\Program Files\TaskTrackerApp\TaskTrackerApp.exe";

    [Test]
    public void RunCommand_QuotesThePath_AndMarksTheStart()
    {
        Assert.That(StartupLauncher.BuildRunCommand(Exe), Is.EqualTo("\"C:\\Program Files\\TaskTrackerApp\\TaskTrackerApp.exe\" --startup"));
    }

    [Test]
    public void RunCommand_MatchesItsOwnExe_IgnoringCase()
    {
        string value = StartupLauncher.BuildRunCommand(Exe).ToUpperInvariant();

        Assert.That(StartupLauncher.RunCommandMatches(value, Exe), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("\"D:\\Other\\TaskTrackerApp.exe\" --startup")] // another copy of the app
    [TestCase("\"C:\\Program Files\\TaskTrackerApp\\TaskTrackerApp.exe\"")] // no start-up marker
    public void RunCommand_DoesNotMatch(string? value)
    {
        Assert.That(StartupLauncher.RunCommandMatches(value, Exe), Is.False);
    }

    [TestCase(new[] { "--startup" }, true)]
    [TestCase(new[] { "--STARTUP" }, true)]
    [TestCase(new[] { "other", "--startup" }, true)]
    [TestCase(new string[0], false)]
    [TestCase(new[] { "--start" }, false)]
    public void StartupArgument_IsRecognised(string[] args, bool expected)
    {
        Assert.That(StartupLauncher.HasStartupArgument(args), Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x02, 0, 0, 0 }, false)]
    [TestCase(new byte[] { 0x06, 0, 0, 0 }, false)]
    [TestCase(new byte[] { 0x03, 0, 0, 0 }, true)]
    [TestCase(new byte[] { 0x07, 0, 0, 0 }, true)]
    [TestCase(new byte[0], false)]
    [TestCase(null, false)]
    public void ApprovedEntry_OddFirstByte_MeansDisabledByUser(byte[]? value, bool expected)
    {
        Assert.That(StartupLauncher.IsApprovedEntryDisabled(value), Is.EqualTo(expected));
    }
}
