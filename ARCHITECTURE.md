# FoldersDiag Architecture

## Overview

FoldersDiag is a native Windows application written in modern C++17. It provides a professional folder size analysis tool with a modern UI and theme support.

## Project Structure

```
Folderdiag/
├── src/
│   ├── main.cpp              - Application entry point
│   ├── MainWindow.h/cpp      - Main application window
│   ├── FolderScanner.h/cpp   - Folder scanning engine
│   ├── FileSystemItem.h/cpp  - File/folder data model
│   ├── ThemeManager.h/cpp    - Theme management (dark/light)
│   ├── resource.h            - Resource definitions
│   ├── FoldersDiag.rc        - Resource script
│   └── app.ico              - Application icon
├── CMakeLists.txt           - CMake build configuration
├── build.bat               - Windows batch build script
├── build.ps1               - PowerShell build script
├── .gitignore              - Git ignore rules
├── LICENSE                 - MIT License
└── README.md               - Project documentation
```

## Component Details

### FileSystemItem
**Purpose**: Data model representing files and folders in the file system.

**Key Features**:
- Stores name, path, type (file/folder), size, and counts
- Supports hierarchical structure with parent-child relationships
- Provides formatted size display (B, KB, MB, GB, TB)
- Tracks file count and directory count for aggregation

**Usage**:
```cpp
auto folder = std::make_shared<FileSystemItem>(L"Documents", L"C:\\Users\\Documents", ItemType::Directory);
folder->SetSize(1024 * 1024 * 500); // 500 MB
std::wstring formatted = folder->GetSizeFormatted(); // "500.00 MB"
```

### FolderScanner
**Purpose**: Asynchronous folder scanning engine that traverses the file system.

**Key Features**:
- Asynchronous scanning using std::thread
- Progress callbacks for UI updates
- Cancellation support
- Recursive directory traversal
- Automatic size aggregation for folders

**Architecture**:
- Uses Win32 FindFirstFile/FindNextFile for efficient file enumeration
- Separate thread for scanning to keep UI responsive
- Bottom-up size calculation after scan completes

**Usage**:
```cpp
FolderScanner scanner;
scanner.ScanAsync(
    L"C:\\Users",
    [](const std::wstring& path) { /* progress */ },
    [](std::shared_ptr<FileSystemItem> root) { /* complete */ }
);
```

### ThemeManager
**Purpose**: Centralized theme management with singleton pattern.

**Key Features**:
- Singleton instance for global access
- Pre-defined color schemes for light and dark themes
- Automatic brush creation for painting
- DWM integration for modern window styling
- Support for Windows 10/11 dark mode title bars

**Color Schemes**:

**Dark Theme**:
- Background: RGB(30, 30, 30)
- Foreground: RGB(220, 220, 220)
- Header: RGB(45, 45, 48)
- Selected: RGB(0, 120, 215)

**Light Theme**:
- Background: RGB(255, 255, 255)
- Foreground: RGB(0, 0, 0)
- Header: RGB(240, 240, 240)
- Selected: RGB(0, 120, 215)

**Usage**:
```cpp
auto& theme = ThemeManager::Instance();
theme.SetTheme(ThemeManager::Theme::Dark);
theme.ApplyToWindow(hwnd);
```

### MainWindow
**Purpose**: Main application window with UI controls and event handling.

**Key Components**:
- **Menu Bar**: File and View menus
- **Toolbar**: Quick access buttons (Browse, Refresh, Theme)
- **ListView**: Multi-column view with sortable headers
- **Status Bar**: Progress and status messages

**Columns**:
1. Name - File/folder name with visual hierarchy
2. Size - Human-readable size (sorted)
3. Files - Number of files in folder
4. Folders - Number of subfolders
5. Path - Full file system path

**Event Handling**:
- WM_CREATE: Initialize controls
- WM_SIZE: Layout management
- WM_COMMAND: Menu and button actions
- WM_NOTIFY: ListView events (column clicks)
- WM_USER+1: Scan progress updates
- WM_USER+2: Scan completion

**Key Methods**:
- `BrowseFolder()`: Shows folder picker dialog
- `ScanFolder()`: Initiates async scan
- `PopulateListView()`: Displays scan results
- `SortBySize()`: Sorts items by size
- `ToggleTheme()`: Switches between themes

## Data Flow

1. **User Action**: User clicks "Browse" button
2. **Folder Selection**: System folder picker dialog
3. **Scan Initiation**: `ScanFolder()` called with selected path
4. **Async Scanning**: 
   - Background thread traverses file system
   - Progress callbacks update UI
   - Size aggregation performed bottom-up
5. **Completion**: 
   - Results posted to main thread
   - `PopulateListView()` displays data
   - ListView populated with all items
6. **Interaction**:
   - User clicks column headers to sort
   - Toggles theme as needed

## Build System

### CMake Configuration
- C++17 standard required
- Windows-only target
- Links against: comctl32, shlwapi, uxtheme, dwmapi
- WIN32 subsystem for GUI application

### Build Process
1. CMake generates Visual Studio solution
2. Compiler builds all .cpp files
3. Resource compiler processes .rc file
4. Linker creates FoldersDiag.exe
5. Output placed in build/bin/Release/

## Modern C++ Features Used

- **Smart Pointers**: std::shared_ptr, std::unique_ptr
- **Lambda Expressions**: Callbacks and sorting
- **Standard Containers**: std::vector, std::wstring
- **Threading**: std::thread, std::atomic
- **Type Safety**: enum class
- **Auto Type Deduction**: auto keyword
- **Range-based for loops**: for (const auto& item : items)

## Win32 API Integration

### Common Controls
- ListView with extended styles
- Toolbar with modern flat style
- Status bar with size grip
- Modern file dialog (IFileDialog)

### Desktop Window Manager
- DWM attributes for dark mode title bar
- Modern window styling
- Hardware-accelerated rendering

### File System
- FindFirstFile/FindNextFile for enumeration
- LARGE_INTEGER for 64-bit file sizes
- Win32 file attributes

## Performance Considerations

1. **Asynchronous Scanning**: UI remains responsive during long scans
2. **Efficient Enumeration**: Win32 APIs for fast file traversal
3. **Memory Management**: Smart pointers prevent leaks
4. **Double Buffering**: ListView uses LVS_EX_DOUBLEBUFFER
5. **Lazy Loading**: Only scanned folders are displayed

## Extensibility

Future enhancements could include:
- File type filtering
- Size threshold filtering
- Export to CSV/JSON
- Chart visualization
- Network drive support
- Custom color schemes
- Saved scan profiles
- Duplicate file detection

## Security Considerations

- No elevated privileges required
- Read-only file access
- User-controlled folder selection
- No network communication
- No data collection or telemetry
