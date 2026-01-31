# Phase 2: Interactive Splitter Dragging

## Overview

Phase 2 adds interactive drag functionality to the splitter bar, allowing users to resize the TreeView and ListView panes by dragging the divider between them.

## Changes Implemented

### Interactive Splitter
- **Mouse Cursor**: Changes to resize cursor (IDC_SIZEWE) when hovering over splitter
- **Click and Drag**: Click on splitter and drag to adjust pane sizes
- **Live Resize**: Panes resize in real-time as splitter is dragged
- **Bounds Checking**: Splitter constrained between 100px and (window width - 200px)
- **Visual Feedback**: Splitter drawn with subtle border color and highlight

### Message Handling
- **WM_LBUTTONDOWN**: Captures mouse when clicking on splitter, starts drag
- **WM_LBUTTONUP**: Releases mouse capture, ends drag
- **WM_MOUSEMOVE**: Updates splitter position during drag, changes cursor on hover
- **WM_SETCURSOR**: Updates cursor to resize arrow when over splitter

### Visual Enhancements
- **Custom Drawing**: Splitter drawn with theme-aware colors
- **Highlight Border**: Subtle 1px highlight on left edge for depth
- **Theme Integration**: Splitter color uses theme border color

### Technical Implementation
```cpp
// Mouse down on splitter
case WM_LBUTTONDOWN:
    if (click on splitter) {
        m_splitterDragging = true;
        SetCapture(m_hwnd);
        SetCursor(IDC_SIZEWE);
    }

// Mouse move - drag splitter
case WM_MOUSEMOVE:
    if (m_splitterDragging) {
        m_splitterPos = constrain(x, 100, width - 200);
        OnSize(); // Trigger layout update
    }

// Mouse up - release splitter
case WM_LBUTTONUP:
    m_splitterDragging = false;
    ReleaseCapture();
```

### Splitter Custom Drawing
```cpp
LRESULT SplitterProc(HWND hwnd, UINT uMsg, ...) {
    case WM_PAINT:
        // Fill with theme border color
        FillRect(hdc, &rect, borderBrush);
        
        // Draw highlight on left edge
        DrawEdge(hdc, &highlightRect, ...);
}
```

## UI Behavior

### Before (Phase 1)
- Splitter fixed at 250px
- No visual feedback
- No interaction possible

### After (Phase 2)
- Splitter draggable with mouse
- Cursor changes to resize arrow on hover
- Smooth live resizing
- Visual splitter bar with subtle styling
- Constrained to prevent panes from being too small

## User Experience

1. **Hover over splitter** → Cursor changes to ⟷ (resize)
2. **Click on splitter** → Mouse captured, drag mode active
3. **Drag left/right** → Panes resize in real-time
4. **Release mouse** → New size locked in
5. **Visual feedback** → Splitter drawn with border color

## Layout Constraints

```
Minimum TreeView width:  100px
Maximum TreeView width:  (Window Width - 200px)
Splitter width:          4px
Minimum ListView width:  200px
```

This ensures both panes remain usable at all window sizes.

## Integration with Existing Features

### Theme Support
- Splitter color adapts to light/dark theme
- Uses `ThemeManager::GetColors().border`
- Highlight color complements theme

### Window Resize
- Splitter position maintained on window resize
- OnSize() handles both window and splitter resize
- Smooth coordination between resize events

### Settings Persistence
- Current implementation: Splitter position resets to 250px on restart
- Future enhancement: Save/load splitter position in settings.ini

## Testing Checklist

- [ ] Cursor changes to resize arrow on hover
- [ ] Click and drag moves splitter
- [ ] Panes resize in real-time during drag
- [ ] Minimum widths enforced (100px left, 200px right)
- [ ] Splitter position stays within bounds
- [ ] Works in both light and dark themes
- [ ] Window resize doesn't break splitter
- [ ] Mouse capture releases properly
- [ ] Visual appearance is clean and professional

## Code Changes

**Modified Files:**
- `src/MainWindow.h` - Added SplitterProc declaration
- `src/MainWindow.cpp` - Added mouse handling, SplitterProc implementation

**New Features:**
- Interactive drag-and-drop splitter
- Custom splitter drawing with theme support
- Real-time pane resizing

**Lines Changed:** ~80 lines added

---

**Phase 2 Status**: ✅ Complete
**Next**: Phase 3 - Windows 11 Style Ribbon Bar
