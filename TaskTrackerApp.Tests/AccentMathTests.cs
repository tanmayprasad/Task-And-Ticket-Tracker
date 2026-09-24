using System.Windows.Media;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp.Tests;

public class AccentMathTests
{
    // A typical Windows blue accent palette
    private static readonly AccentPalette Blue = new(
        Accent: Color.FromRgb(0x00, 0x78, 0xD4),
        Light1: Color.FromRgb(0x42, 0x9C, 0xE3),
        Light2: Color.FromRgb(0x76, 0xB9, 0xED),
        Light3: Color.FromRgb(0x99, 0xEB, 0xFF),
        Dark1: Color.FromRgb(0x00, 0x5A, 0x9E),
        Dark2: Color.FromRgb(0x00, 0x42, 0x75));

    [Test]
    public void DarkTheme_UsesLightShadeForFill_LightTheme_UsesDarkShade()
    {
        Assert.That(AccentMath.ForTheme(Blue, dark: true).Fill, Is.EqualTo(Blue.Light2));
        Assert.That(AccentMath.ForTheme(Blue, dark: false).Fill, Is.EqualTo(Blue.Dark1));
    }

    [Test]
    public void TextOnAccent_PicksTheMoreReadableColour()
    {
        Assert.That(AccentMath.ForTheme(Blue, dark: true).OnAccent, Is.EqualTo(Colors.Black));   // light blue fill
        Assert.That(AccentMath.ForTheme(Blue, dark: false).OnAccent, Is.EqualTo(Colors.White));  // dark blue fill
        Assert.That(AccentMath.BestTextOn(Color.FromRgb(0xFF, 0xD7, 0x00)), Is.EqualTo(Colors.Black)); // yellow
        Assert.That(AccentMath.BestTextOn(Color.FromRgb(0x5B, 0x21, 0xB6)), Is.EqualTo(Colors.White)); // purple
    }

    [Test]
    public void TextOnAccent_MeetsWcagAA_ForTypicalAccents()
    {
        foreach (var dark in new[] { true, false })
        {
            var c = AccentMath.ForTheme(Blue, dark);
            Assert.That(AccentMath.ContrastRatio(c.Fill, c.OnAccent), Is.GreaterThanOrEqualTo(4.5), $"dark={dark}");
        }
    }

    [Test]
    public void RowTint_IsTranslucentAccent()
    {
        var c = AccentMath.ForTheme(Blue, dark: true);
        Assert.That(c.RowTint.A, Is.LessThan(0x40));
        Assert.That((c.RowTint.R, c.RowTint.G, c.RowTint.B), Is.EqualTo((c.Fill.R, c.Fill.G, c.Fill.B)));
    }

    [Test]
    public void ContrastRatio_BlackOnWhiteIs21()
    {
        Assert.That(AccentMath.ContrastRatio(Colors.Black, Colors.White), Is.EqualTo(21).Within(0.01));
    }
}
