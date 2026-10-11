using System;
using System.Collections.Generic;
using System.Windows;

namespace TaskTrackerApp.Onboarding;

/// <summary>An icon + label + caption tile shown on a centered card (e.g. the welcome card's Capture · Focus · Glance).</summary>
public sealed record CoachHighlight(string Glyph, string Label, string Caption);

/// <summary>One card of a tour (see Tours.cs).</summary>
public sealed record CoachStep
{
    public required string Title { get; init; }
    public required string Body { get; init; }

    /// <summary>The area to spotlight, in MainWindow coordinates. Null (or a hidden control) shows a centered card.</summary>
    public Func<MainWindow, Rect?>? Target { get; init; }

    /// <summary>Runs before the step is shown, e.g. to switch to the Tasks page.</summary>
    public Action<MainWindow>? Prepare { get; init; }

    /// <summary>Shows a NEW pill (set for every "What's new" step).</summary>
    public bool IsNew { get; init; }

    public IReadOnlyList<CoachHighlight>? Highlights { get; init; }

    public string? NextText { get; init; }   // default: Next, or Done on the last step
    public string? SkipText { get; init; }   // default: Skip tour

    /// <summary>An optional extra button; it ends the tour and then runs <see cref="ExtraAction"/>.</summary>
    public string? ExtraText { get; init; }
    public Action<MainWindow>? ExtraAction { get; init; }
}

/// <summary>The "What's new" steps of one release.</summary>
public sealed class WhatsNewRelease
{
    public WhatsNewRelease(string version, bool coveredByWelcome, IReadOnlyList<CoachStep> steps)
    {
        Version = Version.Parse(version);
        CoveredByWelcome = coveredByWelcome;
        Steps = steps;
    }

    public Version Version { get; }

    /// <summary>True when the welcome tour already shows this, so users who just took it don't see it twice.</summary>
    public bool CoveredByWelcome { get; }

    public IReadOnlyList<CoachStep> Steps { get; }

    public ReleaseNoteInfo Info => new(Version, CoveredByWelcome);
}
