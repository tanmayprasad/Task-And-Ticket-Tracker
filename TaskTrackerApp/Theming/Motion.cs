using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TaskTrackerApp.Theming;

/// <summary>
/// Small, cheap entrance animations (translate, plus opacity for small surfaces only). Animations don't hold their values after finishing,
/// so nothing keeps running, and they are skipped entirely when Windows animations are turned off.
/// </summary>
public static class Motion
{
    private static readonly IEasingFunction EaseOut = CreateEase();

    private static IEasingFunction CreateEase()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();
        return ease;
    }

    /// <summary>True when the user has Windows "Animation effects" turned on.</summary>
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>
    /// Slides the element from (fromX, fromY) to its resting position, optionally fading it in.
    /// Only use <paramref name="fade"/> on small surfaces: animating the opacity of a large subtree makes WPF render
    /// it through an offscreen layer, which costs GPU-driver memory. Translating needs no extra layer.
    /// </summary>
    public static void FadeSlideIn(UIElement element, double fromX = 0, double fromY = 0, int milliseconds = 150, bool fade = false)
    {
        if (!Enabled || element == null) return;

        var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
        if (fade)
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = EaseOut, FillBehavior = FillBehavior.Stop });

        if (fromX == 0 && fromY == 0) return;
        if (element.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }
        if (fromX != 0)
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(fromX, 0, duration) { EasingFunction = EaseOut, FillBehavior = FillBehavior.Stop });
        if (fromY != 0)
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(fromY, 0, duration) { EasingFunction = EaseOut, FillBehavior = FillBehavior.Stop });
    }
}
