#pragma once
#include <windows.h>
#include <vector>
#include <string>
#include <functional>

// Windows 11 style Ribbon Bar component
class RibbonBar {
public:
    struct RibbonButton {
        int id;
        std::wstring text;
        std::wstring tooltip;
        bool enabled;
        int iconIndex;
    };
    
    struct RibbonGroup {
        std::wstring title;
        std::vector<RibbonButton> buttons;
    };
    
    struct RibbonTab {
        std::wstring title;
        std::vector<RibbonGroup> groups;
        bool active;
    };
    
    RibbonBar();
    ~RibbonBar();
    
    bool Create(HWND parent, int x, int y, int width, int height);
    void SetActiveTab(int index);
    int GetHeight() const { return m_height; }
    HWND GetHwnd() const { return m_hwnd; }
    
    void AddTab(const RibbonTab& tab);
    void OnSize(int width, int height);
    void OnPaint(HDC hdc);
    
private:
    static LRESULT CALLBACK RibbonProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
    
    void DrawTab(HDC hdc, const RECT& rect, const RibbonTab& tab, bool active, bool hover);
    void DrawGroup(HDC hdc, const RECT& rect, const RibbonGroup& group);
    void DrawButton(HDC hdc, const RECT& rect, const RibbonButton& button, bool hover, bool pressed);
    
    HWND m_hwnd;
    HWND m_parent;
    int m_height;
    int m_activeTab;
    int m_hoverTab;
    int m_hoverButton;
    
    std::vector<RibbonTab> m_tabs;
    
    static constexpr int TAB_HEIGHT = 32;
    static constexpr int RIBBON_HEIGHT = 120;
    static constexpr int BUTTON_WIDTH = 80;
    static constexpr int BUTTON_HEIGHT = 70;
    static constexpr int GROUP_MARGIN = 8;
};
