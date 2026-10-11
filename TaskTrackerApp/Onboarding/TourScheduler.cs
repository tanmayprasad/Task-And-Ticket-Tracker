using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskTrackerApp.Onboarding;

public enum TourKind { None, Welcome, WhatsNew }

/// <summary>Which tour to show at startup, and the releases whose "What's new" steps belong in it.</summary>
public sealed record TourDecision(TourKind Kind, bool IsReturningUser, IReadOnlyList<Version> Releases)
{
    public static readonly TourDecision None = new(TourKind.None, false, Array.Empty<Version>());
}

/// <summary>A release that has "What's new" steps. <paramref name="CoveredByWelcome"/>: the welcome tour already shows it.</summary>
public readonly record struct ReleaseNoteInfo(Version Version, bool CoveredByWelcome);

/// <summary>
/// Decides which tour runs at startup. Pure logic (no WPF) so it can be unit tested.
/// Everyone sees the welcome tour once, including existing users updating to the first version that has it;
/// after that, each update shows the "What's new" steps of the releases since the version last seen.
/// </summary>
public static class TourScheduler
{
    /// <summary>The last release without tours: users updating from it have no LastSeenVersion yet.</summary>
    public static readonly Version Baseline = new(1, 5, 0, 0);

    public static TourDecision Decide(bool onboardingCompleted, bool isExistingUser, string? lastSeenVersion,
                                      Version current, IEnumerable<ReleaseNoteInfo> releases)
    {
        var lastSeen = Parse(lastSeenVersion);

        if (!onboardingCompleted)
        {
            // New users have no history, so release notes would only repeat the tour.
            // Existing users also get what changed since their version, minus what the tour already shows.
            var extra = isExistingUser ? Pending(releases, lastSeen, current, skipCoveredByWelcome: true) : Array.Empty<Version>();
            return new TourDecision(TourKind.Welcome, isExistingUser, extra);
        }

        if (current <= lastSeen) return TourDecision.None;
        var pending = Pending(releases, lastSeen, current, skipCoveredByWelcome: false);
        return pending.Count == 0 ? TourDecision.None : new TourDecision(TourKind.WhatsNew, true, pending);
    }

    /// <summary>The version to store as LastSeenVersion; never goes backwards, so a downgrade doesn't replay old notes later.</summary>
    public static string VersionToRecord(string? lastSeenVersion, Version current)
    {
        var lastSeen = Parse(lastSeenVersion);
        return (current > lastSeen ? current : lastSeen).ToString();
    }

    private static Version Parse(string? version) => Version.TryParse(version, out var v) ? v : Baseline;

    private static IReadOnlyList<Version> Pending(IEnumerable<ReleaseNoteInfo> releases, Version lastSeen, Version current, bool skipCoveredByWelcome) =>
        releases.Where(r => r.Version > lastSeen && r.Version <= current && !(skipCoveredByWelcome && r.CoveredByWelcome))
                .Select(r => r.Version)
                .OrderBy(v => v)
                .ToList();
}
