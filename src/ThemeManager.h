#pragma once
#include <windows.h>
#include <string>

class ThemeManager {
public:
    enum class Theme {
        Light,
        Dark
    };
    
    struct Colors {
        COLORREF background;
        COLORREF foreground;
        COLORREF headerBackground;
        COLORREF headerForeground;
        COLORREF alternateRow;
        COLORREF selectedBackground;
        COLORREF selectedForeground;
        COLORREF border;
    };
    
    static ThemeManager& Instance();
    
    void SetTheme(Theme theme);
    Theme GetTheme() const { return m_currentTheme; }
    const Colors& GetColors() const { return m_colors; }
    
    // Apply theme to window
    void ApplyToWindow(HWND hwnd);
    
    // Brushes for painting
    HBRUSH GetBackgroundBrush() const { return m_backgroundBrush; }
    HBRUSH GetHeaderBrush() const { return m_headerBrush; }
    HBRUSH GetAlternateRowBrush() const { return m_alternateRowBrush; }
    
private:
    ThemeManager();
    ~ThemeManager();
    
    void UpdateBrushes();
    
    Theme m_currentTheme;
    Colors m_colors;
    
    HBRUSH m_backgroundBrush;
    HBRUSH m_headerBrush;
    HBRUSH m_alternateRowBrush;
};
