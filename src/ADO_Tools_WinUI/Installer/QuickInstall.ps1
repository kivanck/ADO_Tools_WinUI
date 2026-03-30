#Requires -Version 5.1
#Requires -Version 5.1
<#
.SYNOPSIS
    Installs ADO Tools by importing the signing certificate and installing the MSIX package.

.DESCRIPTION
    This script replaces the Visual Studio-generated Install.ps1 which fails because its
    embedded Authenticode signature has expired. This script has NO signature, so it works
    with any execution policy when launched via:
        PowerShell -ExecutionPolicy Bypass -File ".\QuickInstall.ps1"

    Or right-click -> "Run with PowerShell" after running:
        Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

    What this script does:
        1. Finds the .cer and .msix files in the same folder.
        2. Self-elevates to Administrator if needed (for certificate import).
        3. Imports the certificate into Trusted People (Local Machine).
        4. Installs architecture-appropriate dependency packages.
        5. Installs the ADO Tools MSIX package.

.NOTES
    Run from the folder that contains the .cer, .msix, and Dependencies subfolder.
#>

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ?? Colours ??????????????????????????????????????????????????
function Write-Step  ($msg) { Write-Host "  [$([char]0x2713)] $msg" -ForegroundColor Green }
function Write-Info  ($msg) { Write-Host "  [i] $msg" -ForegroundColor Cyan }
function Write-Warn  ($msg) { Write-Host "  [!] $msg" -ForegroundColor Yellow }
function Write-Err   ($msg) { Write-Host "  [x] $msg" -ForegroundColor Red }

# ?? Banner ???????????????????????????????????????????????????
Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host "    ADO Tools — Quick Installer" -ForegroundColor White
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ""

# ?? Locate files ?????????????????????????????????????????????
$cerFile  = Get-ChildItem -Path $ScriptDir -Filter "*.cer" -File | Select-Object -First 1
$msixFile = Get-ChildItem -Path $ScriptDir -Filter "*.msix" -File | Select-Object -First 1

if (-not $cerFile) {
    Write-Err "No .cer certificate file found in $ScriptDir"
    Write-Host ""; Pause; exit 1
}
if (-not $msixFile) {
    Write-Err "No .msix package file found in $ScriptDir"
    Write-Host ""; Pause; exit 1
}

Write-Info "Certificate : $($cerFile.Name)"
Write-Info "Package     : $($msixFile.Name)"
Write-Host ""

# ?? Check for Administrator privileges ???????????????????????
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Err "This script must be run as Administrator."
    Write-Warn "Right-click 'Install ADO Tools.bat' and choose 'Run as administrator',"
    Write-Warn "or launch PowerShell as Administrator and run:"
    Write-Warn "  PowerShell -ExecutionPolicy Bypass -File `".\QuickInstall.ps1`""
    Write-Host ""; Pause; exit 1
}

# ?? Step 1: Import certificate ???????????????????????????????
Write-Host "  Step 1: Importing certificate..." -ForegroundColor White

# Check if already trusted
$existingCert = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object {
    $_.Thumbprint -eq (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $cerFile.FullName).Thumbprint
}

if ($existingCert) {
    Write-Step "Certificate is already installed in Trusted People — skipping."
}
else {
    try {
        Import-Certificate -FilePath $cerFile.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        Write-Step "Certificate imported into Local Machine\Trusted People."
    }
    catch {
        Write-Err "Failed to import certificate: $_"
        Write-Host ""; Pause; exit 1
    }
}

# ?? Step 2: Install dependencies ?????????????????????????????
Write-Host "  Step 2: Installing dependencies..." -ForegroundColor White

$depsDir = Join-Path $ScriptDir "Dependencies"
$depsInstalled = 0

if (Test-Path $depsDir) {
    # Determine which architecture folders to use
    $arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
    $archDirs = @($arch)

    # x64 machines also need x86 dependencies; arm64 machines need arm64 + arm + x86
    if ($arch -eq "x64")  { $archDirs += "x86" }

    # Also check for architecture-neutral packages in the root Dependencies folder
    $neutralPackages = Get-ChildItem -Path $depsDir -Filter "*.msix" -File -ErrorAction SilentlyContinue
    $neutralPackages += Get-ChildItem -Path $depsDir -Filter "*.appx" -File -ErrorAction SilentlyContinue

    foreach ($pkg in $neutralPackages) {
        try {
            Write-Info "Installing dependency: $($pkg.Name)"
            Add-AppxPackage -Path $pkg.FullName -ErrorAction Stop
            $depsInstalled++
        }
        catch {
            # Dependency might already be installed — that's OK
            if ($_.Exception.Message -match "already installed|higher version") {
                Write-Step "$($pkg.Name) — already installed."
            }
            else {
                Write-Warn "Could not install $($pkg.Name): $($_.Exception.Message)"
            }
        }
    }

    foreach ($dir in $archDirs) {
        $dirPath = Join-Path $depsDir $dir
        if (Test-Path $dirPath) {
            $packages = Get-ChildItem -Path $dirPath -Filter "*.msix" -File -ErrorAction SilentlyContinue
            $packages += Get-ChildItem -Path $dirPath -Filter "*.appx" -File -ErrorAction SilentlyContinue

            foreach ($pkg in $packages) {
                try {
                    Write-Info "Installing dependency: $dir\$($pkg.Name)"
                    Add-AppxPackage -Path $pkg.FullName -ErrorAction Stop
                    $depsInstalled++
                }
                catch {
                    if ($_.Exception.Message -match "already installed|higher version") {
                        Write-Step "$($pkg.Name) — already installed."
                    }
                    else {
                        Write-Warn "Could not install $($pkg.Name): $($_.Exception.Message)"
                    }
                }
            }
        }
    }
}

if ($depsInstalled -eq 0) {
    Write-Step "Dependencies already satisfied."
}
else {
    Write-Step "$depsInstalled dependency package(s) installed."
}

# ?? Step 3: Install the app ??????????????????????????????????
Write-Host "  Step 3: Installing ADO Tools..." -ForegroundColor White

try {
    Add-AppxPackage -Path $msixFile.FullName -ForceApplicationShutdown -ErrorAction Stop
    Write-Step "ADO Tools installed successfully!"
}
catch {
    Write-Err "Failed to install package: $_"
    Write-Host ""
    Write-Warn "If you see 'Deployment failed with HRESULT: 0x80073CF3', try uninstalling"
    Write-Warn "the existing version first (Start menu -> right-click ADO Tools -> Uninstall)."
    Write-Host ""; Pause; exit 1
}

# ?? Done ?????????????????????????????????????????????????????
Write-Host ""
Write-Host "  ============================================" -ForegroundColor Green
Write-Host "    Installation complete!" -ForegroundColor Green
Write-Host "    Launch ADO Tools from the Start menu." -ForegroundColor Green
Write-Host "  ============================================" -ForegroundColor Green
Write-Host ""
Pause
