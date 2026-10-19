using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Onboarding;

/// <summary>
/// Tour content. The welcome tour teaches the app once (new users, and existing users on their first update to a
/// version with tours). Each release adds its "What's new" steps to <see cref="Releases"/> next to its CHANGELOG entry.
/// </summary>
internal static class Tours
{
    public const string PracticeTicket = "START";

    // ------------------------------------------------------------------ What's new
    // Key each entry by the first build that ships the feature (a development build is fine): users see it when they
    // update past that version. scripts/Test-ReleaseVersion.ps1 warns when a Store release has no entry.
    public static readonly IReadOnlyList<WhatsNewRelease> Releases =
    [
        Release("1.5.0.1", coveredByWelcome: true,
            new CoachStep
            {
                Title = "Help, one click away",
                Body = "The new ? button replays the getting-started tour, shows what's new after each update, and opens the user guide.",
                Target = w => Bounds(w, w.HelpButton),
            }),
        Release("1.5.0.2", coveredByWelcome: true,
            ProfilesStep,
            new CoachStep
            {
                Title = "Import and export your tasks",
                Body = "The Profiles page exports a profile's tasks as a JSON file (everything, for backups or another PC) or a CSV spreadsheet for Excel, and imports them into any profile. You can undo an import.",
                Target = w => Bounds(w, w.NavProfiles),
            }),
        Release("1.6.0.0", coveredByWelcome: false,
            new CoachStep
            {
                Title = "Start with Windows",
                Body = "Turn on Settings → Start with Windows and the app starts when you sign in, straight to the desktop widget, so reminders never depend on you opening it.",
                Target = w => Bounds(w, w.NavSettings),
            }),
        Release("1.7.0.0", coveredByWelcome: false,
            new CoachStep
            {
                Title = "History of what you finished",
                Body = "The new History page lists the tasks you finished and the steps you ticked today, yesterday and the three days before. Click a row to open that task.",
                Target = w => Bounds(w, w.NavDoneLog),
            }),
    ];

    private static CoachStep ProfilesStep => new()
    {
        Title = "Separate lists with profiles",
        Body = "Keep work and personal tasks apart. Each profile has its own tasks, active-task limit and reminders; reminders arrive for every profile. Click here to open Profiles: switch, create, import and export.",
        Target = w => Bounds(w, w.NavProfiles),
    };

    private static WhatsNewRelease Release(string version, bool coveredByWelcome, params CoachStep[] steps) =>
        new(version, coveredByWelcome, steps);

    /// <summary>A "What's new" tour: an intro card, then the steps of the given releases, oldest first.</summary>
    public static IReadOnlyList<CoachStep> WhatsNew(IEnumerable<Version> versions)
    {
        var steps = WhatsNewSteps(versions);
        if (steps.Count == 0) return steps;

        var intro = new CoachStep
        {
            Title = $"What's new in version {AppVersion.Current}",
            Body = "A quick look at what changed since you last used the app.",
            IsNew = true,
            NextText = "Show me",
            SkipText = "Not now",
            Prepare = w => w.ShowTasksTab(),
        };
        return [intro, .. steps];
    }

    public static IReadOnlyList<CoachStep> WhatsNewSteps(IEnumerable<Version> versions)
    {
        var wanted = versions.ToHashSet();
        return Releases.Where(r => wanted.Contains(r.Version))
                       .OrderBy(r => r.Version)
                       .SelectMany(r => r.Steps)
                       .Select(s => s with { IsNew = true })
                       .ToList();
    }

