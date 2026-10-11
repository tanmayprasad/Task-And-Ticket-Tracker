using System.Windows;
using TaskTrackerApp.Onboarding;

namespace TaskTrackerApp.Tests;

public class TourSchedulerTests
{
    private static readonly Version V1501 = new(1, 5, 0, 1);
    private static readonly Version V1510 = new(1, 5, 1, 0);
    private static readonly Version V1600 = new(1, 6, 0, 0);

    // 1.5.0.1 introduced the tour itself, so the welcome tour covers it
    private static readonly ReleaseNoteInfo[] Releases =
    [
        new(V1501, CoveredByWelcome: true),
        new(V1510, CoveredByWelcome: false),
        new(V1600, CoveredByWelcome: false),
    ];

    [Test]
    public void NewUser_GetsWelcome_WithoutReleaseNotes()
    {
        var d = TourScheduler.Decide(onboardingCompleted: false, isExistingUser: false, lastSeenVersion: null, V1600, Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.Welcome));
        Assert.That(d.IsReturningUser, Is.False);
        Assert.That(d.Releases, Is.Empty);
    }

    [Test]
    public void ExistingUser_UpdatingFromBeforeTours_GetsWelcomeOnce_PlusNotesNotCoveredByIt()
    {
        var d = TourScheduler.Decide(onboardingCompleted: false, isExistingUser: true, lastSeenVersion: null, V1510, Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.Welcome));
        Assert.That(d.IsReturningUser, Is.True);
        Assert.That(d.Releases, Is.EqualTo(new[] { V1510 })); // 1.5.0.1 is already in the welcome tour
    }

    [Test]
    public void OnboardedUser_Updating_GetsWhatsNewOnly()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "1.5.0.1", V1510, Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.WhatsNew));
        Assert.That(d.Releases, Is.EqualTo(new[] { V1510 }));
    }

    [Test]
    public void SkippedVersions_CollectEveryReleaseInBetween_OldestFirst()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "1.5.0.0", V1600, Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.WhatsNew));
        Assert.That(d.Releases, Is.EqualTo(new[] { V1501, V1510, V1600 }));
    }

    [Test]
    public void SameVersion_ShowsNothing()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "1.6.0.0", V1600, Releases);

        Assert.That(d, Is.EqualTo(TourDecision.None));
    }

    [Test]
    public void Downgrade_ShowsNothing_AndKeepsTheNewerVersion()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "1.6.0.0", V1510, Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.None));
        Assert.That(TourScheduler.VersionToRecord("1.6.0.0", V1510), Is.EqualTo("1.6.0.0"));
    }

    [Test]
    public void UpdateWithoutReleaseNotes_ShowsNothing()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "1.6.0.0", new Version(1, 6, 1, 0), Releases);

        Assert.That(d.Kind, Is.EqualTo(TourKind.None));
    }

    [TestCase(null, "1.6.0.0")]
    [TestCase("garbage", "1.6.0.0")]
    [TestCase("1.5.1.0", "1.6.0.0")]
    public void VersionToRecord_MovesForward(string? lastSeen, string expected)
    {
        Assert.That(TourScheduler.VersionToRecord(lastSeen, V1600), Is.EqualTo(expected));
    }

    [Test]
    public void UnreadableLastSeen_IsTreatedAsTheBaseline()
    {
        var d = TourScheduler.Decide(onboardingCompleted: true, isExistingUser: true, lastSeenVersion: "not a version", V1501, Releases);

        Assert.That(d.Releases, Is.EqualTo(new[] { V1501 }));
    }
}

public class CoachLayoutTests
{
    private static readonly Size Area = new(1000, 700);
    private static readonly Size Card = new(360, 200);

    [Test]
    public void NoTarget_CentersTheCard()
    {
        Assert.That(CoachLayout.PlaceCard(null, Card, Area), Is.EqualTo(new Point(320, 250)));
    }

    [Test]
    public void RoomBelow_PlacesCardBelow_LeftAlignedWithAWideTarget()
    {
        var p = CoachLayout.PlaceCard(new Rect(100, 80, 600, 44), Card, Area);

        Assert.That(p, Is.EqualTo(new Point(100, 80 + 44 + CoachLayout.Gap)));
    }

    [Test]
    public void NarrowTargetNearTheRightEdge_KeepsTheCardInsideTheWindow()
    {
        var p = CoachLayout.PlaceCard(new Rect(940, 0, 50, 32), Card, Area);

        Assert.That(p.X, Is.EqualTo(Area.Width - Card.Width - CoachLayout.Margin));
        Assert.That(p.Y, Is.EqualTo(32 + CoachLayout.Gap));
    }

    [Test]
    public void NoRoomBelow_PlacesCardAbove()
    {
        var p = CoachLayout.PlaceCard(new Rect(100, 600, 200, 60), Card, Area);

        Assert.That(p.Y, Is.EqualTo(600 - CoachLayout.Gap - Card.Height));
    }

    [Test]
    public void TallNarrowTarget_PlacesCardToTheRight()
    {
        var p = CoachLayout.PlaceCard(new Rect(0, 40, 48, 650), Card, Area);

        Assert.That(p, Is.EqualTo(new Point(48 + CoachLayout.Gap, 40)));
    }

    [Test]
    public void TargetFillingTheWindow_PutsCardInsideNearItsBottom()
    {
        var p = CoachLayout.PlaceCard(new Rect(5, 5, 990, 690), Card, Area);

        Assert.That(p.Y, Is.EqualTo(695 - CoachLayout.Gap - Card.Height));
    }
}
