# Microsoft Store listing - version 1.5.0.0

Copy these into Partner Center → the product → *Submission* → *Store listings*. The "What's new" text is generated from
`CHANGELOG.md` (`### Store release notes`) by `scripts/Get-ReleaseNotes.ps1 -Version 1.5.0.0 -Format Store`, and the release pipeline
uses the same text automatically.

## What's new in this version (limit 1500 characters, 946 used)

```
What's new in version 1.5.0.0:
• A fresh Windows 11 look: Mica window, your Windows accent colour, a navigation pane, and tasks shown as cards.
• Choose how your active task is shown: a floating widget, a tab docked to the screen edge, or a strip on the taskbar.
• Add tasks in seconds with Quick add, Save & new, "Start now" and Today / Tomorrow / Next week due dates.
• Ticket numbers now lead every task in the list and become the heading when you open a task. The navigation pane starts collapsed to give your tasks more room.
• Paste a list to create steps, and see due dates and step progress right in the task list.
• Delete with Undo, a warning before unsaved changes are lost, and keyboard shortcuts. Press F1 for the new user guide.
• The widget no longer takes keyboard focus, and closing the window keeps the app running in the tray.
• Your tasks are saved more safely with automatic backups, and the app no longer uses the network.
```

## Screenshots (`Screenshots/1.5.0.0/`)

All are PNG, 16:9 and larger than the 1366 x 768 minimum. They show fictional demo tasks only. Upload them in this order (the first is the hero image).

| # | File | Caption (max 200 characters) |
|---|------|------------------------------|
| 1 | `01-task-list-dark.png` | Your tasks as clean cards: ticket number, title, due date and step progress at a glance. |
| 2 | `02-task-details-dark.png` | Open a task to see its steps, state, priority, due date and acceptance criteria side by side with your list. |
| 3 | `03-new-task-dark.png` | Create tasks fast: paste a list to make steps, pick Today or Tomorrow, or start work right away. |
| 4 | `06-widget-dark.png` | Keep your current task in view with a floating widget, an edge-docked tab or a taskbar strip. |
| 5 | `05-task-list-light.png` | Light or dark, it follows Windows and uses your accent colour. |
| 6 | `04-settings-dark.png` | Simple settings that apply instantly: reminders, appearance and widget style. |

## Re-creating the screenshots

The captures are made from the running app with a temporary set of demo tasks; the user's own `tasks.json` and `settings.json` are backed
up first and restored afterwards (verified by file hash). The images are 16:9, cropped above the taskbar, on a neutral gradient backdrop.
