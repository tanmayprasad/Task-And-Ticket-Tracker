# Changelog

All notable changes to the **Task and Ticket Tracker** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to the specific versioning rules defined in `PACKAGING_CONFIG.md`.

## [1.5.0.0] - 2026-09-25

### Store release notes
- A fresh Windows 11 look: Mica window, your Windows accent colour, a navigation pane, and tasks shown as cards.
- Choose how your active task is shown: a floating widget, a tab docked to the screen edge, or a strip on the taskbar.
- Add tasks in seconds with Quick add, Save & new, "Start now" and Today / Tomorrow / Next week due dates.
- Paste a list to create steps, and see due dates and step progress right in the task list.
- Delete with Undo, a warning before unsaved changes are lost, and keyboard shortcuts. Press F1 for the new user guide.
- The widget no longer takes keyboard focus, and closing the window keeps the app running in the tray.
- Your tasks are saved more safely with automatic backups, and the app no longer uses the network.

### Changed — modernized UI (Windows 11 Fluent)
- **Window & navigation:**
  - Mica backdrop, rounded window corners, and a title bar that follows the theme. Native Windows caption buttons are used on Windows 11, which adds Snap Layouts on hover.
  - A Windows 11-style **navigation pane** (icons + labels with an accent selection pill, auto-compact on narrow windows) with Tasks, About and Settings.
  - A new **maximize** button, and the theme toggle has moved to the title bar.
- **Accent colour:** the app uses the **Windows accent colour** and updates live when you change it, or when the Windows light/dark mode changes while the app theme is *Use system setting*.
- **Task list as cards:**
  - Ticket, due date and step progress are shown as chips; an overdue due date turns red and a due-today one amber.
  - Active tasks get a ★ star toggle and an accent-tinted card.
- **Controls:** Fluent buttons, text boxes (with an accent focus underline), combo boxes, toggle switches, check boxes, sliders, thin scrollbars, tooltips and the tray menu. Page titles use a consistent type ramp.
- **Settings redesigned** as Windows 11 Settings-style rows:
  - Grouped into Focus, Reminders, Appearance and Desktop widget, and **applied instantly**; there is no Save/Discard any more.
  - Dropdowns and sliders replace free-text numbers, so invalid values are impossible.
  - Sliders preview on the widget while you drag and save once when you release.
- **Notifications:** the notification bar and the unsaved-changes bar are Fluent InfoBars.
- **Widget and taskbar strip:** Windows 11 neutral colours with your accent for the ticket badge and dock tab.
- **User guide window:** Mica and the shared styles.
- **Motion:** light, slide-only entrances for pages and the details pane; the notification bar fades in. All motion respects the Windows "Animation effects" setting.
- **Performance:**
  - All control styles are defined once (`Themes/Controls.xaml`) and theme brushes are frozen.
  - The list uses recycling virtualization, and there are no per-row effects.
  - Memory was measured against the previous build: private bytes are +2 MB at idle and 3 MB *lower* after using details and settings, with 0% idle CPU. Whole-page fades were deliberately avoided: they cost about 50 MB of GPU-driver memory in testing.

### Fixed
- The check box and toggle-switch "on" colour referenced an undefined theme brush (`PrimaryColor`).
- **Typed text was indented twice** in every text box (the placeholder and caret sat about 13 px left of the typed text). Placeholders are now drawn inside the text box itself, so they start exactly where typing starts.
- **Title bar alignment:** the theme button, and the app icon and title, now line up with the native minimize / maximize / close buttons at any display scaling and when maximized.
- Multi-line boxes (Description, Acceptance criteria) start at the top instead of centring their text.
- The full-width new-task form no longer cuts off the *Top / Bottom* control; Start now, Add to, State and Priority stack vertically.
- Consistent page margins and title position on Tasks, Settings, About and the full-width form.
- The navigation pane now starts **collapsed** (icons only); ☰ expands it. The automatic width-based expanding was removed.
- In the task list the **ticket number is primary information**, shown in accent colour before the title on the main line (it was a small tag under the title).
- Editing a task shows its **ticket number as the heading** and the ticket can no longer be changed after the task is saved. New tasks can still type their own number or use the automatic one.
- The task form always opens scrolled to the top, and ticket / due / step chips in narrow cards wrap instead of being clipped.

### Added
- **New in-app user guide** (About → *Open user guide*, or **F1** anywhere in the app). It is organised in cards: Quick start, Adding tasks, The task list, Staying focused, The desktop widget, Keyboard shortcuts, Minimize/close/tray, Reminders, Settings, and Your data. It is themed like the rest of the app.
- **Microsoft Store release pipeline** (`.github/workflows/release-store.yml`):
  - Pushing a `v*.*.*.0` tag checks that the version is consistent and Store-valid, runs the tests, and builds the `.msixupload`.
  - After approval in the `microsoft-store` environment, it uploads the package, sets the Store's "What's new" text from `CHANGELOG.md`, submits for certification, and creates a GitHub release.
