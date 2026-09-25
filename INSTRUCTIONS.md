# Task And Ticket Tracker - User Instructions

Welcome to Task And Ticket Tracker, a lightweight tool that keeps you focused on the tasks you are working on right now. Follow these steps to start using the application.

## 1. Installing / Running the Application

### Option A: Microsoft Store (recommended)
Install **Task And Ticket Tracker** from the Microsoft Store and launch it from the Start menu.

### Option B: Visual Studio
1. Open the solution file `TaskTracker.slnx` in the repository root using Visual Studio 2022 (17.13+) or later.
2. Set `TaskTrackerApp` as the Startup Project.
3. Press **F5** to compile and run the app.

### Option C: Command Line
Requires the .NET 10 SDK. From the repository root:
```powershell
dotnet run --project TaskTrackerApp\TaskTrackerApp.csproj
```

Only one instance of the app can run at a time; launching it again does nothing while it is already running.

## 2. Managing Tasks (Manager Window)

*   **Quick add:** type in the **Add a task** box above the list and press `Enter`. The task is added to the bottom as *To Do*. Start with a ticket number to set it, e.g. `#123 Fix login` or `AB-12 Fix login`. Press `Ctrl+Enter` instead to open the full form with that title.
*   **New Task (full form):** Click **New task** (`Ctrl+N`). The cursor is already in **Title** (required).
    *   **Steps** come right after the title. Press `Enter` to add one, or paste a list to add one step per line.
    *   **Ticket number** shows the number it will get automatically (e.g. `Auto: i18`); type your own to override it. Once the task is saved its ticket number is **fixed**: it becomes the heading of the task and can't be edited.
    *   **Start now** makes the task active immediately (In progress, shown on the widget). Leave it off to queue it as *To Do*.
    *   **Add to Top / Bottom** decides where the task goes in the priority list.
    *   **Due date:** use *Today*, *Tomorrow* or *Next week*, or click the date to pick one. Add a time only if it matters; without one the task is due all day.
    *   Click **Save task** (`Ctrl+S`), or **Save & new** (`Ctrl+Enter`) to go straight on to the next task.
*   **Unsaved changes:** if you leave a form with edits (another task, New task, Cancel or `Esc`), the app asks whether to keep editing, discard or save.
*   **Reading the list:** each row shows its priority number, then the **ticket number in accent colour followed by the title** on the main line. Under the title you see the **due date** (amber = due today, red = overdue) and **step progress** (e.g. `2/5`). Active tasks have a coloured bar on the left. When the details panel is open and space is tight, the state shows as a coloured dot.
*   **Edit a Task:** Click a task in the list to open its details. Use the expand button to open the details full-width. `Esc` closes the details without saving.
*   **Details:** Each task has a ticket number, title, state (*To Do*, *In progress*, *Done*), target date and time, description, acceptance criteria and **Steps**.
*   **Steps:** Break a ticket into sub-task steps. Type a step and press **Enter** to add it, drag the handle to reorder, and use the checkbox to mark it done. Step changes are kept only when you click **Save Task**.
*   **Prioritize:** Drag a task row up or down in the list. Priority numbers update automatically (1 = highest).
*   **Filter:** Search by title or ticket number (`Ctrl+F`), use **States** to show or hide states, or tick **Active only**. If nothing matches, click **Show all tasks**.
*   **Delete:** Open a task and click **Delete**, or select a row and press `Del`. The task is removed immediately and a bar at the bottom offers **Undo** for a few seconds.

## 3. Active Tasks

*   Click the **☆ star** on a task to make it your current focus. Active tasks show a filled ★, a tinted card, and are always *In progress*.
*   You can have up to **2** active tasks by default (change this in **Settings → Active tasks at a time**).
*   If no task is active, the highest-priority unfinished task is activated automatically.

## 4. The Widget

Minimizing the manager hides it and shows your active tasks in the **widget**. Choose how it looks in **Settings → Desktop Widget → Widget Style**:

