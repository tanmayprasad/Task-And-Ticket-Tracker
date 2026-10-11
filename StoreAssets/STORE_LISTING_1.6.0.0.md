# Microsoft Store update - version 1.6.0.0

Everything needed to submit the 1.6.0.0 update in Partner Center → the product (Store ID `9N34FGG6MWT7`) → *Start update*.
The "What's new" text is generated from `CHANGELOG.md` (`### Store release notes`) by
`scripts/Get-ReleaseNotes.ps1 -Version 1.6.0.0 -Format Store`.

## 1. Package (*Packages* page)

| | |
|---|---|
| File to upload | `MSIX\TaskTrackerApp-MSIX 1.6.0.0\AppPackages\TaskTrackerApp.Packaging_1.6.0.0_x64_bundle.msixupload` |
| Version | `1.6.0.0` (previous Store version: `1.5.0.0`) |
| Architecture | x64 |
| Identity name | `TanmayPrasad.TaskAndTicketTracker` |
| Publisher | `CN=00F61C64-FF38-455E-8D55-F7094707F364` |
| Minimum Windows | 10.0.17763.0 (unchanged) |
| Bundled runtime | .NET 10.0.12, self-contained (unchanged) |
| Capabilities | `runFullTrust` (unchanged) |
| New in the manifest | `windows.startupTask` extension, task `TaskTrackerStartup`, **disabled by default** |

The upload file is unsigned; the Store signs it. The signed copy in `AppPackages\TaskTrackerApp.Packaging_1.6.0.0_Test\` is only
for local testing with `scripts/Install-TestPackage.ps1`.

This package was assembled locally with the Windows SDK tools (`makepri`, `makeappx`) to the same layout as the CI-built 1.5.0.0
package, because this PC has no Visual Studio. If Partner Center rejects it, build it in CI instead: Actions →
*Release to Microsoft Store* → *Run workflow* with version `1.6.0.0` and *Submit* unticked, then upload that artifact.

## 2. What's new in this version (limit 1500 characters, 908 used)

```
What's new in version 1.6.0.0:
• Profiles: keep separate task lists, for example Work and Personal, each with its own active-task limit and reminders. Reminders arrive for every profile.
• Export a profile's tasks to a file (JSON, or CSV for Excel) and import them into any profile.
• Start with Windows: a new switch in Settings starts the app when you sign in, straight to the desktop widget, so reminders never depend on you opening it.
• New to the app, or just updated? A one-minute guided tour now shows how everything works: Quick add, active (★) tasks, the desktop widget, and what minimize and close do.
• After each update, a short "What's new" tour points out the new features right where they are.
• A new ? button replays the tour, shows what's new and opens the user guide.
• The edge-docked widget keeps its ticket tab attached while it slides in and out, and hides sooner after you move away.
```

The same text is in `MSIX\TaskTrackerApp-MSIX 1.6.0.0\store-release-notes.txt`.

## 3. Product features (optional additions to the listing's feature list, max 200 characters each)

- Profiles: separate task lists such as Work and Personal, each with its own active-task limit and reminders.
- Import and export tasks as JSON (full backup) or CSV (Excel), per profile, with Undo.
- Start with Windows: opens straight to the desktop widget when you sign in.
- Guided getting-started tour, and a short "What's new" tour after each update.

## 4. Notes for certification (*Submission options* → *Notes for certification*)

```
Version 1.6.0.0 adds an optional "Start with Windows" setting (Settings → Start-up). It uses the windows.startupTask
extension, which is disabled by default and is only enabled when the user turns the switch on. The user can turn it off
again in the app or in Windows Settings → Apps → Startup. When started at sign-in the app shows only its tray icon and
desktop widget.

No account or sign-in is needed. The app does not use the network; all data stays in %LOCALAPPDATA%\TaskTrackerApp.
On first launch a short guided tour appears; press Esc or "Not now" to skip it.
```

## 5. Things that do not change

- **Privacy policy, age rating, pricing, markets and category:** unchanged. The new features store nothing new off the device.
- **`runFullTrust` justification:** unchanged from 1.5.0.0 (packaged desktop WPF app).
- **Description:** still accurate; the feature lines in section 3 are optional.

## 6. Screenshots

New for 1.6.0.0 (`Screenshots/1.6.0.0/`, PNG, 1920 x 1080, fictional demo data only):

| File | Caption (max 200 characters) |
|------|------------------------------|
| `01-widget-styles-dark.png` | Keep your active task in view your way: a floating card, a tab docked to the screen edge, or a strip on the taskbar. |
| `02-profiles-dark.png` | Profiles keep Work, Personal and side projects apart, each with its own active-task limit, reminders, import and export. |

`01-widget-styles-dark.png` is a composite: the three widget styles were captured from the running app and placed on one
plain backdrop with labels (enlarged 1.6x), and the taskbar under the strip is a plain band, not a real taskbar. It can
replace `06-widget-dark.png` from 1.5.0.0. `02-profiles-dark.png` is a direct capture of the app window.

The other 1.5.0.0 screenshots (`Screenshots/1.5.0.0/`, captions in `STORE_LISTING_1.5.0.0.md`) can stay, but they predate
the redesigned title bar and the profile badge in the navigation pane; they have not been re-captured.

## 7. After the update is published

1. Uninstall the local test copy before installing from the Store (same version, different signature).
2. Tag the release: `git tag v1.6.0.0` and push the tag. Note that the tag starts the *Release to Microsoft Store* workflow,
   which tries to submit again if the Partner Center secrets are configured.
3. Start the next cycle: `./scripts/Set-Version.ps1 -Version 1.6.0.1` and add a `## [1.6.0.1] - In Development` section to
   `CHANGELOG.md`.
