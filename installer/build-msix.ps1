# PowerShell script to build Windows Store (MSIX) package
# This creates an MSIX package suitable for Windows Store submission

param(
    [string]$BuildConfig = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "FoldersDiag MSIX Package Builder" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if MakeAppx.exe is available
$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"
if (-not (Test-Path $makeAppx)) {
    # Try to find it in PATH
    $makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $makeAppx) {
        Write-Host "Error: makeappx.exe not found. Please install Windows SDK." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Using MakeAppx: $makeAppx" -ForegroundColor Green

# Set paths
$rootDir = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $rootDir "build\$BuildConfig"
$packageDir = Join-Path $rootDir "build\package"
$outputDir = Join-Path $rootDir "build\msix"

# Create directories
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path "$packageDir\Assets" | Out-Null

Write-Host "Copying files to package directory..." -ForegroundColor Yellow

# Copy executable
$exePath = Join-Path $buildDir "FoldersDiag.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "Error: FoldersDiag.exe not found at $exePath" -ForegroundColor Red
    Write-Host "Please build the project first using build.bat or CMake" -ForegroundColor Yellow
    exit 1
}
Copy-Item $exePath $packageDir -Force

# Copy manifest
Copy-Item (Join-Path $rootDir "Package.appxmanifest") $packageDir -Force

# Copy icon as placeholder assets (in a real deployment, create proper sized assets)
$iconPath = Join-Path $rootDir "src\app.ico"
if (Test-Path $iconPath) {
    Copy-Item $iconPath "$packageDir\Assets\Square44x44Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\Square150x150Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\Wide310x150Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\StoreLogo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\SplashScreen.png" -Force
}

Write-Host "Creating MSIX package..." -ForegroundColor Yellow

# Create the MSIX package
$msixPath = Join-Path $outputDir "FoldersDiag_1.0.1.0_x64.msix"
& $makeAppx pack /d $packageDir /p $msixPath /o

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "MSIX package created at: $msixPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps for Windows Store:" -ForegroundColor Cyan
    Write-Host "1. Sign the package with a certificate" -ForegroundColor White
    Write-Host "   signtool sign /fd SHA256 /a /f YourCert.pfx /p YourPassword `"$msixPath`"" -ForegroundColor Gray
    Write-Host "2. Upload to Windows Partner Center" -ForegroundColor White
    Write-Host "3. Complete store listing and submit for certification" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "Error creating MSIX package" -ForegroundColor Red
    exit 1
}
