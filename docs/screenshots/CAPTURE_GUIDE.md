# Screenshot Capture Guide for FoldersDiag

This guide provides step-by-step instructions for capturing high-quality screenshots of the FoldersDiag application.

## Prerequisites

1. **Built Application**: Ensure you have successfully built FoldersDiag.exe
   - Follow the build instructions in the main README.md
   - The executable should be in `build/bin/Release/FoldersDiag.exe`

2. **Test Data**: Have a folder with diverse content ready for scanning
   - Recommended: Use your Documents folder or a project folder
   - Should contain multiple levels of subdirectories
   - Should have varied file sizes for better demonstration

3. **Screenshot Tool**: Use Windows built-in tools:
   - **Snipping Tool** (Windows 10/11) - Recommended
   - **Snip & Sketch** (Windows 10/11) 
   - **Alt + PrtScn** for active window capture

## Step-by-Step Instructions

### Screenshot 1: Light Mode (Main View)

**Filename**: `light-mode-main.png`

1. **Launch the Application**
   ```cmd
   cd build\bin\Release
   FoldersDiag.exe
   ```

2. **Ensure Light Mode is Active**
   - Light mode is the default theme
   - If in dark mode, press `Ctrl+D` or click "Toggle Theme" button

3. **Scan a Folder**
   - Click "Browse" button or press `Ctrl+O`
   - Select a folder with good content (e.g., Documents, Downloads, or a project folder)
   - Wait for the scan to complete
   - The ListView should show multiple items with varied sizes

4. **Arrange the Window**
   - Resize window to approximately 1280x720 pixels for consistency
   - Ensure all UI elements are visible: toolbar, columns, status bar
   - Make sure some data rows are visible in the ListView

5. **Capture the Screenshot**
   - Open Snipping Tool and select "Window Snip" or use `Alt + PrtScn`
   - Capture the entire application window including borders
   - Save as `light-mode-main.png` in the `docs/screenshots/` directory

6. **Verify the Screenshot Shows**:
   - ✅ Application title bar
   - ✅ Menu bar (File, View)
   - ✅ Toolbar with Browse, Refresh, and Toggle Theme buttons
   - ✅ ListView with column headers (Name, Size, Files, Folders, Path)
   - ✅ Multiple data rows showing folder analysis results
   - ✅ Hierarchical indentation visible (e.g., subfolder under parent folder)
   - ✅ Status bar with completion message and statistics
   - ✅ Clear, readable text with good contrast

### Screenshot 2: Dark Mode (Main View)

**Filename**: `dark-mode-main.png`

1. **Toggle to Dark Mode**
   - With the same scan results still displayed
   - Click the "Toggle Theme" button OR press `Ctrl+D`
   - The interface should immediately switch to dark theme

2. **Verify Dark Theme is Applied**
   - Background should be dark gray (#1E1E1E)
   - Text should be light gray (#DCDCDC)
   - All UI elements should have dark theme colors

3. **Capture the Screenshot**
   - Use Snipping Tool or `Alt + PrtScn`
   - Capture the entire application window
   - Save as `dark-mode-main.png` in the `docs/screenshots/` directory

4. **Verify the Screenshot Shows**:
   - ✅ Same layout as light mode but with dark theme colors
   - ✅ Clearly demonstrates the theme toggle functionality
   - ✅ Text remains readable against dark background
   - ✅ All UI elements visible and properly themed

### Optional Screenshot 3: Scanning in Progress

**Filename**: `scanning-progress.png` (Optional)

1. **Prepare for Quick Capture**
   - Close and reopen FoldersDiag.exe
   - Have Snipping Tool ready
   - Choose a large folder that will take 5-10 seconds to scan

2. **Start Scan and Capture**
   - Click Browse and select the folder
   - Quickly capture a screenshot while scanning is in progress
   - Status bar should show "Scanning: [current path]"

3. **What to Show**:
   - ✅ Status bar displaying current scanning path
   - ✅ ListView potentially being populated with items
   - ✅ Application responsive during scan

### Optional Screenshot 4: Folder Browser Dialog

**Filename**: `folder-browser.png` (Optional)

1. **Open the Folder Browser**
   - Click "Browse" button or press `Ctrl+O`
   - The Windows folder browser dialog appears

2. **Capture the Dialog**
   - Capture the folder browser dialog showing the folder tree
   - Save as `folder-browser.png`

## Screenshot Quality Guidelines

### Technical Requirements
- **Format**: PNG (preferred) or JPG
- **Resolution**: Minimum 1280x720, higher is better
- **Quality**: High quality, no compression artifacts
- **Color Depth**: 24-bit color (True Color)

### Content Requirements
- **Window Size**: Large enough to show all UI elements clearly
- **Data Variety**: Show folders with different sizes (KB, MB, GB)
- **Hierarchy**: Demonstrate folder hierarchy with indentation
- **Real Data**: Use actual folders, not made-up data
- **Clean UI**: No overlapping windows or distractions

### Best Practices
- ✅ Capture during good scan results (not empty folder)
- ✅ Show both large and small files/folders
- ✅ Include some expanded folders showing subfolders
- ✅ Ensure status bar has informative message
- ✅ Make sure window is in focus (active)
- ✅ Clean desktop background if visible
- ❌ Don't include sensitive information in paths or filenames
- ❌ Avoid desktop clutter in the background
- ❌ Don't use test data that looks fake

## After Capturing Screenshots

### 1. Replace Placeholder Files

Delete the placeholder SVG files and add your PNG files:
```bash
cd docs/screenshots
rm light-mode-placeholder.svg dark-mode-placeholder.svg
# Add your PNG files (light-mode-main.png, dark-mode-main.png)
```

### 2. Update README.md

Update the main README.md to reference PNG files instead of SVG:

Change from:
```markdown
![FoldersDiag Light Mode](docs/screenshots/light-mode-placeholder.svg)
```

To:
```markdown
![FoldersDiag Light Mode](docs/screenshots/light-mode-main.png)
```

And similarly for dark mode.

### 3. Remove the Placeholder Note

Remove or update the note at the bottom of the Screenshots section in README.md since you now have real screenshots.

### 4. Verify in GitHub

1. Commit and push the new screenshot files
2. View the README.md on GitHub to ensure images display correctly
3. Check that images are appropriately sized and readable

## Troubleshooting

### Screenshot is Too Large
- GitHub will automatically scale images in markdown
- If needed, resize to 1920px width maximum using an image editor

### Screenshot is Blurry
- Ensure you're capturing at native resolution
- Use PNG format instead of JPG
- Don't resize up, only resize down if needed

### Text is Not Readable
- Increase window size before capture
- Ensure high DPI scaling is not affecting capture
- Use a higher resolution display if available

### Colors Look Wrong
- Verify your display color profile is set correctly
- Use PNG format to preserve colors accurately
- Capture in good lighting conditions

## Example Command-Line Workflow

```cmd
# Build the application
cd /path/to/FoldersDiag
mkdir build && cd build
cmake ..
cmake --build . --config Release

# Run the application
cd bin\Release
start FoldersDiag.exe

# After capturing screenshots manually:
# Copy them to the screenshots directory
copy C:\Users\YourName\Pictures\Screenshots\*.png ..\..\..\..\docs\screenshots\

# Commit the changes
git add docs/screenshots/*.png
git add README.md
git commit -m "Add actual screenshots of FoldersDiag application"
git push
```

## Questions?

If you need help with screenshots, see the main [CONTRIBUTING.md](../../CONTRIBUTING.md) guide or open an issue on GitHub.
