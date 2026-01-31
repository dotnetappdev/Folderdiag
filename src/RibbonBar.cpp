#include "RibbonBar.h"
#include "ThemeManager.h"
#include <windowsx.h>

RibbonBar::RibbonBar()
    : m_hwnd(nullptr), m_parent(nullptr), m_height(RIBBON_HEIGHT + TAB_HEIGHT),
      m_activeTab(0), m_hoverTab(-1), m_hoverButton(-1) {
}

RibbonBar::~RibbonBar() {
    if (m_hwnd) {
        DestroyWindow(m_hwnd);
    }
}

bool RibbonBar::Create(HWND parent, int x, int y, int width, int height) {
    m_parent = parent;
    
    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = RibbonProc;
    wc.hInstance = GetModuleHandle(NULL);
    wc.lpszClassName = L"FoldersDiagRibbonBar";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    
    RegisterClassExW(&wc);
    
    m_hwnd = CreateWindowExW(
        0,
        L"FoldersDiagRibbonBar",
        NULL,
        WS_CHILD | WS_VISIBLE,
        x, y, width, m_height,
        parent,
        NULL,
        GetModuleHandle(NULL),
        this
    );
    
    return m_hwnd != nullptr;
}

void RibbonBar::AddTab(const RibbonTab& tab) {
    m_tabs.push_back(tab);
}

void RibbonBar::SetActiveTab(int index) {
    if (index >= 0 && index < static_cast<int>(m_tabs.size())) {
        m_activeTab = index;
        InvalidateRect(m_hwnd, NULL, TRUE);
    }
}

void RibbonBar::OnSize(int width, int height) {
    SetWindowPos(m_hwnd, NULL, 0, 0, width, m_height, SWP_NOMOVE | SWP_NOZORDER);
}

void RibbonBar::OnPaint(HDC hdc) {
    RECT clientRect;
    GetClientRect(m_hwnd, &clientRect);
    
    // Get theme colors
    const auto& theme = ThemeManager::Instance();
    const auto& colors = theme.GetColors();
    
    // Fill background
    HBRUSH bgBrush = CreateSolidBrush(colors.background);
    FillRect(hdc, &clientRect, bgBrush);
    DeleteObject(bgBrush);
    
    // Draw tab bar
    RECT tabBarRect = clientRect;
    tabBarRect.bottom = TAB_HEIGHT;
    
    HBRUSH tabBgBrush = CreateSolidBrush(RGB(240, 240, 240));
    if (theme.GetCurrentTheme() == Theme::Dark) {
        DeleteObject(tabBgBrush);
        tabBgBrush = CreateSolidBrush(RGB(45, 45, 45));
    }
    FillRect(hdc, &tabBarRect, tabBgBrush);
    DeleteObject(tabBgBrush);
    
    // Draw tabs
    int tabX = 0;
    for (size_t i = 0; i < m_tabs.size(); ++i) {
        RECT tabRect = { tabX, 0, tabX + 120, TAB_HEIGHT };
        bool isActive = (static_cast<int>(i) == m_activeTab);
        bool isHover = (static_cast<int>(i) == m_hoverTab);
        DrawTab(hdc, tabRect, m_tabs[i], isActive, isHover);
        tabX += 120;
    }
    
    // Draw bottom border of tab bar
    HBRUSH borderBrush = CreateSolidBrush(colors.border);
    RECT borderRect = { 0, TAB_HEIGHT - 1, clientRect.right, TAB_HEIGHT };
    FillRect(hdc, &borderRect, borderBrush);
    DeleteObject(borderBrush);
    
    // Draw active tab content (groups and buttons)
    if (m_activeTab >= 0 && m_activeTab < static_cast<int>(m_tabs.size())) {
        const auto& activeTab = m_tabs[m_activeTab];
        RECT contentRect = clientRect;
        contentRect.top = TAB_HEIGHT;
        
        int groupX = GROUP_MARGIN;
        for (const auto& group : activeTab.groups) {
            RECT groupRect = {
                groupX,
                contentRect.top + GROUP_MARGIN,
                groupX + (BUTTON_WIDTH * static_cast<int>(group.buttons.size())) + GROUP_MARGIN * 2,
                contentRect.bottom - GROUP_MARGIN
            };
            DrawGroup(hdc, groupRect, group);
            groupX = groupRect.right + GROUP_MARGIN;
        }
    }
}

void RibbonBar::DrawTab(HDC hdc, const RECT& rect, const RibbonTab& tab, bool active, bool hover) {
    const auto& theme = ThemeManager::Instance();
    const auto& colors = theme.GetColors();
    
    // Draw tab background
    COLORREF bgColor;
    if (active) {
        bgColor = colors.background;
    } else if (hover) {
        bgColor = theme.GetCurrentTheme() == Theme::Dark ? RGB(60, 60, 60) : RGB(230, 230, 230);
    } else {
        bgColor = theme.GetCurrentTheme() == Theme::Dark ? RGB(45, 45, 45) : RGB(240, 240, 240);
    }
    
    HBRUSH brush = CreateSolidBrush(bgColor);
    FillRect(hdc, &rect, brush);
    DeleteObject(brush);
    
    // Draw tab text
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, colors.text);
    
    HFONT hFont = CreateFontW(
        14, 0, 0, 0, active ? FW_SEMIBOLD : FW_NORMAL,
        FALSE, FALSE, FALSE, DEFAULT_CHARSET,
        OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE,
        L"Segoe UI"
    );
    
    HFONT oldFont = (HFONT)SelectObject(hdc, hFont);
    DrawTextW(hdc, tab.title.c_str(), -1, const_cast<LPRECT>(&rect), 
              DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    SelectObject(hdc, oldFont);
    DeleteObject(hFont);
    
    // Draw bottom border for active tab
    if (active) {
        HBRUSH accentBrush = CreateSolidBrush(RGB(0, 120, 212)); // Windows 11 accent blue
        RECT borderRect = rect;
        borderRect.top = borderRect.bottom - 3;
        FillRect(hdc, &borderRect, accentBrush);
        DeleteObject(accentBrush);
    }
}

