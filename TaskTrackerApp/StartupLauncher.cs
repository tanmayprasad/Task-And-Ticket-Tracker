using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TaskTrackerApp;

public enum StartupState
{
    Off,
    On,
    /// <summary>Switched off in Windows (Task Manager or Settings → Apps → Startup); only the user can switch it back on there.</summary>
    BlockedByUser,
    BlockedByPolicy,
}

/// <summary>
/// "Start with Windows". The Store (MSIX) install uses the package's start-up task (AppxManifest.xml,
/// <see cref="TaskId"/>); a dev build or the legacy installer uses the per-user Run key. Windows is the source of
/// truth, so nothing is kept in settings.json. Failures never reach the caller: the state is simply Off.
/// </summary>
public static class StartupLauncher
{
    public const string TaskId = "TaskTrackerStartup"; // must match AppxManifest.xml
    public const string StartupArgument = "--startup";

    private const string RunValueName = "TaskAndTicketTracker";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// <summary>True when running from the installed MSIX package.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True when Windows started the app at sign-in rather than the user.</summary>
    public static bool IsStartupLaunch(string[] args)
    {
        if (HasStartupArgument(args)) return true;
        if (!IsPackaged) return false;
        try
        {
            return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind
                   == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<StartupState> GetStateAsync()
    {
        try
        {
            if (!IsPackaged) return GetRunKeyState();
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch
        {
            return StartupState.Off;
        }
    }

    /// <summary>Switches start-up on or off and returns the resulting state (which can be Blocked instead of On).</summary>
    public static async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        try
        {
            if (!IsPackaged)
            {
                SetRunKey(enabled);
                return GetRunKeyState();
            }

            var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                task.Disable();
                return Map(task.State);
            }
            return Map(await task.RequestEnableAsync());
        }
        catch
        {
            return StartupState.Off;
        }
    }

    private static StartupState Map(Windows.ApplicationModel.StartupTaskState state) => state switch
    {
        Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => StartupState.On,
        Windows.ApplicationModel.StartupTaskState.DisabledByUser => StartupState.BlockedByUser,
        Windows.ApplicationModel.StartupTaskState.DisabledByPolicy => StartupState.BlockedByPolicy,
        _ => StartupState.Off,
    };

    // ------------------------------------------------------------------ Run key (not packaged)

    private static StartupState GetRunKeyState()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null) return StartupState.Off;

        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        if (!RunCommandMatches(run?.GetValue(RunValueName) as string, exePath)) return StartupState.Off;

        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
        return IsApprovedEntryDisabled(approved?.GetValue(RunValueName) as byte[]) ? StartupState.BlockedByUser : StartupState.On;
    }

    private static void SetRunKey(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            if (Environment.ProcessPath is string exePath) run.SetValue(RunValueName, BuildRunCommand(exePath));
            return;
        }

        run.DeleteValue(RunValueName, throwOnMissingValue: false);
        // Also forget a "disabled in Task Manager" mark, so switching it on again later works
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approved?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    // ------------------------------------------------------------------ Pure helpers (unit-tested)

    public static bool HasStartupArgument(string[] args) =>
        args.Any(a => string.Equals(a, StartupArgument, StringComparison.OrdinalIgnoreCase));

    public static string BuildRunCommand(string exePath) => $"\"{exePath}\" {StartupArgument}";

    /// <summary>True when a Run value starts this exe (an entry left by another copy of the app doesn't count).</summary>
    public static bool RunCommandMatches(string? runValue, string exePath) =>
        string.Equals(runValue?.Trim(), BuildRunCommand(exePath), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads Explorer's StartupApproved entry: the first byte is even (02, 06) when enabled and odd (03, 07) when the
    /// user disabled the entry in Task Manager. No entry means enabled.
    /// </summary>
    public static bool IsApprovedEntryDisabled(byte[]? approvedValue) =>
        approvedValue is { Length: > 0 } && (approvedValue[0] & 1) == 1;
}
