# Version 1.0.1 Release Notes

**Release Date**: January 31, 2026  
**Publisher**: NovaDevelop  
**Build**: 1.0.1.0

## What's New

### About Dialog
- New **Help → About FoldersDiag** menu item
- Professional About dialog showing:
  - Application version 1.0.1
  - Feature highlights
  - Copyright © 2026 NovaDevelop
  - Modern, centered layout

### Windows Store Packaging
Complete Microsoft Store submission support:
- **MSIX Package**: Store-ready application package
- **Automated Build**: PowerShell script for one-click packaging
- **Proper Manifest**: Complete Package.appxmanifest with NovaDevelop identity
- **Requirements**: Windows 10 version 1809 (Build 17763) or later

**To create MSIX package:**
```powershell
.\installer\build-msix.ps1
```

**Output**: `build/msix/FoldersDiag_1.0.1.0_x64.msix`

### Traditional Windows Installer
Inno Setup configuration for standard Windows installation:
- Full installation wizard with modern UI
- Desktop shortcut option
- Start menu integration
- Professional uninstaller
- Registry cleanup on uninstall
- Automatic Windows version checking

**To create installer (requires Inno Setup):**
```powershell
iscc installer\setup.iss
```

**Output**: `build/installer/FoldersDiag-Setup-1.0.1.exe`

### Distribution Methods

Three ways to distribute FoldersDiag:

1. **Windows Store (MSIX)**
   - Enterprise-friendly
   - Auto-updates
   - Sandboxed installation
   - Best for Store submission

2. **Inno Setup Installer**
   - Traditional Windows installer
   - Full system integration
   - Administrator privileges
   - Best for direct distribution

3. **Portable ZIP**
   - No installation required
   - USB-friendly
   - User-level only
   - Best for testing

## Complete Feature Set

### Core Features
- **Dual-Pane Layout**: TreeView (left) + ListView (right)
- **Folder Size Analysis**: Recursive size calculation with aggregation
- **Visual Progress Bars**: Color-coded size visualization
- **RGB Color Customization**: 6 customizable color zones
- **Multiple View Modes**: Details, List, Icons
- **Theme Support**: Light and Dark modes
- **Windows 11 Ribbon**: Modern tab-based interface
- **Interactive Splitter**: Drag-to-resize panes

### Technical Highlights
- Modern C++17 codebase
- Win32 API with Common Controls
- Asynchronous folder scanning
- Thread-safe operations
- DWM integration for title bar styling
- Persistent user settings (INI format)
- No external dependencies

### User Experience
- Real-time size updates during scanning
- Column sorting by size, name, etc.
- Keyboard shortcuts (Ctrl+D for theme, F5 for refresh)
- Status bar with real-time progress
- Professional Windows 11 styling
- Smooth animations and gradients

## System Requirements

**Minimum:**
- Windows 10 Version 1809 (Build 17763) or later
- 64-bit processor
- 4 MB disk space
- 512 MB RAM

**Recommended:**
- Windows 11
- Modern multi-core processor
- SSD for faster scanning

## Building from Source

**Prerequisites:**
- Visual Studio 2019 or later with C++ Desktop Development
- CMake 3.15 or later
- Windows SDK 10.0.17763 or later

**Build Commands:**
```batch
# Quick build
build.bat

# Or manual CMake
mkdir build && cd build
cmake ..
cmake --build . --config Release
```

**Output**: `build/Release/FoldersDiag.exe`

## Creating Packages

### MSIX Package for Windows Store

1. Build the application:
   ```powershell
   .\build.bat
   ```

2. Create MSIX package:
   ```powershell
   .\installer\build-msix.ps1
   ```

3. Sign the package (required for Store):
   ```powershell
   signtool sign /fd SHA256 /a /f YourCert.pfx /p Password "build\msix\FoldersDiag_1.0.1.0_x64.msix"
   ```

4. Submit to Windows Partner Center:
   - Upload signed MSIX
   - Complete store listing
   - Add screenshots (minimum 1, recommended 4-5)
   - Submit for certification

### Traditional Installer

1. Build the application (see above)

2. Install Inno Setup from https://jrsoftware.org/isinfo.php

3. Compile installer:
   ```powershell
   iscc installer\setup.iss
   ```

4. (Optional) Sign the installer:
   ```powershell
   signtool sign /f YourCert.pfx /p Password "build\installer\FoldersDiag-Setup-1.0.1.exe"
   ```

## Known Issues

None currently identified.

## Future Enhancements

Planned for future versions:
- Phase 5: File operations (copy, move, delete, rename)
- Context menu integration
- Hidden files toggle
- Export size reports (CSV, HTML)
- Network drive support improvements
- Multi-language support
- Real-time folder monitoring

## Documentation

Complete documentation available:
- **README.md**: Project overview and quick start
- **QUICKSTART.md**: Getting started guide
- **USER_GUIDE.md**: Complete user manual
- **ARCHITECTURE.md**: Technical architecture
- **installer/README.md**: Packaging and distribution guide

## Support

For issues, questions, or feature requests:
- GitHub Issues: https://github.com/dotnetappdev/Folderdiag/issues
- Discussions: https://github.com/dotnetappdev/Folderdiag/discussions

## License

MIT License - See LICENSE file for details

## Credits

**Developed by**: NovaDevelop  
**Copyright**: © 2026 NovaDevelop  
**All rights reserved**

## Version History

### 1.0.1 (January 31, 2026)
- Added About dialog
- Created Windows Store packaging
- Created Inno Setup installer
- Updated branding to NovaDevelop

### 1.0.0 (January 31, 2026)
- Initial release
- All phases (1-4) implemented
- Complete feature set
- Professional Windows 11 UI

---

**Thank you for using FoldersDiag!**
