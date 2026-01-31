# FoldersDiag Installer and Packaging

This directory contains setup and packaging files for distributing FoldersDiag.

## Version Information
- **Version**: 1.0.1
- **Publisher**: NovaDevelop
- **Copyright**: © 2026 NovaDevelop

## Distribution Methods

### 1. Inno Setup Installer (Traditional Windows Installer)

**Requirements:**
- Inno Setup 6.x or later (https://jrsoftware.org/isinfo.php)
- Built FoldersDiag.exe in `build/Release/`

**To create installer:**
```powershell
# Open setup.iss in Inno Setup Compiler and click Compile
# Or use command line:
iscc installer\setup.iss
```

**Output:**
- `build/installer/FoldersDiag-Setup-1.0.1.exe`

**Features:**
- Full installation wizard
- Desktop shortcut option
- Start menu entries
- Uninstaller
- Registry entries cleanup
- Windows 10 1809+ version check

### 2. Windows Store (MSIX Package)

**Requirements:**
- Windows SDK 10.0.17763 or later
- Built FoldersDiag.exe in `build/Release/`
- MakeAppx.exe (part of Windows SDK)

**To create MSIX package:**
```powershell
# Run the PowerShell script
.\installer\build-msix.ps1
```

**Output:**
- `build/msix/FoldersDiag_1.0.1.0_x64.msix`

**For Windows Store submission:**

1. **Sign the package** (required for Store):
   ```powershell
   signtool sign /fd SHA256 /a /f YourCertificate.pfx /p YourPassword "build\msix\FoldersDiag_1.0.1.0_x64.msix"
   ```

2. **Create a code signing certificate** (for testing):
   ```powershell
   New-SelfSignedCertificate -Type Custom -Subject "CN=NovaDevelop" -KeyUsage DigitalSignature -FriendlyName "NovaDevelop" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
   ```

3. **Upload to Windows Partner Center**:
   - Go to https://partner.microsoft.com/dashboard
   - Create new app submission
   - Upload the signed MSIX package
   - Complete store listing (description, screenshots, etc.)
   - Submit for certification

4. **Store Requirements**:
   - Screenshots (at least 1, recommended 4-5)
   - App description and features
   - Privacy policy URL (if collecting data)
   - Age ratings
   - Pricing tier

### 3. Portable ZIP Distribution

**For portable distribution without installer:**

```powershell
# Create a ZIP file
Compress-Archive -Path "build\Release\FoldersDiag.exe", "README.md", "LICENSE" -DestinationPath "build\FoldersDiag-1.0.1-Portable.zip"
```

## Package Contents

### Inno Setup Installer includes:
- FoldersDiag.exe
- README.md
- LICENSE
- Uninstaller
- Registry settings

### MSIX Package includes:
- FoldersDiag.exe
- Package.appxmanifest
- Assets (app icons, splash screen)

## Asset Requirements for Windows Store

To properly submit to Windows Store, create proper PNG assets:

**Required sizes:**
- Square44x44Logo.png (44x44px) - App list icon
- Square150x150Logo.png (150x150px) - Medium tile
- Wide310x150Logo.png (310x150px) - Wide tile
- StoreLogo.png (50x50px) - Store listing
- SplashScreen.png (620x300px) - App launch screen

**Optional but recommended:**
- Square71x71Logo.png (71x71px) - Small tile
- Square310x310Logo.png (310x310px) - Large tile

You can use tools like:
- Adobe Photoshop / Illustrator
- GIMP (free)
- Inkscape (free)
- Online tools: https://www.appicon.co/

## Code Signing

For production distribution, you need a code signing certificate:

**Options:**
1. **Commercial Certificate**: Purchase from DigiCert, Sectigo, etc. ($200-500/year)
2. **Microsoft Partner Network**: Free if enrolled in Partner program
3. **Self-signed**: For testing only, not for public distribution

**Sign the installer:**
```powershell
signtool sign /f YourCert.pfx /p YourPassword /t http://timestamp.digicert.com "build\installer\FoldersDiag-Setup-1.0.1.exe"
```

## Testing Installation

**Inno Setup Installer:**
```powershell
# Install
.\build\installer\FoldersDiag-Setup-1.0.1.exe /VERYSILENT

# Uninstall
"C:\Program Files\FoldersDiag\unins000.exe" /VERYSILENT
```

**MSIX Package:**
```powershell
# Install (developer mode or signed)
Add-AppxPackage -Path "build\msix\FoldersDiag_1.0.1.0_x64.msix"

# Uninstall
Remove-AppxPackage -Package "NovaDevelop.FoldersDiag_1.0.1.0_x64__8wekyb3d8bbwe"
```

## Troubleshooting

**Error: "makeappx.exe not found"**
- Install Windows SDK from https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/

**Error: "Cannot install unsigned package"**
- Enable Developer Mode in Windows Settings
- Or sign the package with a certificate

**Error: "App not launching after install"**
- Check Windows Event Viewer for errors
- Verify all dependencies are included
- Test on clean Windows VM

## Version Updates

When releasing a new version:

1. Update version in `CMakeLists.txt`:
   ```cmake
   project(FoldersDiag VERSION 1.0.2 LANGUAGES CXX)
   ```

2. Update version in `setup.iss`:
   ```
   #define MyAppVersion "1.0.2"
   ```

3. Update version in `Package.appxmanifest`:
   ```xml
   <Identity Version="1.0.2.0" />
   ```

4. Update version in About dialog resource (`FoldersDiag.rc`)

5. Rebuild and repackage

## Support

For issues with packaging:
- Check the build logs in `build/` directory
- Verify Windows SDK is installed correctly
- Ensure Visual Studio 2019+ is installed with C++ workload

For Windows Store specific issues:
- Visit Windows Partner Center documentation
- Check certification requirements: https://docs.microsoft.com/en-us/windows/uwp/publish/
