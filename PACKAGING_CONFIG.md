# MSIX Packaging Configuration and Feedback

This document stores the correct configuration required for creating the MSIX package for the Task Tracker app to avoid validation errors during Microsoft Partner Center submission.

## Package Configuration
When using the MSIX Packaging Tool, use the following details:

*   **Package name:** `TanmayPrasad.TaskAndTicketTracker`
*   **Package display name:** `TanmayPrasad.TaskAndTicketTracker_5njg6p2f7v32e`
*   **Publisher name:** `CN=00F61C64-FF38-455E-8D55-F7094707F364`
*   **Publisher display name:** `Tanmay Prasad`
*   **Version:** `1.4.1.0` (or the version you are currently building)
*   **Package Description:** `Task And Ticket Tracker is a lightweight, context-aw` (Note: ensure it matches the store listing if required)

## Past Validation Feedbacks (Partner Center Errors)
The following errors were encountered when the package was incorrectly configured. Make sure the manifest matches the expected values exactly.

*   **Invalid package identity name:** The package identity name must be exactly `TanmayPrasad.TaskAndTicketTracker`. (Failed when it was `73b9b58e-c254-4329-8f0f-fbf0a1db9738`).
*   **Invalid package family name:** Expected `TanmayPrasad.TaskAndTicketTracker_5njg6p2f7v32e`. (Failed when it was `73b9b58e-c254-4329-8f0f-fbf0a1db9738_v25hrw1y8zg10`).
*   **Invalid package publisher name:** The publisher name must be exactly `CN=00F61C64-FF38-455E-8D55-F7094707F364`. (Failed when it was `CN=8E711AA0-7EBE-4C90-8800-4740D796CFB0`).
*   **Display Name Reservation:** The package's manifest (`Package/Properties/DisplayName`) used a display name that was not reserved (`Task Tracker`). It must match a name you have reserved in the Partner Center.

## Versioning Rules
Follow these rules when updating the version number (format: `1st.2nd.3rd.0` e.g., `1.4.2.0`). **CRITICAL:** Microsoft Store strictly requires the 4th digit (Revision) to be exactly `0`!

*   **1st Index (Major):** Only increase when explicitly asked by the user.
*   **2nd Index (Minor):** Increase when a new major feature is added (e.g., Steps feature, new view for task creation). *Note: Setup generation/installers should only be created for these versions when explicitly asked.*
*   **3rd Index (Build/Fixes):** Increase when a new item is added, such as a UI redesign or adding hover-over functionality. You must **also** use this index for bug fixes or minor code adjustments, because the 4th index is reserved by Microsoft.
*   **4th Index (Revision):** MUST ALWAYS BE `0` for new store submissions.
