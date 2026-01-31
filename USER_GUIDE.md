# User Guide - FoldersDiag

## Getting Started

### Installation

FoldersDiag is a portable application that doesn't require installation. Simply:

1. Download the latest release from the [Releases page](https://github.com/dotnetappdev/Folderdiag/releases)
2. Extract the ZIP file to any location
3. Run `FoldersDiag.exe`

Alternatively, build from source following the instructions in README.md.

### First Time Use

1. **Launch the application**: Double-click `FoldersDiag.exe`
2. **Browse for a folder**: Click the "Browse" button or use `File > Browse Folder` (Ctrl+O)
3. **Select a folder**: Choose any folder you want to analyze
4. **Wait for scanning**: The application will scan the folder and all subdirectories
5. **View results**: Once complete, you'll see all files and folders sorted by size

## Main Interface

### Menu Bar

#### File Menu
- **Browse Folder... (Ctrl+O)**: Open folder picker to select a folder for analysis
- **Refresh (F5)**: Re-scan the current folder
- **Exit**: Close the application

#### View Menu
- **Toggle Dark Mode (Ctrl+D)**: Switch between light and dark themes

### Toolbar

Quick access buttons for common actions:
- **Browse**: Select a folder to analyze
- **Refresh**: Re-scan the current folder
- **Toggle Theme**: Switch between light/dark modes

### ListView Columns

The main view displays information in five columns:

1. **Name**: 
   - File or folder name
   - Folders are displayed with indentation to show hierarchy
   - Items sorted from largest to smallest by default

2. **Size**: 
   - Total size in human-readable format
   - For folders: includes all files and subfolders
   - Automatically formatted (B, KB, MB, GB, TB)
   - Click header to sort by size

3. **Files**: 
   - Number of files in a folder
   - For files: displays 0
   - Includes files in all subfolders

4. **Folders**: 
   - Number of subfolders
   - For files: displays 0
   - Includes nested subfolders

5. **Path**: 
   - Full path to the file or folder
   - Useful for locating items on disk

### Status Bar

Located at the bottom of the window:
- Shows current operation status
- Displays scan progress
- Shows summary after scan completion

## Features in Detail

### Folder Scanning

**How it works**:
- FoldersDiag recursively scans all files and folders
- Calculates sizes for all items
- Aggregates sizes for folders (includes all contents)
- Updates progress in real-time

**What's included**:
- All files in selected folder
- All subfolders (recursively)
- Hidden files and folders
- System files (if accessible)

**What's excluded**:
- Inaccessible folders (permission denied)
- Symbolic links (to prevent infinite loops)

### Sorting

**Sort by Size**:
- Click the "Size" column header
- First click: Largest to smallest (descending)
- Second click: Smallest to largest (ascending)
- Useful for finding what's taking up space

**Other columns**:
- Currently, only size sorting is interactive
- Other columns can be viewed but not sorted

### Theme Support

**Light Mode** (Default):
- White background
- Black text
- Traditional Windows appearance
- Easy to read in bright environments

**Dark Mode**:
- Dark gray background
- Light text
- Reduced eye strain
- Better for low-light environments
- Modern appearance

**Switching Themes**:
- Use `View > Toggle Dark Mode`
- Press `Ctrl+D`
- Click "Toggle Theme" toolbar button
- Changes apply immediately

## Common Tasks

### Finding Large Files

1. Scan a folder
2. Click the "Size" column header to sort
3. Largest items appear at the top
4. Check the "Path" column to locate files

### Analyzing Disk Space

1. Scan your user folder or entire drive (e.g., C:\)
2. Review folder sizes in the "Size" column
3. Identify folders consuming the most space
4. Navigate to those folders in File Explorer to clean up

### Comparing Folder Sizes

1. Scan a parent folder containing multiple subfolders
2. Sort by size
3. Compare sizes of subdirectories
4. Identify which folders are largest

### Monitoring Growth

1. Scan a folder and note the size
2. Use "Refresh" (F5) later to re-scan
3. Compare before and after sizes
4. Track which folders are growing

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Browse for folder |
| F5 | Refresh current scan |
| Ctrl+D | Toggle dark/light mode |
| Alt+F4 | Exit application |

## Performance Tips

### For Best Performance

**Small to medium folders** (< 10,000 files):
- Scanning is nearly instantaneous
- All features work smoothly

**Large folders** (10,000 - 100,000 files):
- Scan may take 10-30 seconds
- UI remains responsive during scan
- Consider scanning subfolders individually

**Very large folders** (> 100,000 files):
- Scan may take several minutes
- Watch the status bar for progress
- Consider scanning smaller portions

### What Affects Speed

- **Number of files**: More files = longer scan
- **Folder depth**: Deeply nested folders take longer
- **Drive type**: SSD is faster than HDD
- **Network drives**: Much slower than local drives
- **Antivirus**: May slow down file access

### Optimizing Scans

1. **Start specific**: Scan only the folder you need
2. **Avoid network drives**: Local drives are much faster
3. **Close other programs**: Reduces disk contention
4. **Use SSD**: Significantly faster than HDD

## Troubleshooting

### "Access Denied" Messages

**Problem**: Some folders show 0 size or missing files

**Cause**: Windows security prevents access to certain folders

**Solution**: 
- Run as Administrator (right-click > Run as administrator)
- Some system folders require elevation
- Note: Not recommended for regular use

### Scan Takes Too Long

**Problem**: Scanning seems to hang or take forever

**Cause**: Very large folder or slow drive

**Solutions**:
- Scan smaller subfolders instead
- Check status bar for progress
- Ensure drive is healthy
- Close other disk-intensive programs

### Application Doesn't Start

**Problem**: Double-clicking does nothing or shows error

**Possible causes and solutions**:
1. **Missing dependencies**: Install Visual C++ Redistributable
2. **Incompatible Windows version**: Requires Windows 10 or later
3. **Corrupted file**: Re-download the application
4. **Antivirus blocking**: Add exception for FoldersDiag.exe

### Theme Not Changing

**Problem**: Dark mode toggle doesn't work

**Cause**: Windows version or settings

**Solution**:
- Requires Windows 10 1809 or later
- Window title bar may not change on older Windows
- Rest of UI should still change correctly

## Tips and Tricks

### Finding Duplicate Folders

While FoldersDiag doesn't detect duplicates automatically:
1. Scan parent folder
2. Look for folders with identical sizes
3. Manually check those folders for duplicates

### Quick Cleanup Workflow

1. Scan your Downloads folder
2. Sort by size (largest first)
3. Note the paths of large items
4. Open in File Explorer and delete as needed
5. Use Refresh to verify cleanup

### Regular Maintenance

Run FoldersDiag monthly on:
- Downloads folder
- Documents folder
- Desktop
- AppData\Local\Temp

This helps keep your system clean and fast.

### Favorite Locations

Common folders to analyze:
- `C:\Users\[YourName]\Downloads`
- `C:\Users\[YourName]\AppData\Local\Temp`
- `C:\Users\[YourName]\Documents`
- `C:\Program Files`
- `C:\Windows\Temp` (requires admin)

## Privacy and Security

### What Data is Collected?

**None.** FoldersDiag:
- Does not connect to the internet
- Does not send any data anywhere
- Does not collect telemetry
- Does not store history or logs
- Does not modify any files (read-only)

### Is it Safe?

Yes:
- Open source - you can review the code
- No elevated privileges needed (usually)
- Only reads file metadata
- Does not modify, move, or delete files
- No DLL injection or system hooks

### Permissions

FoldersDiag only needs:
- Read access to folders you select
- Standard user permissions
- No admin rights (unless scanning system folders)

## Frequently Asked Questions

### Q: Can I scan network drives?

**A**: Yes, but they're slower than local drives. Scanning large network folders may take considerable time.

### Q: Does it work on Windows 11?

**A**: Yes, fully compatible with Windows 11.

### Q: Can I scan my entire C: drive?

**A**: Yes, but it may take a long time. Some system folders may require administrator privileges.

### Q: Will this delete files?

**A**: No. FoldersDiag is read-only and never modifies, moves, or deletes files.

### Q: Can I export the results?

**A**: Not in the current version. This feature may be added in future releases.

### Q: Does it show hidden files?

**A**: Yes, all files are included in the scan, including hidden and system files.

### Q: Why are some folder sizes different from Windows Explorer?

**A**: 
- FoldersDiag shows actual file sizes
- Windows Explorer may show "Size on disk" (with cluster overhead)
- Both are correct, just measuring different things

### Q: Can I cancel a scan in progress?

**A**: Not in the current version. You can close the application or start a new scan.

## Support

### Getting Help

If you encounter issues:
1. Check this User Guide
2. Review the troubleshooting section
3. Check existing GitHub Issues
4. Create a new issue with details

### Contributing

FoldersDiag is open source! Contributions welcome:
- Report bugs
- Suggest features
- Submit pull requests
- Improve documentation

Visit: https://github.com/dotnetappdev/Folderdiag

## Version History

### Version 1.0.0
- Initial release
- Folder scanning and analysis
- Dark/Light theme support
- ListView with sortable columns
- Modern Windows UI
