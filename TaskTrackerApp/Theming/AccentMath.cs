using System;
using System.Windows.Media;

namespace TaskTrackerApp.Theming;

/// <summary>The Windows accent colour and its lighter/darker shades (as reported by UISettings).</summary>
public readonly record struct AccentPalette(Color Accent, Color Light1, Color Light2, Color Light3, Color Dark1, Color Dark2);

/// <summary>Accent brushes for one theme.</summary>
public readonly record struct AccentColors(Color Fill, Color Hover, Color Text, Color OnAccent, Color RowTint);

/// <summary>Pure colour rules for the accent (Windows 11 conventions). Unit-tested.</summary>
public static class AccentMath
{
    /// <summary>
    /// Windows 11 uses a light shade of the accent on dark backgrounds and a dark shade on light ones,
    /// so accent fills and accent text stay readable in both themes.
    /// </summary>
    public static AccentColors ForTheme(AccentPalette p, bool dark)
    {
        var fill = dark ? p.Light2 : p.Dark1;
        var hover = dark ? p.Light1 : p.Accent;
        var text = dark ? p.Light3 : p.Dark2;
        var rowTint = Color.FromArgb(dark ? (byte)0x24 : (byte)0x18, fill.R, fill.G, fill.B);
        return new AccentColors(fill, hover, text, BestTextOn(fill), rowTint);
    }

    /// <summary>Black or white, whichever has the higher WCAG contrast against <paramref name="background"/>.</summary>
    public static Color BestTextOn(Color background)
    {
        double l = RelativeLuminance(background);
        double contrastWithBlack = (l + 0.05) / 0.05;
        double contrastWithWhite = 1.05 / (l + 0.05);
        return contrastWithBlack >= contrastWithWhite ? Colors.Black : Colors.White;
    }

    public static double ContrastRatio(Color a, Color b)
    {
        double la = RelativeLuminance(a), lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static double RelativeLuminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