| Style | What it does |
|---|---|
| **Floating** (default) | A small window that stays on top of other windows. Drag it anywhere. |
| **Edge-docked (auto-hide)** | Drag the widget to the left, right, top or bottom edge of the screen. It hides as a thin coloured tab showing the ticket number, slides open when you hover over the tab, and slides away when the mouse leaves. Drag it away from the edge to undock it. |
| **Taskbar strip** *(experimental)* | A one-line strip on the taskbar next to the clock. It shows `#ticket · current step`, or `#ticket · task name` if the task has no open steps. Hover for ✓ (complete the step or task) and ‹ › (switch active task, or use the mouse wheel). Click the strip to open the full widget above the taskbar. |

Clicking the widget never takes the keyboard focus away from the app you are typing in.

**Taskbar strip notes:** Windows doesn't officially let apps draw inside the taskbar, so the strip is a small window placed over it. It hides while the taskbar is auto-hidden or a fullscreen app (video, game, presentation) is active. On a very crowded taskbar it may cover some icons. It needs a horizontal taskbar; with a vertical one, the floating widget is used instead.

**Full widget controls** (Floating, Edge-docked, and the taskbar strip's pop-up):

*   **Move:** Drag the widget by its border or the `⋮⋮` handle.
*   **Resize:** Drag the bottom-right corner.
*   **Switch tasks / steps:** Use the `‹` `›` arrows (shown when there is more than one).
*   **Tick a step:** Use the step checkbox. The widget jumps to the next unfinished step.
*   **Buttons (shown on hover):** `✓` mark the task done · `↻` reset to the top-priority task · `→` skip to the next task.
*   **Open the task:** Click the `#ticket` badge. `⛶` opens the manager and `✕` hides the widget.

## 5. The System Tray

The app keeps running in the Windows System Tray (the icons next to the clock).

*   **Double-click** the tray icon to open the manager.
*   **Right-click** the tray icon for:
    *   **Show Manager:** opens the main window.
    *   **Reset Widget Position:** undocks the widget and moves it back to the middle of the screen.
    *   **Exit:** closes the application completely.

*   **Minimize (—)** hides the manager and shows the widget.
*   **Close (✕ or `Alt+F4`)** hides the manager straight to the tray **without** the widget; reminders keep working. Double-click the tray icon to reopen it.
*   To quit, right-click the tray icon and choose **Exit**.

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` | New task |
| `Ctrl+S` | Save the open task |
| `Ctrl+Enter` | Save and start another new task (in the form) · open the full form (in Quick add) |
| `Esc` | Close the task details without saving |
| `Ctrl+F` | Search |
| `Del` | Delete the selected task (with Undo) |
| `F1` | Open the in-app user guide (also: About → Open user guide) |

## 6. Reminders & Do Not Disturb

*   Tasks with a target date trigger a reminder the configured number of hours before they are due (**Settings → Remind me**, default 4 hours before). You can turn reminders off in Settings.
*   **Privacy while presenting:** When Windows notifications are turned off (Do Not Disturb), reminders hide your task details and only show *"Background Task Active"*, so nothing sensitive appears if you are sharing your screen.

## 7. Settings & Themes

Open **Settings** at the bottom of the navigation pane to change the active-task limit, reminders, the app theme (Use system setting / Dark / Light), and the widget style, theme, text size, bold text and opacity. **Changes apply and save immediately** — there is no Save button.

*   The **sun/moon** button in the title bar quickly switches between Dark and Light.
*   The app uses your **Windows accent colour** and, on Windows 11, the **Mica** window material. Change the accent in Windows Settings → Personalization → Colors; the app updates straight away.
*   The navigation pane starts **collapsed to icons**. Click ☰ to expand it and show the labels.

## 8. Your Data

All tasks and settings are stored locally in `%LOCALAPPDATA%\TaskTrackerApp\` (`tasks.json`, `settings.json`). Nothing is sent over the network.

*   Every save keeps the previous version as `tasks.json.bak`.
*   If `tasks.json` ever becomes unreadable, the app moves it aside as `tasks.corrupt-<timestamp>.json`, restores the last backup and tells you what happened. Your data is never silently overwritten.
