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
- 📁 **Detailed View**: Shows file count, folder count, and full paths
- ⚡ **Fast Scanning**: Asynchronous scanning with progress updates
- 💻 **Native Performance**: Written in modern C++ for optimal speed

## Screenshots

### Light Mode
The application features a clean, modern interface with detailed file and folder information displayed in columns.

### Dark Mode
Easy-to-read dark theme for reduced eye strain during extended use.

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

- **Name**: File or folder name (with indentation for hierarchy)
- **Size**: Total size in human-readable format (B, KB, MB, GB, TB)
- **Files**: Number of files in the folder
- **Folders**: Number of subfolders
- **Path**: Full path to the item

## Technical Details

### Architecture

- **FileSystemItem**: Data structure representing files and folders
- **FolderScanner**: Asynchronous folder scanning engine
- **ThemeManager**: Centralized theme management for dark/light modes
- **MainWindow**: Main UI window with Win32 controls

### Technologies

- Modern C++17
- Win32 API for native Windows integration
- Common Controls (ListView, Toolbar, StatusBar)
- Desktop Window Manager (DWM) API for modern window styling
- Asynchronous scanning with std::thread

## License

MIT License - See LICENSE file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.