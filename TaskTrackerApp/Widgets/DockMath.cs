using System;

namespace TaskTrackerApp.Widgets;

public enum DockEdge
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>Integer rectangle in physical pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Intersects(PixelRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public bool Contains(PixelRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
}

/// <summary>Pure geometry for edge docking. No Win32 calls, so it is unit-testable.</summary>
public static class DockMath
{
    /// <summary>
    /// Returns the work-area edge the window is closest to, if that distance is within <paramref name="thresholdPx"/>.
    /// A window pushed past an edge counts as distance 0.
    /// </summary>
    public static DockEdge? NearestEdge(PixelRect window, PixelRect workArea, int thresholdPx)
    {
        var distances = new (DockEdge Edge, int Distance)[]
        {
            (DockEdge.Left, Math.Max(0, window.X - workArea.X)),
            (DockEdge.Right, Math.Max(0, workArea.Right - window.Right)),
            (DockEdge.Top, Math.Max(0, window.Y - workArea.Y)),
            (DockEdge.Bottom, Math.Max(0, workArea.Bottom - window.Bottom)),
        };

        DockEdge? best = null;
        int bestDistance = int.MaxValue;
        foreach (var (edge, distance) in distances)
        {
            if (distance <= thresholdPx && distance < bestDistance)
            {
                best = edge;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Position of the fully visible window flush against <paramref name="edge"/>, clamped along the edge.</summary>
    public static PixelRect ExpandedRect(PixelRect window, PixelRect workArea, DockEdge edge)
    {
        var clamped = ClampToWorkArea(window, workArea);
        return edge switch
        {
            DockEdge.Left => clamped with { X = workArea.X },
            DockEdge.Right => clamped with { X = workArea.Right - window.Width },
            DockEdge.Top => clamped with { Y = workArea.Y },
            _ => clamped with { Y = workArea.Bottom - window.Height },
        };
    }

    /// <summary>Position where only <paramref name="visiblePx"/> of the window remains inside the work area.</summary>
    public static PixelRect CollapsedRect(PixelRect window, PixelRect workArea, DockEdge edge, int visiblePx)
    {
        var expanded = ExpandedRect(window, workArea, edge);
        return edge switch
        {
            DockEdge.Left => expanded with { X = workArea.X - window.Width + visiblePx },
            DockEdge.Right => expanded with { X = workArea.Right - visiblePx },
            DockEdge.Top => expanded with { Y = workArea.Y - window.Height + visiblePx },
            _ => expanded with { Y = workArea.Bottom - visiblePx },
        };
    }

    /// <summary>Moves (never resizes, unless larger than the area) the window so it lies fully inside the work area.</summary>
    public static PixelRect ClampToWorkArea(PixelRect window, PixelRect workArea)
    {
        int x = Math.Clamp(window.X, workArea.X, Math.Max(workArea.X, workArea.Right - window.Width));
        int y = Math.Clamp(window.Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - window.Height));
        return window with { X = x, Y = y };
    }

    /// <summary>A point one pixel beyond the middle of the given edge; used to test whether another monitor is there.</summary>
    public static (int X, int Y) PointBeyondEdge(PixelRect window, PixelRect workArea, DockEdge edge)
    {
        int midX = window.X + window.Width / 2;
        int midY = window.Y + window.Height / 2;
        return edge switch
        {
            DockEdge.Left => (workArea.X - 1, midY),
            DockEdge.Right => (workArea.Right, midY),
            DockEdge.Top => (midX, workArea.Y - 1),
            _ => (midX, workArea.Bottom),
        };
    }

    /// <summary>Ease-out cubic interpolation between two integers, t in [0,1].</summary>
    public static int EaseOut(int from, int to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3);
        return (int)Math.Round(from + (to - from) * eased);
    }
}
