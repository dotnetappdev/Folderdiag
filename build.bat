@echo off
REM Build script for FoldersDiag

echo Building FoldersDiag...

if not exist build mkdir build
cd build

echo Running CMake...
cmake .. -G "Visual Studio 16 2019" -A x64

if errorlevel 1 (
    echo CMake configuration failed!
    exit /b 1
)

echo Building Release configuration...
cmake --build . --config Release

if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo.
echo Build successful!
echo Executable location: build\bin\Release\FoldersDiag.exe
echo.

cd ..
