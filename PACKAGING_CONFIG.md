# MSIX Packaging Configuration and Feedback

This document stores the correct configuration required for creating the MSIX package for the Task Tracker app to avoid validation errors during Microsoft Partner Center submission.

## Package Configuration
When using the MSIX Packaging Tool, use the following details:

*   **Package name:** `TanmayPrasad.TaskAndTicketTracker`
*   **Package display name:** `TanmayPrasad.TaskAndTicketTracker_5njg6p2f7v32e`
*   **Publisher name:** `CN=00F61C64-FF38-455E-8D55-F7094707F364`
*   **Publisher display name:** `Tanmay Prasad`
*   **Version:** the value in `TaskTrackerApp.Packaging/AppxManifest.xml` (currently `1.4.3.7`, a development build)
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

### Development builds (4th index)
While changes are still in progress, bump the **4th index** for each round of changes (e.g. `1.4.3.0` → `1.4.3.1` → `1.4.3.2`). These builds are for local testing and sideloading only. **Never submit them to the Store.**

When the changes are ready to ship, **reset the 4th index to `0` and bump the 3rd index** (e.g. `1.4.3.2` → `1.4.4.0`). The 2nd index applies instead if the release contains a major feature.

Use `scripts/Set-Version.ps1` for both: `-Version 1.4.3.7` for the next development build, and `-Version 1.4.4.0 -Release` to finalize a release. It updates every location below safely (UTF-8, line endings and byte-order mark preserved).

### Where the version lives
`scripts/Set-Version.ps1` updates all of these together:
1.  `TaskTrackerApp.Packaging/AppxManifest.xml` → `Identity/@Version` (authoritative)
2.  `TaskTrackerApp/MainWindow.xaml` → About tab `Version: x.y.z.w` text
3.  `CHANGELOG.md`
4.  `setup.iss` → `AppVersion` and `OutputBaseFilename` (only needed when building the installer)

## Releasing to the Microsoft Store

Releases are published by the **Release to Microsoft Store** workflow (`.github/workflows/release-store.yml`). It uses the official [Microsoft Store Developer CLI](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/commands). The Store's *"What's new in this version"* text comes from `CHANGELOG.md`.

### One-time setup
1.  **Create an Entra ID (Azure AD) app** for Partner Center:
    *   Partner Center → **Account settings → User management → Microsoft Entra applications → Create Microsoft Entra application** (or add an existing one), with the **Manager** role.
    *   Create a **client secret** for it (note its expiry date and renew it before then).
2.  **Collect the IDs:**
    *   **Tenant ID** and **Client ID** come from that Entra app.
    *   **Seller ID** is shown in Partner Center → **Account settings → Legal info → Developer**.
    *   The app's **Store ID** is `9N34FGG6MWT7` (Partner Center → the app → **Product identity**); it's already in the workflow.
3.  **Add GitHub secrets** (repo → **Settings → Secrets and variables → Actions**):

    | Secret | Value |
    |---|---|
    | `PARTNER_CENTER_TENANT_ID` | Entra tenant ID |
    | `PARTNER_CENTER_SELLER_ID` | Partner Center seller ID |
    | `PARTNER_CENTER_CLIENT_ID` | Entra app (client) ID |
    | `PARTNER_CENTER_CLIENT_SECRET` | Entra app client secret |

    The app's Store ID (`9N34FGG6MWT7`) is built into the workflow; a repository *variable* `STORE_PRODUCT_ID` can override it.

4.  **Create a GitHub environment** named `microsoft-store` (repo → **Settings → Environments**) and add yourself as a **required reviewer**. Every submission then waits for your approval after the package is built and tested. You can also move the four secrets into this environment so that only approved runs can see them.

### Releasing a version
1.  Make sure the top `CHANGELOG.md` section (the one marked *In Development*) has a short, user-facing **`### Store release notes`** list. If it is missing, the Store text is generated from all entries of the cycle and shortened to 1500 characters.
2.  Finalize the version. This updates the manifest, About page, installer script and this file, and dates the changelog section:
    ```powershell
    ./scripts/Set-Version.ps1 -Version 1.5.0.0 -Release
    ```
3.  Commit and push a tag:
    ```powershell
    git commit -am "Release 1.5.0.0"
    git tag v1.5.0.0
    git push origin main v1.5.0.0
    ```
4.  **The workflow then:**
    *   validates the version (`scripts/Test-ReleaseVersion.ps1`: last number is `0`, and the version matches everywhere);
    *   builds the notes (`scripts/Get-ReleaseNotes.ps1`), runs the unit tests, and builds the `.msixupload`;
    *   **waits for your approval**;
    *   uploads the package, sets *What's new* on every Store listing language, submits for certification, and creates a GitHub release with the full notes and package.
5.  Certification usually takes up to **3 business days**. Follow it in Partner Center.

**Dry run:** Actions → *Release to Microsoft Store* → **Run workflow** with the version and *Submit* unticked. This validates, tests, packages and shows both release notes in the run summary without contacting the Store.

**After the release:** start the next development cycle, e.g. `./scripts/Set-Version.ps1 -Version 1.5.0.1`, and add a new `## [1.5.0.1] - In Development` section at the top of `CHANGELOG.md`.
