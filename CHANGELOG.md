# Changelog

All notable changes to the **Task and Ticket Tracker** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to the specific versioning rules defined in `PACKAGING_CONFIG.md`.

## [1.4.3.0] - Current Latest
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
