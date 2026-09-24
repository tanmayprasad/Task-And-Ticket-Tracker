using TaskTrackerApp.Widgets;

namespace TaskTrackerApp.Tests;

public class DockMathTests
{
    // 1920x1040 work area (1080p with a 40px bottom taskbar) and a 450x200 widget
    private static readonly PixelRect Work = new(0, 0, 1920, 1040);

    [TestCase(1460, 400, DockEdge.Right)]   // 10px from the right edge
    [TestCase(5, 400, DockEdge.Left)]
    [TestCase(700, 20, DockEdge.Top)]
    [TestCase(700, 830, DockEdge.Bottom)]
    [TestCase(1500, 400, DockEdge.Right)]   // pushed past the edge counts as touching it
    public void NearestEdge_WithinThreshold_ReturnsEdge(int x, int y, DockEdge expected)
    {
        Assert.That(DockMath.NearestEdge(new PixelRect(x, y, 450, 200), Work, 48), Is.EqualTo(expected));
    }

    [Test]
    public void NearestEdge_BeyondThreshold_ReturnsNull()
    {
        Assert.That(DockMath.NearestEdge(new PixelRect(700, 400, 450, 200), Work, 48), Is.Null);
    }

    [Test]
    public void NearestEdge_Corner_PicksTheCloserEdge()
    {
        // 30px from the right, 10px from the top
        Assert.That(DockMath.NearestEdge(new PixelRect(1440, 10, 450, 200), Work, 48), Is.EqualTo(DockEdge.Top));
    }

    [Test]
    public void ExpandedRect_SnapsFlushAndClampsAlongTheEdge()
    {
        var rect = DockMath.ExpandedRect(new PixelRect(1460, 950, 450, 200), Work, DockEdge.Right);
        Assert.That(rect, Is.EqualTo(new PixelRect(1470, 840, 450, 200)));
    }

    [TestCase(DockEdge.Right, 1898, 400)]
    [TestCase(DockEdge.Left, -428, 400)]
    [TestCase(DockEdge.Top, 700, -178)]
    [TestCase(DockEdge.Bottom, 700, 1018)]
    public void CollapsedRect_LeavesOnlyVisiblePxOnScreen(DockEdge edge, int x, int y)
    {
        var rect = DockMath.CollapsedRect(new PixelRect(700, 400, 450, 200), Work, edge, 22);
        Assert.That((rect.X, rect.Y), Is.EqualTo((x, y)));
    }

    [Test]
    public void ClampToWorkArea_OffScreenWindow_IsPulledBackInside()
    {
        var rect = DockMath.ClampToWorkArea(new PixelRect(3000, -500, 450, 200), Work);
        Assert.That(rect, Is.EqualTo(new PixelRect(1470, 0, 450, 200)));
    }

    [Test]
    public void EaseOut_HitsEndpoints()
    {
        Assert.That(DockMath.EaseOut(0, 100, 0), Is.EqualTo(0));
        Assert.That(DockMath.EaseOut(0, 100, 1), Is.EqualTo(100));
        Assert.That(DockMath.EaseOut(0, 100, 5), Is.EqualTo(100));
        Assert.That(DockMath.EaseOut(0, 100, 0.5), Is.GreaterThan(50)); // ease-out front-loads movement
    }
}
