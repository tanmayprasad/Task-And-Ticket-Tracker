<#
.SYNOPSIS
    Installs (or updates) a CI-built test package (.msixbundle) on this PC.

.DESCRIPTION
    CI signs test builds with a throw-away self-signed certificate (TaskTrackerCert.cer next to the package).
    This script:
      1. asks for administrator rights (UAC prompt) if it isn't elevated,
      2. trusts that certificate (LocalMachine\TrustedPeople) and removes older test certificates for the same
         publisher, so old builds don't stay trusted,
      3. stops a running copy of the app,
      4. installs the package with -ForceUpdateFromAnyVersion (allows re-installing an older/equal version). If
         Windows still refuses because it is the same version with different contents, the installed copy is
         removed and the new build installed.
    Works on Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./scripts/Install-TestPackage.ps1 -PackageFolder "D:\One App\Tasks\Tasks_v1\MSIX\TaskTrackerApp-MSIX 1.5.0.0"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageFolder,
    [string]$LogFile = (Join-Path $env:TEMP 'tt_install.log')
)

$ErrorActionPreference = 'Stop'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    # Re-run this script elevated (Windows shows the UAC prompt) and wait for it, then show its log
    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    $self = $MyInvocation.MyCommand.Path
    $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$self`"", '-PackageFolder', "`"$PackageFolder`"", '-LogFile', "`"$LogFile`"")
    $proc = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $args2 -Verb RunAs -Wait -PassThru
    if (Test-Path $LogFile) { Get-Content $LogFile }
    exit $proc.ExitCode
}

function Log($m) { $m | Tee-Object -FilePath $LogFile -Append | Out-Host }

try {
    $bundle = Get-ChildItem $PackageFolder -Recurse -Filter *.msixbundle | Select-Object -First 1
    $cerFile = Get-ChildItem $PackageFolder -Recurse -Filter *.cer | Select-Object -First 1
    if (-not $bundle) { throw "No .msixbundle found under $PackageFolder" }
    if (-not $cerFile) { throw "No .cer certificate found under $PackageFolder" }
    $cer = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerFile.FullName)

    $sig = Get-AuthenticodeSignature $bundle.FullName
    if ($sig.SignerCertificate.Thumbprint -ne $cer.Thumbprint) { throw "The package is not signed with the certificate in the folder (signer $($sig.SignerCertificate.Thumbprint))." }

    # Remove older test certificates for the same publisher, then trust the new one
    $store = 'Cert:\LocalMachine\TrustedPeople'
    Get-ChildItem $store | Where-Object { $_.Subject -eq $cer.Subject -and $_.Thumbprint -ne $cer.Thumbprint } | ForEach-Object {
        Log "Removing old test certificate $($_.Thumbprint)"
        Remove-Item $_.PSPath
    }
    Import-Certificate -FilePath $cerFile.FullName -CertStoreLocation $store | Out-Null
    Log "Trusted certificate $($cer.Thumbprint) (expires $($cer.NotAfter.ToString('yyyy-MM-dd')))"

    Get-Process TaskTrackerApp -ErrorAction SilentlyContinue | Stop-Process -Force

    Log "Installing $($bundle.Name) ..."
    try {
        Add-AppxPackage -Path $bundle.FullName -ForceUpdateFromAnyVersion
    }
    catch {
        if ($_.Exception.Message -notmatch '0x80073CFB') { throw }
        # Same version number but different contents (a rebuild): Windows refuses to update in place, and
        # -PreserveApplicationData only works for development-mode packages. So the installed copy is removed
        # and the new build installed. The app's data normally lives outside the package (%LOCALAPPDATA%\TaskTrackerApp),
        # but back up %LOCALAPPDATA%\Packages\<family> yourself first if you rely on it.
        Log "Same version already installed with different contents; removing it and installing the new build."
        Get-AppxPackage -Name 'TanmayPrasad.TaskAndTicketTracker' | Remove-AppxPackage
        Add-AppxPackage -Path $bundle.FullName
    }
    $pkg = Get-AppxPackage -Name 'TanmayPrasad.TaskAndTicketTracker'
    Log "INSTALLED: version $($pkg.Version), signature $($pkg.SignatureKind)"
    Log "Location : $($pkg.InstallLocation)"
    exit 0
}
catch {
    Log "FAILED: $($_.Exception.Message)"
    exit 1
}