    // ------------------------------------------------------------------ Welcome
    /// <param name="returningUser">Existing user seeing the tour after an update (different first card).</param>
    /// <param name="extra">"What's new" steps to show before the final card.</param>
    public static IReadOnlyList<CoachStep> Welcome(MainWindow window, bool returningUser, int maxActiveTasks, IEnumerable<CoachStep> extra)
    {
        var steps = new List<CoachStep>
        {
            new()
            {
                Title = returningUser ? "Welcome back! Here's how it all works" : "Welcome to Task And Ticket Tracker",
                Body = returningUser
                    ? "This version adds a quick tour. It takes about a minute and shows the parts of the app that are easy to miss."
                    : "Keep your tickets moving in three moves. This quick tour shows where everything is; it takes about a minute.",
                Highlights =
                [
                    new("", "Capture", "Add tasks and steps in seconds"),
                    new("", "Focus", "Star the tasks you're working on"),
                    new("", "Glance", "See your next step on the desktop"),
                ],
                NextText = "Take the tour",
                SkipText = "Not now",
                Prepare = w => w.ShowTasksTab(),
            },
            new()
            {
                Title = "Capture a task in seconds",
                Body = "Type a title here and press Enter. Start with a ticket number, like \"#123 Fix login\", to file it under that ticket. Ctrl+Enter opens the full form instead.",
                Target = w => Bounds(w, w.QuickAddBox),
                Prepare = w => w.ShowTasksTab(),
            },
            new()
            {
                Title = "Need more detail?",
                Body = "New task (Ctrl+N) opens the full form: steps, due date, description and acceptance criteria. Save & new (Ctrl+Enter) saves and starts the next one.",
                Target = w => Bounds(w, w.NewTaskButton),
            },
            new()
            {
                Title = "Your list, in priority order",
                Body = $"Click ★ on a task to make it active: that's what you're working on now. Up to {maxActiveTasks} tasks can be active at once (you can change this in Settings). Drag a card up or down to change its priority.",
                Target = w => w.TasksDataGrid.Items.Count > 0 ? Bounds(w, w.TasksDataGrid, maxHeight: 150) : Bounds(w, w.EmptyStatePanel),
            },
            new()
            {
                Title = "Find anything",
                Body = "Search by ticket or title (Ctrl+F), filter by state, or show only active tasks. Done tasks stay hidden until you tick Done under States.",
                Target = w => Bounds(w, w.FiltersRow),
            },
            new()
            {
                Title = "Minimize shows your widget",
                Body = "— hides this window and puts your active task and its current step on the desktop, so you always know what's next.\n\n" +
                       "The widget comes in three styles: a floating card, a tab docked to a screen edge, or a strip on the taskbar. Pick one in Settings → Desktop widget → Widget style.\n\n" +
                       "✕ doesn't quit: the app keeps running in the tray so reminders still arrive. To quit, right-click the tray icon and choose Exit.",
                Target = CaptionButtons,
            },
            ProfilesStep,
            new()
            {
                Title = "Make it yours",
                Body = "In Settings, choose the widget style, its theme and text size, and the app theme. The active-task limit and reminders are per profile, on the Profiles page.",
                Target = w => Bounds(w, w.NavSettings),
            },
        };

        steps.AddRange(extra);

        bool hasPracticeTask = window.HasPracticeTask;
        steps.Add(new CoachStep
        {
            Title = "You're all set",
            Body = "Replay this tour, see what's new after an update, or open the full user guide (F1) from the ? button at any time.",
            Target = w => Bounds(w, w.HelpButton),
            NextText = "Done",
            ExtraText = hasPracticeTask ? null : "Add a practice task",
            ExtraAction = hasPracticeTask ? null : w => w.AddPracticeTask(),
        });
        return steps;
    }

    /// <summary>A task whose steps walk the user through the core loop: star → minimize → tick a step from the widget.</summary>
    public static TaskModel CreatePracticeTask() => new()
    {
        VstsNumber = PracticeTicket,
        Title = "Try the tracker",
        Description = "A practice task from the getting-started tour. Work through its steps, then delete it.",
        State = "To Do",
        Steps =
        [
            new TicketStep { Description = "Click ★ on this task in the list to make it active" },
            new TicketStep { Description = "Minimize the window (—) to see this task on the widget" },
            new TicketStep { Description = "Tick this step from the widget" },
            new TicketStep { Description = "Delete this task when you're done (Del, and you can Undo)" },
        ],
    };

    // ------------------------------------------------------------------ Targets

    /// <summary>The control's bounds in window coordinates (optionally only its top part), or null when it isn't shown.</summary>
    private static Rect? Bounds(Window window, FrameworkElement? element, double maxHeight = double.PositiveInfinity)
    {
        if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return null;
        if (!element.IsDescendantOf(window)) return null;
        var size = new Rect(0, 0, element.ActualWidth, Math.Min(element.ActualHeight, maxHeight));
        return element.TransformToAncestor(window).TransformBounds(size);
    }

    /// <summary>The app's own minimize / maximize / close buttons.</summary>
    private static Rect? CaptionButtons(MainWindow window) => Bounds(window, window.CaptionButtonsPanel);
}
