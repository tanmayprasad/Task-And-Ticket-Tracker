param(
    [string]$Version,
    [ValidateSet('major','minor','patch','build')][string]$Increment = 'build',
    [string]$PackagingProj = "TaskTrackerApp.Packaging\TaskTrackerApp.Packaging.wapproj",
    [string]$AppProj = "TaskTrackerApp\TaskTrackerApp.csproj",
    [string]$ManifestPath = "TaskTrackerApp.Packaging\AppxManifest.xml",
    [string]$Configuration = 'Release'
)

function Get-CurrentVersion {
    param($manifest)
    $text = Get-Content $manifest -Raw
    $m = [regex]::Match($text, '<Identity[^>]*Version="([0-9\.]+)"')
    if ($m.Success) { return $m.Groups[1].Value }
    throw "Could not find Identity Version in $manifest"
}

function Bump-Version {
    param($current, $inc)
    $parts = $current -split '\.' | ForEach-Object {[int]$_}
    while ($parts.Count -lt 4) { $parts += 0 }
    switch ($inc) {
        'major' { $parts[0] += 1; $parts[1]=0; $parts[2]=0; $parts[3]=0 }
        'minor' { $parts[1] += 1; $parts[2]=0; $parts[3]=0 }
        'patch' { $parts[2] += 1; $parts[3]=0 }
        'build' { $parts[3] += 1 }
    }
    return ($parts -join '.')
}

Write-Host "Using manifest: $ManifestPath"
if (-not (Test-Path $ManifestPath)) { throw "Manifest not found: $ManifestPath" }

$current = Get-CurrentVersion -manifest $ManifestPath
Write-Host "Current version: $current"

if ($Version) {
    $newVersion = $Version
} else {
    $newVersion = Bump-Version -current $current -inc $Increment
}

if ($newVersion -eq $current) { Write-Host "Version unchanged: $newVersion" } else {
    Write-Host "Updating version to: $newVersion"
    $content = Get-Content $ManifestPath -Raw
    $pattern = 'Version="' + [regex]::Escape($current) + '"'
    $replacement = 'Version="' + $newVersion + '"'
    $content = [regex]::Replace($content, $pattern, $replacement)
    Set-Content -Path $ManifestPath -Value $content -Encoding utf8
}

Write-Host "Building project: $AppProj (Configuration=$Configuration)"
dotnet build $AppProj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "App build failed" }

Write-Host "Building packaging project: $PackagingProj (Configuration=$Configuration)"
dotnet build $PackagingProj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Warning "dotnet build failed for packaging project — attempting to locate MSBuild.exe and try msbuild."
    $msbuildCmd = $null
    # Try vswhere to locate Visual Studio and its MSBuild
    $vswherePaths = @("${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe", "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe")
    $vsinstall = $null
    foreach ($p in $vswherePaths) {
        if (Test-Path $p) {
            try {
                $inst = & "$p" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
                if ($inst) { $vsinstall = $inst.Trim(); break }
            } catch {}
        }
    }
    if ($vsinstall) {
        $candidate = Join-Path $vsinstall "MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $candidate) { $msbuildCmd = $candidate }
    }
    # Fallback to system msbuild if present
    if (-not $msbuildCmd) {
        try { $msbuildCmd = (Get-Command msbuild.exe -ErrorAction SilentlyContinue).Source } catch {}
    }
    if (-not $msbuildCmd) {
        try { $where = & where.exe msbuild.exe 2>$null | Select-Object -First 1; if ($where) { $msbuildCmd = $where } } catch {}
    }
    if ($msbuildCmd) {
        Write-Host "Found MSBuild at: $msbuildCmd — running msbuild on packaging project"
        & $msbuildCmd $PackagingProj /p:Configuration=$Configuration /p:UapAppxPackageBuildMode=StoreUpload /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Always /p:AppxPackageSigningEnabled=false
        if ($LASTEXITCODE -ne 0) { throw "MSBuild packaging build also failed" }
    } else {
        throw "Packaging build failed and MSBuild.exe was not found. To build the packaging project you need Visual Studio (with the Windows Application Packaging Project / Desktop Bridge targets) installed."
    }
}

# Find generated package files (msix/appx)
$packDir = Join-Path -Path (Split-Path $PackagingProj) -ChildPath "bin\$Configuration"
Write-Host "Searching for packages in: $packDir"
$packages = Get-ChildItem -Path $packDir -Recurse -Include *.msix, *.appx, *.msixupload -File -ErrorAction SilentlyContinue

if ($packages.Count -eq 0) {
    Write-Warning "No package files (.msix/.appx/.msixupload) were found under $packDir."
} else {
    foreach ($p in $packages) {
        Write-Host "Found package: $($p.FullName)"
    }
    # Copy to Releases folder
    $outDir = Join-Path -Path (Get-Location) -ChildPath 'Releases'
    if (-not (Test-Path $outDir)) { New-Item -Path $outDir -ItemType Directory | Out-Null }
    foreach ($p in $packages) {
        $dest = Join-Path $outDir $p.Name
        Copy-Item -Path $p.FullName -Destination $dest -Force
        Write-Host "Copied to: $dest"
    }
}

Write-Host "Done. New version: $newVersion"

Write-Host 'Note: If you need to sign the package, provide a certificate and sign with signtool after building.'
