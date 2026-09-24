<#
.SYNOPSIS
    Builds release notes for a version from CHANGELOG.md.

.DESCRIPTION
    Takes the "## [<Version>] ..." section plus the development-build sections directly below it
    (versions whose 4th number is not 0, e.g. 1.4.3.5) down to the previous Store release, so that
    everything changed during the development cycle is included. Entries are merged by their
    "### Added / Changed / Fixed / Removed" headings.

    -Format Markdown  Full notes for the GitHub release.
    -Format Store     Plain text for the Microsoft Store "What's new in this version" field
                      (Store limit: 1500 characters). If the release section contains a
                      "### Store release notes" subsection, only that text is used; otherwise the
                      merged entries are converted and shortened to fit.

    Works on Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./scripts/Get-ReleaseNotes.ps1 -Version 1.5.0.0 -Format Store -OutFile release/store-release-notes.txt
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet('Markdown', 'Store')][string]$Format = 'Markdown',
    [string]$ChangelogPath,
    [string]$OutFile,
    [int]$MaxStoreLength = 1500
)

$ErrorActionPreference = 'Stop'
# (Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults)
if (-not $ChangelogPath) { $ChangelogPath = Join-Path $PSScriptRoot '..\CHANGELOG.md' }
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$lines = [IO.File]::ReadAllText((Resolve-Path $ChangelogPath).Path, $utf8NoBom) -split "`r?`n"

# --- Split the changelog into "## [version]" sections -------------------------------------------
$sections = New-Object System.Collections.Generic.List[object]
$current = $null
foreach ($line in $lines) {
    if ($line -match '^## \[(?<v>[^\]]+)\]') {
        $current = [pscustomobject]@{ Version = $Matches['v']; Body = New-Object System.Collections.Generic.List[string] }
        $sections.Add($current)
    }
    elseif ($null -ne $current) {
        $current.Body.Add($line)
    }
}

$start = -1
for ($i = 0; $i -lt $sections.Count; $i++) {
    if ($sections[$i].Version -eq $Version) { $start = $i; break }
}
if ($start -lt 0) { throw "CHANGELOG.md has no '## [$Version]' section." }

function Test-DevBuild([string]$v) {
    $parts = $v.Split('.')
    return ($parts.Count -eq 4 -and $parts[3] -ne '0')
}

$picked = New-Object System.Collections.Generic.List[object]
$picked.Add($sections[$start])
for ($i = $start + 1; $i -lt $sections.Count; $i++) {
    if (Test-DevBuild $sections[$i].Version) { $picked.Add($sections[$i]) } else { break }
}

# --- Merge entries by heading; pick up an explicit "### Store release notes" block -------------
$groups = [ordered]@{}
$storeBlock = New-Object System.Collections.Generic.List[string]
foreach ($section in $picked) {
    $isReleaseSection = [object]::ReferenceEquals($section, $picked[0])
    $heading = 'Changes'
    $inStoreBlock = $false
    foreach ($line in $section.Body) {
        if ($line -match '^###\s+(?<h>.+?)\s*$') {
            $h = $Matches['h']
            $inStoreBlock = ($h -match '^Store release notes')
            # "Changed - UI/UX review" (em dash or hyphen) and "Changed" merge into one "Changed" group
            if (-not $inStoreBlock) { $heading = ($h -split '\s+[\u2014-]\s+', 2)[0].Trim() }
            continue
        }
        if ($inStoreBlock) {
            # Only the release section's own store block is used; older ones belong to older builds
            if ($isReleaseSection) { $storeBlock.Add($line) }
            continue
        }
        if ($line -match '^\s*>' -or $line.Trim() -eq '') { continue }   # dev-build notes and blank lines
        if (-not $groups.Contains($heading)) { $groups[$heading] = New-Object System.Collections.Generic.List[string] }
        $groups[$heading].Add($line)
    }
}

# Conventional order first, anything else afterwards
$order = @('Added', 'Changed', 'Fixed', 'Removed', 'Security', 'Deprecated')
$headings = @($order | Where-Object { $groups.Contains($_) }) + @($groups.Keys | Where-Object { $order -notcontains $_ })

function ConvertTo-PlainText([string]$line) {
    $indent = $line.Length - $line.TrimStart().Length
    $text = $line.Trim()
    $prefix = ''
    if ($text -match '^[-*]\s+(?<rest>.*)$') {
        $text = $Matches['rest']
        $prefix = if ($indent -ge 2) { '   - ' } else { [string][char]0x2022 + ' ' }
    }
    $text = $text -replace '\*\*(.+?)\*\*', '$1'          # bold
    $text = $text -replace '`([^`]*)`', '$1'              # code
    $text = $text -replace '\[([^\]]+)\]\([^)]*\)', '$1'  # links
    $text = $text -replace '(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', '$1'  # italics
    return $prefix + $text
}

# --- Render ------------------------------------------------------------------------------------
if ($Format -eq 'Markdown') {
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("## Task And Ticket Tracker $Version")
    foreach ($h in $headings) {
        $out.Add('')
        $out.Add("### $h")
        foreach ($l in $groups[$h]) { $out.Add($l) }
    }
    if ($picked.Count -gt 1) {
        $out.Add('')
        $out.Add("_Includes the development builds " + (($picked | Select-Object -Skip 1 | ForEach-Object { $_.Version }) -join ', ') + '._')
    }
    $result = ($out -join "`n").Trim() + "`n"
}
else {
    $labels = @{ Added = 'New'; Changed = 'Improved'; Fixed = 'Fixed'; Removed = 'Removed' }
    $plain = New-Object System.Collections.Generic.List[string]
    $plain.Add("What's new in version ${Version}:")

    $explicit = @($storeBlock | Where-Object { $_.Trim() -ne '' })
    if ($explicit.Count -gt 0) {
        foreach ($l in $explicit) { $plain.Add((ConvertTo-PlainText $l)) }
    }
    else {
        foreach ($h in $headings) {
            $plain.Add('')
            $label = if ($labels.ContainsKey($h)) { $labels[$h] } else { $h }
            $plain.Add("${label}:")
            foreach ($l in $groups[$h]) { $plain.Add((ConvertTo-PlainText $l)) }
        }
    }

    # Keep whole lines within the Store limit
    $suffix = [string][char]0x2026 + 'and more improvements.'
    $sb = New-Object System.Text.StringBuilder
    $truncated = $false
    foreach ($l in $plain) {
        $needed = $sb.Length + $l.Length + 1
        if ($needed -gt ($MaxStoreLength - $suffix.Length - 1)) { $truncated = $true; break }
        [void]$sb.Append($l).Append("`n")
    }
    if ($truncated) { [void]$sb.Append($suffix).Append("`n") }
    $result = ($sb.ToString() -replace "(`n){3,}", "`n`n").Trim()
    if ($result.Length -gt $MaxStoreLength) { throw "Store notes are $($result.Length) characters (limit $MaxStoreLength)." }
}

if ($OutFile) {
    $dir = Split-Path -Parent $OutFile
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutFile), $result, $utf8NoBom)
}
$result
