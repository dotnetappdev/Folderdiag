# FoldersDiag - Folder Size Analyzer

A modern Windows file explorer application that analyzes and displays the biggest file sizes and directories in an organized column view.

## 📚 Documentation

- **[Quick Start Guide](QUICKSTART.md)** - Get started in 60 seconds
- **[User Guide](USER_GUIDE.md)** - Comprehensive user manual
- **[Architecture](ARCHITECTURE.md)** - Technical architecture details
- **[UI Design](UI_DESIGN.md)** - Visual design documentation
- **[Contributing](CONTRIBUTING.md)** - How to contribute
- **[Project Summary](PROJECT_SUMMARY.md)** - Complete project overview

## Features

- 📊 **Folder Size Analysis**: Scans directories and calculates total sizes including all subdirectories
- 📈 **Sortable Columns**: View files and folders sorted by size, name, or other attributes
- 🎨 **Modern UI**: Clean, modern interface with professional styling
- 🌓 **Dark/Light Mode**: Toggle between dark and light themes
- 📁 **Windows 11 Style Navigation**: Left pane with special folders (Desktop, Documents, Downloads) and drives
- 📊 **Visual Size Bars**: Color-coded progress bars showing relative folder sizes
- 🎀 **Windows 11 Ribbon**: Modern ribbon interface with Home, View, and Share tabs
- 🖱️ **Shell Context Menus**: Native Windows context menus with shell extension support
- ⚡ **Fast Scanning**: Asynchronous scanning with progress updates
- 💻 **Native Performance**: Written in modern C++ for optimal speed

## Screenshots

### Main Application Interface
The application features a modern Windows 11-style interface with dual-pane layout, visual size bars, and comprehensive folder analysis.

![FoldersDiag Application](https://github.com/user-attachments/assets/367c93c2-b035-482a-886f-acc112f95218)

*Main window showing:*
- **Left Pane**: TreeView with special folders (Desktop, Documents, Downloads, etc.) and drive hierarchy
- **Right Pane**: Detailed list view with sortable columns (Name, Size, Percent, Files, Folders, etc.)
- **Visual Size Bars**: Color-coded progress bars in the Percent column showing relative sizes
- **Context Menu**: Right-click support with native Windows shell integration (Locate in Explorer, Properties, Delete, etc.)
- **Bottom Chart**: Visual bar chart showing folder size distribution

### Key Features Visible
- ✅ **Special Folders**: Desktop, Documents, Downloads, Music, Pictures, Videos in left pane
- ✅ **This PC Integration**: Drives (C:, D:) with volume labels and size information
- ✅ **Size Visualization**: Percentage bars with color coding for easy size comparison
- ✅ **Shell Integration**: Native Windows context menus with all file operations
- ✅ **Hierarchical View**: Expandable folder tree showing nested structure
- ✅ **Status Information**: Drive sizes and usage statistics at the bottom

> **Note**: To capture your own screenshots with the latest features, build and run the application on Windows, and follow the instructions in [docs/screenshots/CAPTURE_GUIDE.md](docs/screenshots/CAPTURE_GUIDE.md).

## Building the Application

### Prerequisites

- Windows 10 or later
- Visual Studio 2019 or later (with C++ Desktop Development workload)
- CMake 3.15 or later

### Build Steps

1. Clone the repository:
```bash
git clone https://github.com/dotnetappdev/Folderdiag.git
cd Folderdiag
```

2. Create a build directory:
```bash
mkdir build
cd build
```

3. Generate build files with CMake:
```bash
cmake ..
```

4. Build the project:
```bash
cmake --build . --config Release
```

5. The executable will be in `build/bin/Release/FoldersDiag.exe`

### Using Visual Studio

Alternatively, you can open the project directly in Visual Studio 2019 or later:
1. Open Visual Studio
2. Select "Open a local folder"
3. Navigate to the cloned repository
4. Visual Studio will automatically detect CMakeLists.txt
5. Build using F7 or Build > Build Solution

## Usage

1. Launch FoldersDiag.exe
2. Click "Browse" or use File > Browse Folder (Ctrl+O)
3. Select a folder to analyze
4. Wait for the scan to complete
5. Click on column headers to sort by that attribute
6. Use View > Toggle Dark Mode (Ctrl+D) to switch themes

## Columns

The application displays comprehensive folder information in sortable columns:

- **Name**: File or folder name with expandable hierarchy
- **Size**: Total size in human-readable format (B, KB, MB, GB, TB)
- **Percent**: Visual progress bar showing relative size percentage
- **Files**: Number of files in the folder
- **Folders**: Number of subfolders
- **Created**: Creation date and time
- **Modified**: Last modification date and time
- **Accessed**: Last access date and time
- **Attributes**: File attributes (Hidden, System, Read-only, etc.)
- **Owner**: File owner information

Click any column header to sort by that attribute.

## Technical Details

### Architecture

- **MainWindow**: Main UI window with dual-pane layout (TreeView + ListView)
- **RibbonBar**: Windows 11 style ribbon interface with tabs and command groups
- **FileSystemItem**: Data structure representing files and folders
- **FolderScanner**: Asynchronous folder scanning engine
- **ThemeManager**: Centralized theme management for dark/light modes
- **Settings**: User preferences and color customization

### Key Components

#### Navigation
- **TreeView**: Left pane showing special folders (Desktop, Documents, Downloads) and drives
- **Special Folders**: Integration with Windows shell special folders (CSIDL)
- **Drive Enumeration**: Automatic detection of fixed, removable, and RAM drives

#### Display
- **ListView**: Right pane with detailed file/folder information
- **Size Bars**: Custom-drawn progress bars with gradient colors
- **Sorting**: Multi-column sorting support
- **Context Menus**: Native Windows shell context menu integration (IContextMenu)

#### Performance
- **Asynchronous Scanning**: Non-blocking folder analysis
- **Lazy Loading**: TreeView subdirectories loaded on-demand
- **Memory Management**: Proper cleanup of allocated resources

### Technologies

- Modern C++17
- Win32 API for native Windows integration
- Common Controls (TreeView, ListView, StatusBar)
- Windows Shell COM interfaces (IShellFolder, IContextMenu)
- Desktop Window Manager (DWM) API for modern window styling
- GDI+ for gradient rendering
- Asynchronous scanning with std::thread

## License

MIT License - See LICENSE file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.