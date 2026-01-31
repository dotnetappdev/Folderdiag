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
$makeAppx = $null
$sdkBasePath = "C:\Program Files (x86)\Windows Kits\10\bin"

# Try to find the latest SDK version with makeappx
if (Test-Path $sdkBasePath) {
    $sdkVersions = Get-ChildItem $sdkBasePath -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | Sort-Object -Descending
    foreach ($version in $sdkVersions) {
        $makeAppxPath = Join-Path $version.FullName "x64\makeappx.exe"
        if (Test-Path $makeAppxPath) {
            $makeAppx = $makeAppxPath
            break
        }
    }
}

if (-not $makeAppx) {
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

# Copy icon as placeholder assets
# NOTE: In production, create proper PNG assets at correct sizes (44x44, 150x150, 310x150, etc.)
# For now, we'll note that proper assets are needed
$iconPath = Join-Path $rootDir "src\app.ico"
if (Test-Path $iconPath) {
    Write-Host "WARNING: Using .ico as placeholder. Create proper PNG assets for production!" -ForegroundColor Yellow
    Write-Host "  Required: Square44x44Logo.png, Square150x150Logo.png, Wide310x150Logo.png, etc." -ForegroundColor Yellow
    # Copy as placeholders - Windows will display them but they won't be optimal
    Copy-Item $iconPath "$packageDir\Assets\Square44x44Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\Square150x150Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\Wide310x150Logo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\StoreLogo.png" -Force
    Copy-Item $iconPath "$packageDir\Assets\SplashScreen.png" -Force
} else {
    Write-Host "WARNING: Icon not found. Creating dummy assets." -ForegroundColor Yellow
    # Create minimal placeholder files if icon doesn't exist
    for ($i = 0; $i -lt 100; $i++) {
        [System.IO.File]::WriteAllBytes("$packageDir\Assets\Square44x44Logo.png", (1..$i))
    }
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
