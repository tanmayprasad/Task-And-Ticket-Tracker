<#
.SYNOPSIS
    Sets the app version everywhere it is written, and optionally finalizes CHANGELOG.md for a release.

.DESCRIPTION
    Updates:
      * TaskTrackerApp.Packaging/AppxManifest.xml  (Identity Version - authoritative)
      * TaskTrackerApp/MainWindow.xaml             (About page "Version: ...")
      * setup.iss                                  (AppVersion and installer file name)
      * PACKAGING_CONFIG.md                        ("currently ..." note)

    -Release renames the CHANGELOG "## [x] - In Development" heading to "## [<Version>] - <today>"
    and removes its "> Development build" note, so the development builds below it become part of
    this release's notes (see scripts/Get-ReleaseNotes.ps1).

    Files are read and written as UTF-8 and keep their byte-order-mark status, so non-ASCII text
    (dashes, symbols, ellipses) is never corrupted. Works on Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./scripts/Set-Version.ps1 -Version 1.4.3.6            # next development build
    ./scripts/Set-Version.ps1 -Version 1.5.0.0 -Release   # finalize a Store release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version,
    [switch]$Release,
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
# (Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults)
if (-not $RepoRoot) { $RepoRoot = Join-Path $PSScriptRoot '..' }
$isDevBuild = $Version.Split('.')[3] -ne '0'
if ($Release -and $isDevBuild) { throw "A Store release needs the last number to be 0 (got $Version)." }

function Update-File {
    param([string]$Relative, [string]$Pattern, [string]$Replacement, [switch]$Optional)
    $path = (Resolve-Path (Join-Path $RepoRoot $Relative)).Path
    $bytes = [IO.File]::ReadAllBytes($path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($path, (New-Object System.Text.UTF8Encoding($false)))
    $regex = New-Object System.Text.RegularExpressions.Regex($Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if (-not $regex.IsMatch($text)) {
        if ($Optional) { Write-Warning "${Relative}: pattern not found, skipped."; return }
        throw "${Relative}: could not find the version to update (pattern: $Pattern)."
    }
    $updated = $regex.Replace($text, $Replacement, 1)
    [IO.File]::WriteAllText($path, $updated, (New-Object System.Text.UTF8Encoding($hasBom)))
    Write-Host "Updated $Relative"
}

Update-File 'TaskTrackerApp.Packaging\AppxManifest.xml' '(<Identity\b[^>]*?\bVersion=")[^"]+(")' ('${1}' + $Version + '${2}')
Update-File 'TaskTrackerApp\MainWindow.xaml' '(Text="Version: )[^"]+(")' ('${1}' + $Version + '${2}')
Update-File 'setup.iss' '^(AppVersion=)[^\r\n]*' ('${1}' + $Version) -Optional
Update-File 'setup.iss' '^(OutputBaseFilename=TaskTrackerApp-win-Setup-v)[^\r\n]*' ('${1}' + $Version) -Optional
$note = if ($isDevBuild) { 'a development build' } else { 'the latest Store release' }
Update-File 'PACKAGING_CONFIG.md' '\(currently `[^`]+`, [^)]*\)' ('(currently `' + $Version + '`, ' + $note + ')') -Optional

if ($Release) {
    $today = (Get-Date).ToString('yyyy-MM-dd')
    # Heading -> release heading; drop the "> Development build ..." note right under it.
    # The captured newline keeps the file's own line endings.
    Update-File 'CHANGELOG.md' '^## \[[^\]]+\] - In Development(\r?\n)(> Development build[^\r\n]*\r?\n)?' ("## [$Version] - $today" + '${1}')
}

Write-Host ""
Write-Host "Version is now $Version." -ForegroundColor Green
if ($Release) { Write-Host "Next: review CHANGELOG.md (add a '### Store release notes' block if you like), commit, then tag v$Version and push the tag." }