- **Release scripts:**
  - `scripts/Set-Version.ps1` updates every version location, and `-Release` finalizes the changelog.
  - `scripts/Test-ReleaseVersion.ps1` validates a release.
  - `scripts/Get-ReleaseNotes.ps1` builds the Store and GitHub notes from the changelog.

### Changed
- About page: updated feature list, and an "Open user guide" button replaces "View Documentation".
- The CI build (`build-msix.yml`) now also runs the unit tests.

## [1.4.3.6] - Development build
> Development build: in-app user guide (F1), About page refresh, Microsoft Store release pipeline and release scripts.

## [1.4.3.5] - Development build

### Added — faster task creation
- **Quick add** box above the list.
  - Type a title and press **Enter** to add it instantly, or **Ctrl+Enter** to continue in the full form.
  - Start with `#123` or a key like `AB-12` to set the ticket number. Short numbers such as "3 bugs to fix" stay in the title.
- **Save & new** (**Ctrl+Enter** in the form) saves and immediately opens an empty form for the next task.
- **Start now** toggle for new tasks, replacing the State dropdown: on = active and In progress (respecting the active-task limit), off = To Do. Existing tasks keep State.
- **Add to Top / Bottom** for new tasks, so urgent tasks no longer need to be dragged up.
- **Due-date chips:** *Today*, *Tomorrow*, *Next week* (Monday), *Clear*. The time is now optional ("+ Add a time", 30-minute slots); without a time the task is due all day.
- **Paste a list into Steps:** each line becomes a step, and bullets or numbering are removed.
- **Unsaved-changes guard:** leaving an edited form (another row, New task, Cancel or Esc) shows "You have unsaved changes · Keep editing · Discard · Save" instead of silently losing the edits.

### Changed
- The form is re-ordered as **Title → Steps → Planning (ticket, state/start, priority/position, due date) → Notes**. In the full-width layout, Planning sits in the right column.
- The cursor goes straight to **Title** when you create a task.
- The ticket number shows the number it will get ("Auto: i18") instead of a blank box.
- Description and Acceptance criteria start compact and grow with their content. Character counters appear only near the limit.
- The due date shows as e.g. "Fri, 25 Sept 2026" (your regional format) instead of dd-MM-yyyy. The 24- and 60-item hour/minute lists are gone.
- The step "Add" button is a small secondary "+" so it no longer competes with **Save task**.

## [1.4.3.4] - Development build

### Changed
- **✕ / Alt+F4 now hides the app straight to the tray without showing the widget.** Minimize (—) still shows the widget. Double-click the tray icon to reopen.

### Fixed
- Title-bar minimize/close buttons showed garbled characters (`â€"`, `âœ•`) after a file-encoding mistake in the 1.4.3.4 version bump, and used WPF's default light-blue hover. The text is restored and they now use proper Windows-style caption buttons (Fluent icons, subtle hover, red hover on close). The same encoding damage in `PACKAGING_CONFIG.md` is repaired.
- Dragging a task to change its priority no longer opens the ticket. The list now selects a row on mouse release, and only when the press wasn't a drag. The ACTIVE checkbox still responds immediately.

## [1.4.3.3] - Development build

### Changed — UI/UX review (quick wins)
- **Task list:**
  - Titles are no longer truncated to a few letters when the details panel is open. The redundant PRIORITY column is gone; the rank shows in a slim `#` column, and a drag grip appears on hover.
  - Each row shows the **due date** under the title (amber = today, red = overdue; date-only targets count as due for the whole day) and **step progress** (`2/5`).
  - Active tasks have an accent bar and tint. Done tasks are struck through.
  - When space is tight, the state pill becomes a coloured dot.
- **Empty state:** "No tasks yet — Create your first task", or "No tasks match your filters — Show all tasks".
- **Drag and drop:** reordering starts only after the mouse actually moves, so clicking a row no longer triggers accidental drags.
- **Task details:**
  - Title comes first.
  - Priority is shown as read-only text ("#3 in the list") instead of a disabled-looking textbox.
  - The header reads "New task" for unsaved tasks, and Delete is hidden until a task is saved.
