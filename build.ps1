# Build script for FoldersDiag (PowerShell)

Write-Host "Building FoldersDiag..." -ForegroundColor Green

if (-not (Test-Path "build")) {
    New-Item -ItemType Directory -Path "build" | Out-Null
}

Set-Location build

Write-Host "Running CMake..." -ForegroundColor Cyan
cmake .. -G "Visual Studio 16 2019" -A x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "CMake configuration failed!" -ForegroundColor Red
    Set-Location ..
    exit 1
}

Write-Host "Building Release configuration..." -ForegroundColor Cyan
cmake --build . --config Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Set-Location ..
    exit 1
}

Write-Host ""
Write-Host "Build successful!" -ForegroundColor Green
Write-Host "Executable location: build\bin\Release\FoldersDiag.exe" -ForegroundColor Yellow
Write-Host ""

Set-Location ..
