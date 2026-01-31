# FoldersDiag UI Design

## Application Overview

FoldersDiag features a modern, clean interface designed for efficient folder size analysis.

## Window Layout

```
┌────────────────────────────────────────────────────────────────┐
│ FoldersDiag - Folder Size Analyzer                    [_][□][X]│
├────────────────────────────────────────────────────────────────┤
│ File   View                                                    │
├────────────────────────────────────────────────────────────────┤
│ [Browse] [Refresh] │ [Toggle Theme]                            │
├────────────────────────────────────────────────────────────────┤
│                    ListView Area                                │
│ ┌──────────────┬────────┬───────┬─────────┬─────────────────┐ │
│ │ Name         │ Size   │ Files │ Folders │ Path            │ │
│ ├──────────────┼────────┼───────┼─────────┼─────────────────┤ │
│ │ Documents    │ 15.2GB │  5234 │     142 │ C:\Users\...    │ │
│ │   Projects   │ 8.1GB  │  3451 │      87 │ C:\Users\...    │ │
│ │     MyApp    │ 5.2GB  │  2103 │      45 │ C:\Users\...    │ │
│ │     WebApp   │ 2.9GB  │  1348 │      42 │ C:\Users\...    │ │
│ │   Photos     │ 7.1GB  │  1783 │      55 │ C:\Users\...    │ │
│ │ Downloads    │ 12.8GB │  892  │      23 │ C:\Users\...    │ │
│ │ Videos       │ 45.3GB │  124  │       8 │ C:\Users\...    │ │
│ └──────────────┴────────┴───────┴─────────┴─────────────────┘ │
│                                                                 │
│                                                                 │
├────────────────────────────────────────────────────────────────┤
│ Ready. Select a folder to analyze.                             │
└────────────────────────────────────────────────────────────────┘
```

## Light Theme

### Color Scheme
- **Background**: White (#FFFFFF)
- **Text**: Black (#000000)
- **Header**: Light Gray (#F0F0F0)
- **Alternate Rows**: Very Light Gray (#F8F8F8)
- **Selected**: Blue (#0078D7)
- **Border**: Medium Gray (#C8C8C8)

### Visual Characteristics
- Clean, professional appearance
- High contrast for readability
- Follows Windows design guidelines
- Suitable for bright environments

## Dark Theme

### Color Scheme
- **Background**: Dark Gray (#1E1E1E)
- **Text**: Light Gray (#DCDCDC)
- **Header**: Medium Dark Gray (#2D2D30)
- **Alternate Rows**: Slightly Lighter Dark (#282828)
- **Selected**: Blue (#0078D7)
- **Border**: Dark Border (#3C3C3C)

### Visual Characteristics
- Reduced eye strain
- Modern, sleek appearance
- Comfortable for extended use
- Better for low-light environments

## UI Elements

### 1. Title Bar
```
┌────────────────────────────────────────────────────┐
│ FoldersDiag - Folder Size Analyzer       [_][□][X] │
└────────────────────────────────────────────────────┘
```
- Application title on left
- Standard Windows controls on right
- Dark mode affects title bar appearance (Windows 10 1809+)

### 2. Menu Bar
```
┌────────────────────────────────────────────────────┐
│ File   View                                        │
└────────────────────────────────────────────────────┘
```

**File Menu:**
```
File
├─ Browse Folder...    Ctrl+O
├─ Refresh             F5
├─ ────────────────────────
└─ Exit
```

**View Menu:**
```
View
└─ Toggle Dark Mode    Ctrl+D
```

### 3. Toolbar
```
┌────────────────────────────────────────────────────┐
│ [📁 Browse] [🔄 Refresh] │ [🌓 Toggle Theme]      │
└────────────────────────────────────────────────────┘
```
- Icon buttons with tooltips
- Separator between groups
- Flat, modern styling
- Hover effects

### 4. ListView (Main Content Area)

#### Column Headers
```
┌───────────────┬────────┬───────┬─────────┬──────────────┐
│ Name      ▼▲  │ Size ▼ │ Files │ Folders │ Path         │
└───────────────┴────────┴───────┴─────────┴──────────────┘
```
- Clickable for sorting
- Visual indicator for sort direction
- Resizable columns
- Professional appearance

#### Data Rows
```
┌───────────────┬────────┬───────┬─────────┬──────────────┐
│ Documents     │ 15.2GB │  5234 │     142 │ C:\Users\... │
│   Projects    │  8.1GB │  3451 │      87 │ C:\Users\... │
│     MyApp     │  5.2GB │  2103 │      45 │ C:\Users\... │
└───────────────┴────────┴───────┴─────────┴──────────────┘
```
- Indentation shows hierarchy
- Alternating row colors (subtle)
- Full row selection
- Grid lines for clarity

### 5. Status Bar
```
┌────────────────────────────────────────────────────┐
│ Ready. Select a folder to analyze.                 │
└────────────────────────────────────────────────────┘
```
- Shows current status
- Displays progress during scanning
- Summary after scan completion
- Size grip on right for window resizing

## Interaction Patterns

### Scanning Flow

1. **Initial State**
```
Status: "Ready. Select a folder to analyze."
ListView: Empty
```

2. **User Clicks Browse**
```
Dialog: Windows folder picker appears
```

3. **Scanning**
```
Status: "Scanning: C:\Users\Documents\Projects\MyApp"
ListView: Empty (being populated)
```

4. **Complete**
```
Status: "Scan complete. Found 5,234 files in 142 folders."
ListView: Populated with sorted results
```

### Theme Toggle

**Before (Light Mode):**
- White background
- Black text
- Light header

**After (Dark Mode):**
- Dark gray background
- Light gray text
- Dark header
- Smooth transition

### Sorting Interaction

1. **User clicks "Size" header**
   - First click: ↓ (descending - largest first)
   - Second click: ↑ (ascending - smallest first)
   
2. **Visual feedback**
   - Arrow indicator in header
   - Items re-order immediately
   - No loading or lag

## Responsive Behavior

### Window Resizing
- Toolbar: Maintains height, adjusts width
- ListView: Expands/shrinks with window
- Status bar: Maintains height, adjusts width
- Columns: Can be manually resized

### Minimum Size
- Width: 800px
- Height: 500px
- Ensures all elements remain usable

### Maximum Size
- No maximum
- Scales appropriately on any monitor
- Works with multi-monitor setups

## Accessibility Features

### Keyboard Navigation
- Tab: Move between controls
- Arrow keys: Navigate list items
- Enter: Default action
- All actions have keyboard shortcuts

### High Contrast
- Respects Windows high contrast settings
- Readable in all modes
- Clear focus indicators

### Screen Reader Support
- Proper control labels
- Status updates announced
- Logical tab order

## Visual Feedback

### Hover Effects
- Buttons: Slight highlight
- ListView rows: Subtle background change
- Menu items: Background highlight

### Selection
- ListView: Full row highlight in blue
- Clear contrast with unselected items
- Remains visible in both themes

### Focus
- Keyboard focus: Dotted outline
- Clear indicator of current control
- Follows Windows standards

## Folder Browser Dialog

```
┌─────────────────────────────────────────────────┐
│ Browse For Folder                     [?][X]    │
├─────────────────────────────────────────────────┤
│  Select a folder to analyze:                    │
│                                                  │
│  📁 This PC                                      │
│  ├─ 💾 Local Disk (C:)                          │
│  │  ├─ 📁 Users                                 │
│  │  │  ├─ 📁 YourName                           │
│  │  │  │  ├─ 📁 Documents            ✓          │
│  │  │  │  ├─ 📁 Downloads                       │
│  │  │  │  └─ 📁 Pictures                        │
│  │  │  └─ 📁 Public                             │
│  │  └─ 📁 Program Files                         │
│  └─ 💾 Local Disk (D:)                          │
│                                                  │
├─────────────────────────────────────────────────┤
│                           [Select] [Cancel]     │
└─────────────────────────────────────────────────┘
```

## Error States

### Access Denied
```
Status: "Cannot access C:\System Volume Information: Access denied"
ListView: Partial results (accessible items only)
```

### Empty Folder
```
Status: "Scan complete. Folder is empty."
ListView: Shows only the root folder with 0 size
```

### Cancellation
```
Status: "Scan cancelled by user."
ListView: Partial results from before cancellation
```

## Loading States

### Scanning In Progress
```
Status: "Scanning: C:\Very\Long\Path\To\Current\Folder..."
ListView: (Optional) Can show items as they're found
Cursor: Working cursor (spinning circle on Windows)
```

## Context Menu (Future Enhancement)

Right-click on item:
```
┌────────────────────────┐
│ Open in Explorer       │
│ Copy Path              │
│ Properties             │
│ ───────────────────────│
│ Exclude from View      │
└────────────────────────┘
```

## Tooltip Examples

- **Browse button**: "Select a folder to analyze (Ctrl+O)"
- **Refresh button**: "Re-scan current folder (F5)"
- **Theme button**: "Toggle between light and dark themes (Ctrl+D)"
- **Column headers**: "Click to sort by [column name]"

## Animation and Transitions

### Smooth Transitions
- Theme changes: Instant (no animation)
- Window resize: Smooth control repositioning
- ListView scrolling: Standard smooth scroll
- Sorting: Instant re-order

### No Unnecessary Animations
- Focus on performance
- Quick, responsive feel
- No distracting effects

## Professional Polish

### Attention to Detail
- Consistent spacing
- Aligned elements
- Proper padding
- Clean borders

### Modern Aesthetics
- Flat design
- Minimal shadows
- Professional color scheme
- Clean typography

### Windows Integration
- Native look and feel
- Standard controls
- Familiar interactions
- Follows design guidelines

---

This UI design prioritizes:
- ✅ Clarity and readability
- ✅ Efficient workflow
- ✅ Professional appearance
- ✅ Modern aesthetics
- ✅ Accessibility
- ✅ Performance
