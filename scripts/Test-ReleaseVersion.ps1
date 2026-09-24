<#
.SYNOPSIS
    Checks that a version is ready to be submitted to the Microsoft Store.

.DESCRIPTION
    Fails (exit code 1) unless:
      * the version has the form Major.Minor.Build.Revision and Revision is 0 (Store rule;
        development builds such as 1.4.3.5 must never be submitted),
      * TaskTrackerApp.Packaging/AppxManifest.xml has exactly this Identity Version,
      * the About page (TaskTrackerApp/MainWindow.xaml) shows "Version: <version>",
      * CHANGELOG.md has a "## [<version>]" section that is no longer marked "In Development".
    setup.iss (legacy installer) is only checked with a warning.

    Use -AllowDevBuild to validate a development build locally.
    Works on Windows PowerShell 5.1 and PowerShell 7; emits GitHub Actions annotations.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$RepoRoot,
    [switch]$AllowDevBuild
)

$ErrorActionPreference = 'Stop'
# (Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults)
if (-not $RepoRoot) { $RepoRoot = Join-Path $PSScriptRoot '..' }
$utf8 = New-Object System.Text.UTF8Encoding($false)
$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Read-Text([string]$relative) {
    $path = Join-Path $RepoRoot $relative
    if (-not (Test-Path $path)) { $errors.Add("Missing file: $relative"); return '' }
    return [IO.File]::ReadAllText((Resolve-Path $path).Path, $utf8)
}

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    $errors.Add("'$Version' is not a four-part version such as 1.5.0.0.")
}
elseif (-not $AllowDevBuild -and $Version.Split('.')[3] -ne '0') {
    $errors.Add("$Version is a development build. The Microsoft Store requires the last number to be 0. Finalize it first, e.g.: scripts/Set-Version.ps1 -Version 1.5.0.0 -Release")
}

$manifestText = Read-Text 'TaskTrackerApp.Packaging\AppxManifest.xml'
if ($manifestText) {
    $manifest = [xml]$manifestText
    $manifestVersion = $manifest.Package.Identity.Version
    if ($manifestVersion -ne $Version) { $errors.Add("AppxManifest.xml has Version=$manifestVersion, expected $Version.") }
}

$about = Read-Text 'TaskTrackerApp\MainWindow.xaml'
if ($about -and $about -notmatch [regex]::Escape("Text=""Version: $Version""")) {
    $errors.Add("The About page (MainWindow.xaml) does not show 'Version: $Version'.")
}

$changelog = Read-Text 'CHANGELOG.md'
if ($changelog) {
    $header = [regex]::Match($changelog, '(?m)^## \[' + [regex]::Escape($Version) + '\].*$')
    if (-not $header.Success) { $errors.Add("CHANGELOG.md has no '## [$Version]' section.") }
    elseif (-not $AllowDevBuild -and $header.Value -match 'In Development') {
        $errors.Add("CHANGELOG.md still marks $Version as 'In Development'. Give it a release date (scripts/Set-Version.ps1 -Release does this).")
    }
}

$setup = Read-Text 'setup.iss'
if ($setup -and $setup -notmatch ('(?m)^AppVersion=' + [regex]::Escape($Version) + '\s*$')) {
    $warnings.Add("setup.iss AppVersion is not $Version (only matters for the legacy installer).")
}

$inCi = [bool]$env:GITHUB_ACTIONS
foreach ($w in $warnings) { if ($inCi) { Write-Host "::warning::$w" } else { Write-Warning $w } }
if ($errors.Count -gt 0) {
    foreach ($e in $errors) { if ($inCi) { Write-Host "::error::$e" } else { Write-Host "ERROR: $e" -ForegroundColor Red } }
    exit 1
}
Write-Host "Version $Version is consistent and ready for release."