void RibbonBar::DrawGroup(HDC hdc, const RECT& rect, const RibbonGroup& group) {
    const auto& theme = ThemeManager::Instance();
    const auto& colors = theme.GetColors();
    
    // Draw group border
    HBRUSH borderBrush = CreateSolidBrush(colors.border);
    FrameRect(hdc, &rect, borderBrush);
    DeleteObject(borderBrush);
    
    // Draw group title
    RECT titleRect = rect;
    titleRect.top = titleRect.bottom - 20;
    
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, colors.text);
    
    HFONT hFont = CreateFontW(
        11, 0, 0, 0, FW_NORMAL,
        FALSE, FALSE, FALSE, DEFAULT_CHARSET,
        OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE,
        L"Segoe UI"
    );
    
    HFONT oldFont = (HFONT)SelectObject(hdc, hFont);
    DrawTextW(hdc, group.title.c_str(), -1, &titleRect, 
              DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    SelectObject(hdc, oldFont);
    DeleteObject(hFont);
    
    // Draw buttons
    int buttonX = rect.left + GROUP_MARGIN;
    for (const auto& button : group.buttons) {
        RECT buttonRect = {
            buttonX,
            rect.top + GROUP_MARGIN,
            buttonX + BUTTON_WIDTH,
            rect.bottom - 25
        };
        DrawButton(hdc, buttonRect, button, false, false);
        buttonX += BUTTON_WIDTH;
    }
}

void RibbonBar::DrawButton(HDC hdc, const RECT& rect, const RibbonButton& button, bool hover, bool pressed) {
    const auto& theme = ThemeManager::Instance();
    const auto& colors = theme.GetColors();
    
    // Draw button background
    if (hover || pressed) {
        COLORREF bgColor = pressed ? RGB(0, 100, 192) : RGB(230, 240, 250);
        if (theme.GetCurrentTheme() == Theme::Dark) {
            bgColor = pressed ? RGB(0, 100, 192) : RGB(60, 60, 60);
        }
        HBRUSH brush = CreateSolidBrush(bgColor);
        FillRect(hdc, &rect, brush);
        DeleteObject(brush);
    }
    
    // Draw button border on hover
    if (hover) {
        HBRUSH borderBrush = CreateSolidBrush(RGB(0, 120, 212));
        FrameRect(hdc, &rect, borderBrush);
        DeleteObject(borderBrush);
    }
    
    // Draw icon placeholder (colored rectangle for now)
    RECT iconRect = rect;
    iconRect.left += 20;
    iconRect.right -= 20;
    iconRect.top += 10;
    iconRect.bottom = iconRect.top + 32;
    
    COLORREF iconColor = RGB(0, 120, 212); // Windows 11 blue
    HBRUSH iconBrush = CreateSolidBrush(iconColor);
    FillRect(hdc, &iconRect, iconBrush);
    DeleteObject(iconBrush);
    
    // Draw button text
    RECT textRect = rect;
    textRect.top = iconRect.bottom + 4;
    
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, button.enabled ? colors.text : colors.border);
    
    HFONT hFont = CreateFontW(
        11, 0, 0, 0, FW_NORMAL,
        FALSE, FALSE, FALSE, DEFAULT_CHARSET,
        OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE,
        L"Segoe UI"
    );
    
    HFONT oldFont = (HFONT)SelectObject(hdc, hFont);
    DrawTextW(hdc, button.text.c_str(), -1, &textRect, 
              DT_CENTER | DT_TOP | DT_SINGLELINE);
    SelectObject(hdc, oldFont);
    DeleteObject(hFont);
}

LRESULT CALLBACK RibbonBar::RibbonProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    RibbonBar* pRibbon = nullptr;
    
    if (uMsg == WM_NCCREATE) {
        CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lParam);
        pRibbon = reinterpret_cast<RibbonBar*>(pCreate->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pRibbon));
    } else {
        pRibbon = reinterpret_cast<RibbonBar*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }
    
    if (pRibbon) {
        switch (uMsg) {
            case WM_PAINT: {
                PAINTSTRUCT ps;
                HDC hdc = BeginPaint(hwnd, &ps);
                pRibbon->OnPaint(hdc);
                EndPaint(hwnd, &ps);
                return 0;
            }
            
            case WM_LBUTTONDOWN: {
                int x = GET_X_LPARAM(lParam);
                int y = GET_Y_LPARAM(lParam);
                
                // Check if click is on a tab
                if (y < TAB_HEIGHT) {
                    int tabIndex = x / 120;
                    if (tabIndex < static_cast<int>(pRibbon->m_tabs.size())) {
                        pRibbon->SetActiveTab(tabIndex);
                    }
                }
                return 0;
            }
            
            case WM_MOUSEMOVE: {
                int x = GET_X_LPARAM(lParam);
                int y = GET_Y_LPARAM(lParam);
                
                int oldHoverTab = pRibbon->m_hoverTab;
                
                if (y < TAB_HEIGHT) {
                    pRibbon->m_hoverTab = x / 120;
                    if (pRibbon->m_hoverTab >= static_cast<int>(pRibbon->m_tabs.size())) {
                        pRibbon->m_hoverTab = -1;
                    }
                } else {
                    pRibbon->m_hoverTab = -1;
                }
                
                if (oldHoverTab != pRibbon->m_hoverTab) {
                    InvalidateRect(hwnd, NULL, TRUE);
                }
                return 0;
            }
        }
    }
    
    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}