- **Keyboard shortcuts:** `Ctrl+N` new, `Ctrl+S` save, `Esc` close details, `Ctrl+F` search, `Del` delete.
- **Delete is immediate with Undo** (6-second bar at the bottom) instead of a confirmation dialog. The "active task limit" warning also uses this bar, with a *Change limit* shortcut, instead of a system dialog.
- **Close (✕ / Alt+F4) hides to the tray** (one-time tip explains it); quit via tray → Exit.
- **Consistency:**
  - Sentence-case headings at consistent sizes ("Tasks", "Task details", "Settings").
  - "New task" button with icon; chevron icon on the States filter; matching filter font sizes.
  - Tooltips and accessible names on all icon buttons.
- **Widget readability:**
  - The light theme drops the muddy text shadow, and its arrows, step pill and footer icons are now visible (they were hard-coded white).
  - The minimum widget opacity is raised from 10% to 40%.

## [1.4.3.2] - Development build

### Added
- **Widget styles** (Settings → Desktop Widget → *Widget Style*):
  - **Floating:** the classic movable widget (default).
  - **Edge-docked (auto-hide):** drag the widget to an outer screen edge and it snaps there. It collapses to a thin tab showing the ticket number, slides open on hover and slides away 0.7 s after the mouse leaves. It never docks to an edge shared with another monitor or to the taskbar edge. The docked edge is remembered.
  - **Taskbar strip (experimental):** a one-line strip on the taskbar, left of the system tray. It shows `#ticket · current step`, or `#ticket · task name` when the task has no open steps. On hover it offers ✓ (complete step/task) and ‹ › (switch active task, also with the mouse wheel). Clicking it opens the full widget above the taskbar. It hides while the taskbar is auto-hidden or a fullscreen app is active. With a vertical taskbar, the app falls back to Floating.

### Changed
- The widget no longer takes keyboard focus when clicked, so you can keep typing in your current app.
- The widget position is saved once when a drag or resize ends, instead of on every pixel of movement.
- A widget position saved on a disconnected monitor or an old resolution is pulled back on screen.
- Tray menu: "Center Widget" is renamed **"Reset Widget Position"** (it also undocks).

## [1.4.3.1] - Development build

### Removed
- **Microsoft Teams call detection.** The local Teams WebSocket client never worked (it had no pairing token). The app no longer makes any network connection. Notification details are now hidden only while Windows Do Not Disturb is on.

### Fixed
- **Data safety:** `tasks.json` and `settings.json` are now written atomically (temp file + swap), and the previous version is kept as `.bak`.
- **Data safety:** An unreadable `tasks.json` is no longer silently replaced by an empty list. It is moved aside as `tasks.corrupt-<timestamp>.json`, the last backup is restored, and the user is told what happened.
- **Data safety:** A failed save (disk full, file locked) shows an error instead of crashing the app. Failed settings writes are ignored safely.
- **Data safety:** The deadline-reminder timer now runs on the UI thread, so it no longer reads the task list while it is being modified.
- **Data safety:** Sub-task step IDs are preserved when a task is edited and saved.
- The test project now builds (target framework aligned with the app). Added tests for task-file backup and recovery.
- About page: replaced the "Your Company" placeholder and added the Privacy Policy link.

### Changed
- In-app documentation, `INSTRUCTIONS.md` and `v1_documentation.md` updated to match current behaviour.
- Repository hygiene: build output (`TaskTrackerApp/Output/`), MSIX packages and release installers are no longer tracked. `.gitignore` is fixed (it was partly saved as UTF-16).

## [1.4.3.0]
### Added
- **Widget Hover Functionality:** Added hover-over visibility states for control buttons on the desktop widget.
- **Widget Tooltips:** Added text trimming and tooltips for long sub-task steps in the widget.
### Changed
- Improved auto-advance logic for sub-task steps inside the widget.
- Preserved current task index when widget tasks are refreshed.
- Improved fallback mechanism for native Windows notifications.

## [1.4.2.0]
### Fixed
- Fixed Microsoft Store package conflicts and validation errors.
- Compressed `Wide310x150Logo` image to strictly comply with the 200KB Microsoft Store limit.
- Permanently resolved the "Double Start Menu Ghost Icon" issue by optimizing the MSBuild packaging configuration.
- Configured GitHub Actions to properly build the `.msixupload` bundle for Store Submission.

## [1.4.1.0]
### Added
- **Sub-task Management:** Complex tickets can now be broken down into smaller, actionable sub-task steps.
- **Drag-and-Drop Reordering:** Added intuitive drag-and-drop support to prioritize sub-tasks effortlessly.
- **Store Readiness:** Added Privacy Policy link, MSIX installer, and Store-compliant configurations.

## [1.3.8.0]
### Changed
- Stable base version before the introduction of Sub-task Steps.
