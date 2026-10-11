using System;
using System.Windows;

namespace TaskTrackerApp.Onboarding;

/// <summary>Where the tour card goes relative to the spotlighted control. Pure geometry, unit tested.</summary>
public static class CoachLayout
{
    public const double Gap = 12;    // between the target and the card
    public const double Margin = 12; // between the card and the window edge

    /// <summary>
    /// Places the card below the target, else above, right or left of it. A target that fills most of the window
    /// gets the card inside it, near its bottom. With no target the card is centered.
    /// </summary>
    public static Point PlaceCard(Rect? target, Size card, Size area)
    {
        if (target is not Rect t || t.IsEmpty)
            return new Point(Math.Max(Margin, (area.Width - card.Width) / 2), Math.Max(Margin, (area.Height - card.Height) / 2));

        // Wide targets (e.g. Quick add) line up with the card's left edge; narrow ones are centered under the card
        double x = ClampX(t.Width >= card.Width ? t.Left : t.Left + (t.Width - card.Width) / 2);

        if (t.Bottom + Gap + card.Height <= area.Height - Margin) return new Point(x, t.Bottom + Gap);
        if (t.Top - Gap - card.Height >= Margin) return new Point(x, t.Top - Gap - card.Height);
        if (t.Right + Gap + card.Width <= area.Width - Margin) return new Point(t.Right + Gap, ClampY(t.Top));
        if (t.Left - Gap - card.Width >= Margin) return new Point(t.Left - Gap - card.Width, ClampY(t.Top));
        return new Point(x, ClampY(t.Bottom - Gap - card.Height));

        double ClampX(double value) => Clamp(value, Margin, area.Width - card.Width - Margin);
        double ClampY(double value) => Clamp(value, Margin, area.Height - card.Height - Margin);
    }

    private static double Clamp(double value, double min, double max) => max < min ? min : Math.Clamp(value, min, max);
}
