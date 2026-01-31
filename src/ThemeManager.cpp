#include "ThemeManager.h"
#include <uxtheme.h>
#include <dwmapi.h>

#pragma comment(lib, "uxtheme.lib")
#pragma comment(lib, "dwmapi.lib")

ThemeManager& ThemeManager::Instance() {
    static ThemeManager instance;
    return instance;
}

ThemeManager::ThemeManager() : m_currentTheme(Theme::Light) {
    m_backgroundBrush = nullptr;
    m_headerBrush = nullptr;
    m_alternateRowBrush = nullptr;
    
    SetTheme(Theme::Light);
}

ThemeManager::~ThemeManager() {
    if (m_backgroundBrush) DeleteObject(m_backgroundBrush);
    if (m_headerBrush) DeleteObject(m_headerBrush);
    if (m_alternateRowBrush) DeleteObject(m_alternateRowBrush);
}

void ThemeManager::SetTheme(Theme theme) {
    m_currentTheme = theme;
    
    if (theme == Theme::Dark) {
        m_colors.background = RGB(30, 30, 30);
        m_colors.foreground = RGB(220, 220, 220);
        m_colors.headerBackground = RGB(45, 45, 48);
        m_colors.headerForeground = RGB(255, 255, 255);
        m_colors.alternateRow = RGB(40, 40, 40);
        m_colors.selectedBackground = RGB(0, 120, 215);
        m_colors.selectedForeground = RGB(255, 255, 255);
        m_colors.border = RGB(60, 60, 60);
    } else {
        m_colors.background = RGB(255, 255, 255);
        m_colors.foreground = RGB(0, 0, 0);
        m_colors.headerBackground = RGB(240, 240, 240);
        m_colors.headerForeground = RGB(0, 0, 0);
        m_colors.alternateRow = RGB(248, 248, 248);
        m_colors.selectedBackground = RGB(0, 120, 215);
        m_colors.selectedForeground = RGB(255, 255, 255);
        m_colors.border = RGB(200, 200, 200);
    }
    
    UpdateBrushes();
}

void ThemeManager::UpdateBrushes() {
    if (m_backgroundBrush) DeleteObject(m_backgroundBrush);
    if (m_headerBrush) DeleteObject(m_headerBrush);
    if (m_alternateRowBrush) DeleteObject(m_alternateRowBrush);
    
    m_backgroundBrush = CreateSolidBrush(m_colors.background);
    m_headerBrush = CreateSolidBrush(m_colors.headerBackground);
    m_alternateRowBrush = CreateSolidBrush(m_colors.alternateRow);
}

void ThemeManager::ApplyToWindow(HWND hwnd) {
    if (m_currentTheme == Theme::Dark) {
        BOOL useDarkMode = TRUE;
        // Try to enable dark mode for the window title bar (Windows 10 1809+)
        DwmSetWindowAttribute(hwnd, 20, &useDarkMode, sizeof(useDarkMode));
    } else {
        BOOL useDarkMode = FALSE;
        DwmSetWindowAttribute(hwnd, 20, &useDarkMode, sizeof(useDarkMode));
    }
    
    InvalidateRect(hwnd, NULL, TRUE);
}
