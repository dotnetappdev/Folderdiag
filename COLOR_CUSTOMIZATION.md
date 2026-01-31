# Color Customization Feature

## Overview

Users can now customize the colors used in the size visualization progress bars through a preferences dialog. This allows for personalized color schemes tailored to individual preferences or accessibility needs.

## Features

### Customizable Color Zones

The application uses 6 color zones to represent different size percentages:

1. **Zone 1** (0-10%): Default Blue - RGB(100, 150, 220)
2. **Zone 2** (10-20%): Default Cyan - RGB(80, 180, 200)
3. **Zone 3** (20-40%): Default Green - RGB(100, 200, 100)
4. **Zone 4** (40-60%): Default Yellow - RGB(255, 200, 50)
5. **Zone 5** (60-80%): Default Orange - RGB(255, 140, 50)
6. **Zone 6** (80-100%): Default Red - RGB(220, 50, 50)

### Accessing Preferences

**Menu**: View → Preferences...

The preferences dialog allows you to:
- **View current colors**: Each zone displays its current color
- **Change colors**: Click "Change Color" button to pick a new color
- **Reset to defaults**: Restore the original color scheme
- **Save changes**: Click OK to apply and save your custom colors

## How to Customize Colors

1. Open the application
2. Go to **View → Preferences...**
3. For each zone you want to customize:
   - Click the **"Change Color"** button next to that zone
   - Select your desired color from the color picker
   - Click OK in the color picker
4. Click **OK** to save your changes
5. The progress bars will immediately update with your new colors

## Settings Persistence

Your color preferences are automatically saved to:
```
%APPDATA%\FoldersDiag\settings.ini
```

The settings file format:
```ini
# FoldersDiag Color Settings
# Format: percentage,R,G,B
# Percentage: 0.0 to 1.0 (0% to 100%)

0,100,150,220
0.1,80,180,200
0.2,100,200,100
0.4,255,200,50
0.6,255,140,50
0.8,220,50,50
```

## Benefits

### Accessibility
- **High Contrast**: Choose colors that work better with your vision
- **Color Blindness**: Adjust colors to be more distinguishable
- **Personal Preference**: Match your desktop theme or personal style

### Use Cases

**Example 1: Monochrome Preference**
Instead of multiple colors, use shades of a single color:
- Zone 1: Light Blue RGB(180, 200, 255)
- Zone 2: Blue RGB(140, 170, 255)
- Zone 3: Medium Blue RGB(100, 140, 255)
- Zone 4: Blue RGB(60, 110, 255)
- Zone 5: Dark Blue RGB(30, 80, 230)
- Zone 6: Darker Blue RGB(10, 50, 200)

**Example 2: High Contrast**
Use highly contrasting colors for better visibility:
- Zone 1: White RGB(255, 255, 255)
- Zone 2: Light Gray RGB(200, 200, 200)
- Zone 3: Gray RGB(150, 150, 150)
- Zone 4: Dark Gray RGB(100, 100, 100)
- Zone 5: Darker Gray RGB(50, 50, 50)
- Zone 6: Black RGB(0, 0, 0)

**Example 3: Warm Colors**
A warm color palette:
- Zone 1: Pale Yellow RGB(255, 255, 200)
- Zone 2: Light Orange RGB(255, 220, 150)
- Zone 3: Orange RGB(255, 180, 100)
- Zone 4: Deep Orange RGB(255, 140, 60)
- Zone 5: Red-Orange RGB(230, 80, 40)
- Zone 6: Dark Red RGB(180, 40, 20)

## Technical Details

### Settings Class
The `Settings` class manages color preferences:
- **Singleton pattern**: Single instance across the application
- **Automatic loading**: Preferences loaded on startup
- **Automatic saving**: Changes saved immediately
- **Default fallback**: Uses default colors if no settings file exists

### PreferencesDialog
The preferences dialog provides:
- **Color previews**: Real-time preview of each color zone
- **Color picker integration**: Standard Windows color picker
- **Zone labels**: Clear indication of percentage ranges
- **Reset functionality**: Quick return to default colors

### Integration
The color system integrates seamlessly with:
- **Progress bars**: Automatically use custom colors
- **Theme system**: Works with both light and dark modes
- **Real-time updates**: Changes apply immediately without restart

## Troubleshooting

### Colors Not Saving
- Check that `%APPDATA%\FoldersDiag` directory exists and is writable
- Ensure the application has permission to write to AppData folder

### Colors Look Wrong After Update
- Use "Reset to Defaults" button in preferences
- Or manually delete `%APPDATA%\FoldersDiag\settings.ini`

### Want to Share Color Scheme
- Copy your `settings.ini` file from `%APPDATA%\FoldersDiag\`
- Share the file with others
- Others can paste it into their `%APPDATA%\FoldersDiag\` folder

## Future Enhancements

Potential future additions:
- **Pre-defined themes**: Bundle popular color schemes
- **Import/Export**: Easy sharing of color configurations
- **More zones**: Additional color thresholds for finer control
- **Color interpolation**: Smooth gradients between zones
- **Preview pane**: See how colors look with sample data

---

**This feature ensures FoldersDiag adapts to your visual preferences!** 🎨
