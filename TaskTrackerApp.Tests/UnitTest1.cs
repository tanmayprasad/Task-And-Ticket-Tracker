namespace TaskTrackerApp.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ContextEngine_InitialState_IsNotDistracted()
    {
        var engine = new ContextAwareEngine();
        Assert.That(engine.IsInDistractionState, Is.False);
    }
}
