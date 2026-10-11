using System;

namespace TaskTrackerApp;

/// <summary>The running app version (the &lt;Version&gt; in TaskTrackerApp.csproj, kept in sync by scripts/Set-Version.ps1).</summary>
public static class AppVersion
{
    public static Version Current { get; } = typeof(AppVersion).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
}
