<#
.SYNOPSIS
    Launches the most recently built TaskTrackerApp.exe.

.DESCRIPTION
    Picks the newest TaskTrackerApp.exe under TaskTrackerApp\bin (any
    configuration / target framework) and starts it.

.PARAMETER Build
    Run 'dotnet build' first (Debug unless -Configuration is given).

.PARAMETER Configuration
    Only consider builds of this configuration (Debug or Release).

.PARAMETER Restart
    Close any running TaskTrackerApp instance before launching.

.EXAMPLE
    .\scripts\Start-LatestBuild.ps1
    .\scripts\Start-LatestBuild.ps1 -Build -Restart
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration,
    [switch]$Restart
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'TaskTrackerApp\TaskTrackerApp.csproj'
$binRoot  = Join-Path $repoRoot 'TaskTrackerApp\bin'

if ($Restart) {
    $running = Get-Process -Name 'TaskTrackerApp' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Closing running instance(s): $($running.Id -join ', ')"
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
}

if ($Build) {
    $buildConfig = if ($Configuration) { $Configuration } else { 'Debug' }
    Write-Host "Building ($buildConfig)..."
    dotnet build $project -c $buildConfig --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
}

$searchRoot = if ($Configuration) { Join-Path $binRoot $Configuration } else { $binRoot }
if (-not (Test-Path $searchRoot)) { throw "No build output at '$searchRoot'. Run with -Build." }

$exe = Get-ChildItem -Path $searchRoot -Recurse -Filter 'TaskTrackerApp.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $exe) { throw "TaskTrackerApp.exe not found under '$searchRoot'. Run with -Build." }

$relative = $exe.FullName.Substring($repoRoot.Length + 1)
Write-Host "Launching $relative (built $($exe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')))"
Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName
