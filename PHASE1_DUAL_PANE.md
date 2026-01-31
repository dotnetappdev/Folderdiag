# Phase 1: Dual-Pane File Explorer Layout

## Overview

This document describes Phase 1 of transforming FoldersDiag into a full file explorer with dual-pane layout.

## Changes Implemented

### Dual-Pane Layout
- **Left Pane**: TreeView control showing folder hierarchy
- **Right Pane**: ListView control with size analysis (existing functionality)
- **Splitter**: Resizable divider between panes (4px width)

### TreeView Navigation
- **This PC Root**: Top-level node showing all drives
- **Drive Enumeration**: Automatically detects Fixed, Removable, and RAM drives
- **Volume Names**: Shows drive letters with volume labels (e.g., "C:\ Windows")
- **Lazy Loading**: Subdirectories loaded on-demand when node is expanded
- **Selection Sync**: Clicking a folder in TreeView scans and displays it in ListView

### Layout Management
- **Splitter Position**: Default 250px from left, user-adjustable
- **Bounds Checking**: Splitter constrained between 100px and (width-200px)
- **Responsive**: Panes resize with window

### Features Retained
- All size visualization features (progress bars, colors)
- Sortable columns
- Custom color preferences
- Dark/Light themes
- Status bar with scan progress

## UI Structure

```
┌────────────────────────────────────────────────────────────────┐
│ FoldersDiag - File Explorer                          [_][□][X] │
├────────────────────────────────────────────────────────────────┤
│ File   View                                                    │
├────────────────────────────────────────────────────────────────┤
│ [Browse] [Refresh] │ [Toggle Theme]                            │
├──────────────────┬───┬─────────────────────────────────────────┤
│ This PC          │ │ │ Name    Size    [Bar]  Files  Folders  │
│ ├─ C:\ Windows   │ │ │──────────────────────────────────────  │
│ │  ├─ Program... │ │ │ Documents  45.8GB  [████]  8,934  423  │
│ │  ├─ Users      │ │ │ Downloads  85.2GB  [████]  3,421  156  │
│ │  └─ Windows    │ │ │ Pictures   28.5GB  [███]   1,892   89  │
│ └─ D:\ Data      │ │ │ Videos    125.4GB  [████]    847   12  │
│                  │ │ │                                          │
│   TreeView       │S│ │          ListView                       │
│   (Left Pane)    │P│ │          (Right Pane)                   │
│                  │L│ │                                          │
│                  │I│ │                                          │
│                  │T│ │                                          │
├──────────────────┴─┴─┴─────────────────────────────────────────┤
│ Ready. Select a folder to analyze.                             │
└────────────────────────────────────────────────────────────────┘
```

## Technical Implementation

### New Components
- **m_treeView**: HWND for TreeView control
- **m_splitter**: HWND for splitter bar
- **m_splitterPos**: Integer tracking splitter position
- **m_splitterDragging**: Boolean for drag state (future use)

### New Methods
```cpp
void PopulateTreeView();                        // Initialize TreeView with drives
void PopulateTreeNode(HTREEITEM, wstring);      // Lazy-load subdirectories
void OnTreeSelectionChanged(HTREEITEM);         // Handle tree selection
wstring GetTreeItemPath(HTREEITEM);             // Get path from tree item
```

### Message Handling
- **TVN_SELCHANGED**: TreeView selection changed → scan selected folder
- **WM_SIZE**: Repositions TreeView, Splitter, ListView with proper layout

### Memory Management
- Tree item paths stored as `wstring*` in LPARAM
- Memory cleaned up when tree items are destroyed

## Usage

### Navigation
1. **Expand Tree Nodes**: Click [+] to expand folders
2. **Select Folder**: Click folder name to view contents
3. **View Details**: Right pane shows files/folders with sizes

### Current Limitations
- Splitter not yet draggable (positioned at fixed 250px)
- No context menus
- Tree doesn't show file icons yet
- Hidden files always shown

## Next Phases

### Phase 2 - Interactive Splitter
- Mouse cursor changes over splitter
- Drag to resize panes
- Save splitter position to settings

### Phase 3 - Navigation Bar
- Address bar for direct path entry
- Back/forward buttons
- Up button
- Breadcrumb trail

### Phase 4 - View Modes
- Large icons
- Small icons
- List view
- Details view (current)
- Tiles view

### Phase 5 - File Operations
- Context menus
- Copy, cut, paste
- Delete with confirmation
- Rename

## Integration with Existing Features

### Size Analysis
- Tree selection triggers folder scan
- ListView shows size bars and sorting
- All customization features work

### Themes
- TreeView and ListView use current theme
- Splitter adapts to theme colors

### Settings
- Color preferences apply to ListView bars
- Theme selection affects all controls

## Testing Checklist

- [ ] TreeView shows all drives
- [ ] Clicking drive shows root contents
- [ ] Expanding folders shows subdirectories
- [ ] Selecting folder scans and displays in ListView
- [ ] Size bars work correctly
- [ ] Sorting by size works
- [ ] Window resize adjusts both panes
- [ ] Dark/light theme affects all controls
- [ ] Status bar shows scan progress

---

**Phase 1 Status**: ✅ Complete
**Next**: Phase 2 - Interactive Splitter Dragging
