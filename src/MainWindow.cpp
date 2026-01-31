#include "MainWindow.h"
#include "ThemeManager.h"
#include "resource.h"
#include <shlobj.h>
#include <shlwapi.h>
#include <algorithm>
#include <sstream>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shlwapi.lib")

MainWindow::MainWindow() 
    : m_hwnd(nullptr), m_listView(nullptr), m_statusBar(nullptr), 
      m_toolbar(nullptr), m_sortDescending(true), m_maxSize(0) {
    m_scanner = std::make_unique<FolderScanner>();
}

MainWindow::~MainWindow() {
}

bool MainWindow::Create() {
    // Initialize common controls
    INITCOMMONCONTROLSEX icex = {};
    icex.dwSize = sizeof(INITCOMMONCONTROLSEX);
    icex.dwICC = ICC_LISTVIEW_CLASSES | ICC_BAR_CLASSES;
    InitCommonControlsEx(&icex);
    
    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = GetModuleHandle(NULL);
    wc.lpszClassName = L"FoldersDiagMainWindow";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = ThemeManager::Instance().GetBackgroundBrush();
    wc.lpszMenuName = MAKEINTRESOURCEW(IDR_MAINMENU);
    
    RegisterClassExW(&wc);
    
    m_hwnd = CreateWindowExW(
        0,
        L"FoldersDiagMainWindow",
        L"FoldersDiag - Folder Size Analyzer",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT,
        1200, 700,
        NULL, NULL,
        GetModuleHandle(NULL),
        this
    );
    
    if (!m_hwnd) {
        return false;
    }
    
    ThemeManager::Instance().ApplyToWindow(m_hwnd);
    return true;
}

void MainWindow::Show(int nCmdShow) {
    ShowWindow(m_hwnd, nCmdShow);
    UpdateWindow(m_hwnd);
}

int MainWindow::MessageLoop() {
    MSG msg = {};
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    return static_cast<int>(msg.wParam);
}

LRESULT CALLBACK MainWindow::WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    MainWindow* pThis = nullptr;
    
    if (uMsg == WM_NCCREATE) {
        CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = reinterpret_cast<MainWindow*>(pCreate->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        pThis->m_hwnd = hwnd;
    } else {
        pThis = reinterpret_cast<MainWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }
    
    if (pThis) {
        return pThis->HandleMessage(uMsg, wParam, lParam);
    }
    
    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

LRESULT MainWindow::HandleMessage(UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
        case WM_CREATE:
            OnCreate();
            return 0;
            
        case WM_SIZE:
            OnSize(LOWORD(lParam), HIWORD(lParam));
            return 0;
            
        case WM_PAINT:
            OnPaint();
            return 0;
            
        case WM_COMMAND:
            OnCommand(wParam);
            return 0;
            
        case WM_NOTIFY:
            OnNotify(reinterpret_cast<LPNMHDR>(lParam));
            return 0;
            
        case WM_CONTEXTMENU:
            OnContextMenu(GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
            return 0;
            
        case WM_USER + 1: {
            // Progress update
            std::wstring* pPath = reinterpret_cast<std::wstring*>(lParam);
            if (pPath) {
                UpdateStatusBar(L"Scanning: " + *pPath);
                delete pPath;
            }
            return 0;
        }
        
        case WM_USER + 2:
            // Scan complete
            PopulateListView(m_rootItem);
            return 0;
            
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
    }
    
    return DefWindowProc(m_hwnd, uMsg, wParam, lParam);
}

void MainWindow::OnCreate() {
    CreateMenuBar();
    CreateControls();
    UpdateStatusBar(L"Ready. Select a folder to analyze.");
}

void MainWindow::CreateMenuBar() {
    HMENU hMenuBar = CreateMenu();
    HMENU hFileMenu = CreatePopupMenu();
    HMENU hViewMenu = CreatePopupMenu();
    
    AppendMenuW(hFileMenu, MF_STRING, ID_BROWSE, L"&Browse Folder...\tCtrl+O");
    AppendMenuW(hFileMenu, MF_STRING, ID_REFRESH, L"&Refresh\tF5");
    AppendMenuW(hFileMenu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(hFileMenu, MF_STRING, IDCANCEL, L"E&xit");
    
    AppendMenuW(hViewMenu, MF_STRING, ID_THEME, L"Toggle &Dark Mode\tCtrl+D");
    
    AppendMenuW(hMenuBar, MF_POPUP, reinterpret_cast<UINT_PTR>(hFileMenu), L"&File");
    AppendMenuW(hMenuBar, MF_POPUP, reinterpret_cast<UINT_PTR>(hViewMenu), L"&View");
    
    SetMenu(m_hwnd, hMenuBar);
}

void MainWindow::CreateControls() {
    HINSTANCE hInst = GetModuleHandle(NULL);
    
    // Create toolbar
    m_toolbar = CreateWindowExW(
        0, TOOLBARCLASSNAMEW, NULL,
        WS_CHILD | WS_VISIBLE | TBSTYLE_FLAT | TBSTYLE_TOOLTIPS,
        0, 0, 0, 0,
        m_hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(3001)), hInst, NULL
    );
    
    SendMessage(m_toolbar, TB_BUTTONSTRUCTSIZE, sizeof(TBBUTTON), 0);
    
    TBBUTTON tbButtons[] = {
        { 0, ID_BROWSE, TBSTATE_ENABLED, BTNS_BUTTON, {0}, 0, reinterpret_cast<INT_PTR>(L"Browse") },
        { 1, ID_REFRESH, TBSTATE_ENABLED, BTNS_BUTTON, {0}, 0, reinterpret_cast<INT_PTR>(L"Refresh") },
        { 0, 0, 0, BTNS_SEP, {0}, 0, 0 },
        { 2, ID_THEME, TBSTATE_ENABLED, BTNS_BUTTON, {0}, 0, reinterpret_cast<INT_PTR>(L"Toggle Theme") }
    };
    
    SendMessage(m_toolbar, TB_ADDBUTTONSW, 4, reinterpret_cast<LPARAM>(&tbButtons));
    SendMessage(m_toolbar, TB_AUTOSIZE, 0, 0);
    
    // Create ListView
    m_listView = CreateWindowExW(
        0, WC_LISTVIEWW, NULL,
        WS_CHILD | WS_VISIBLE | WS_BORDER | LVS_REPORT | LVS_SINGLESEL | LVS_SHOWSELALWAYS,
        0, 0, 0, 0,
        m_hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(ID_LISTVIEW)), hInst, NULL
    );
    
    ListView_SetExtendedListViewStyle(m_listView, 
        LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER | LVS_EX_GRIDLINES);
    
    // Add columns
    LVCOLUMNW col = {};
    col.mask = LVCF_TEXT | LVCF_WIDTH | LVCF_SUBITEM;
    
    col.pszText = const_cast<LPWSTR>(L"Name");
    col.cx = 300;
    col.iSubItem = 0;
    ListView_InsertColumn(m_listView, 0, &col);
    
    col.pszText = const_cast<LPWSTR>(L"Size");
    col.cx = 120;
    col.iSubItem = 1;
    ListView_InsertColumn(m_listView, 1, &col);
    
    col.pszText = const_cast<LPWSTR>(L"Size Bar");
    col.cx = 200;
    col.iSubItem = 2;
    ListView_InsertColumn(m_listView, 2, &col);
    
    col.pszText = const_cast<LPWSTR>(L"Files");
    col.cx = 100;
    col.iSubItem = 3;
    ListView_InsertColumn(m_listView, 3, &col);
    
    col.pszText = const_cast<LPWSTR>(L"Folders");
    col.cx = 100;
    col.iSubItem = 4;
    ListView_InsertColumn(m_listView, 4, &col);
    
    col.pszText = const_cast<LPWSTR>(L"Path");
    col.cx = 350;
    col.iSubItem = 5;
    ListView_InsertColumn(m_listView, 5, &col);
    
    // Create status bar
    m_statusBar = CreateWindowExW(
        0, STATUSCLASSNAMEW, NULL,
        WS_CHILD | WS_VISIBLE | SBARS_SIZEGRIP,
        0, 0, 0, 0,
        m_hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(3002)), hInst, NULL
    );
}

void MainWindow::OnSize(int width, int height) {
    if (m_toolbar) {
        SendMessage(m_toolbar, TB_AUTOSIZE, 0, 0);
    }
    
    RECT rcToolbar = {};
    if (m_toolbar) {
        GetWindowRect(m_toolbar, &rcToolbar);
    }
    int toolbarHeight = rcToolbar.bottom - rcToolbar.top;
    
    RECT rcStatus = {};
    if (m_statusBar) {
        GetWindowRect(m_statusBar, &rcStatus);
        SendMessage(m_statusBar, WM_SIZE, 0, 0);
    }
    int statusHeight = rcStatus.bottom - rcStatus.top;
    
    if (m_listView) {
        SetWindowPos(m_listView, NULL, 
            0, toolbarHeight, 
            width, height - toolbarHeight - statusHeight,
            SWP_NOZORDER);
    }
}

void MainWindow::OnPaint() {
    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(m_hwnd, &ps);
    EndPaint(m_hwnd, &ps);
}

void MainWindow::OnCommand(WPARAM wParam) {
    switch (LOWORD(wParam)) {
        case ID_BROWSE:
            BrowseFolder();
            break;
            
        case ID_REFRESH:
            if (m_rootItem) {
                ScanFolder(m_rootItem->GetPath());
            }
            break;
            
        case ID_THEME:
            ToggleTheme();
            break;
            
        case IDCANCEL:
            PostMessage(m_hwnd, WM_CLOSE, 0, 0);
            break;
    }
}

void MainWindow::OnNotify(LPNMHDR pnmhdr) {
    if (pnmhdr->idFrom == ID_LISTVIEW) {
        if (pnmhdr->code == LVN_COLUMNCLICK) {
            LPNMLISTVIEW pnmv = reinterpret_cast<LPNMLISTVIEW>(pnmhdr);
            if (pnmv->iSubItem == 1) { // Size column
                SortBySize();
            }
        } else if (pnmhdr->code == NM_CUSTOMDRAW) {
            LPNMLVCUSTOMDRAW pCustomDraw = reinterpret_cast<LPNMLVCUSTOMDRAW>(pnmhdr);
            
            if (pCustomDraw->nmcd.dwDrawStage == CDDS_PREPAINT) {
                SetWindowLongPtr(m_hwnd, DWLP_MSGRESULT, CDRF_NOTIFYITEMDRAW);
                return;
            }
            
            if (pCustomDraw->nmcd.dwDrawStage == CDDS_ITEMPREPAINT) {
                SetWindowLongPtr(m_hwnd, DWLP_MSGRESULT, CDRF_NOTIFYSUBITEMDRAW);
                return;
            }
            
            if (pCustomDraw->nmcd.dwDrawStage == (CDDS_ITEMPREPAINT | CDDS_SUBITEM)) {
                if (pCustomDraw->iSubItem == 2) { // Size Bar column
                    FileSystemItem* item = reinterpret_cast<FileSystemItem*>(pCustomDraw->nmcd.lItemlParam);
                    if (item) {
                        RECT rect = pCustomDraw->nmcd.rc;
                        DrawProgressBar(pCustomDraw->nmcd.hdc, rect, item->GetSize(), m_maxSize);
                        SetWindowLongPtr(m_hwnd, DWLP_MSGRESULT, CDRF_SKIPDEFAULT);
                        return;
                    }
                }
                SetWindowLongPtr(m_hwnd, DWLP_MSGRESULT, CDRF_DODEFAULT);
                return;
            }
        }
    }
}

void MainWindow::OnContextMenu(int x, int y) {
    // Context menu can be implemented here
}

void MainWindow::BrowseFolder() {
    IFileDialog* pfd = nullptr;
    HRESULT hr = CoCreateInstance(CLSID_FileOpenDialog, NULL, CLSCTX_INPROC_SERVER, 
                                  IID_PPV_ARGS(&pfd));
    
    if (SUCCEEDED(hr)) {
        DWORD dwOptions;
        pfd->GetOptions(&dwOptions);
        pfd->SetOptions(dwOptions | FOS_PICKFOLDERS);
        
        hr = pfd->Show(m_hwnd);
        if (SUCCEEDED(hr)) {
            IShellItem* psi;
            hr = pfd->GetResult(&psi);
            if (SUCCEEDED(hr)) {
                PWSTR pszPath = nullptr;
                hr = psi->GetDisplayName(SIGDN_FILESYSPATH, &pszPath);
                if (SUCCEEDED(hr)) {
                    ScanFolder(pszPath);
                    CoTaskMemFree(pszPath);
                }
                psi->Release();
            }
        }
        pfd->Release();
    }
}

void MainWindow::ScanFolder(const std::wstring& path) {
    UpdateStatusBar(L"Scanning: " + path);
    ListView_DeleteAllItems(m_listView);
    m_flatList.clear();
    
    m_scanner->ScanAsync(
        path,
        [this](const std::wstring& currentPath) {
            // Progress callback
            PostMessage(m_hwnd, WM_USER + 1, 0, 
                       reinterpret_cast<LPARAM>(new std::wstring(currentPath)));
        },
        [this](std::shared_ptr<FileSystemItem> root) {
            // Completion callback
            m_rootItem = root;
            PostMessage(m_hwnd, WM_USER + 2, 0, 0);
        }
    );
}

void MainWindow::PopulateListView(std::shared_ptr<FileSystemItem> root) {
    if (!root) return;
    
    ListView_DeleteAllItems(m_listView);
    m_flatList.clear();
    m_maxSize = 0;
    
    // Collect all items and find max size
    std::function<void(std::shared_ptr<FileSystemItem>, int)> collectItems;
    collectItems = [&](std::shared_ptr<FileSystemItem> item, int indent) {
        m_flatList.push_back(item);
        if (item->GetSize() > m_maxSize) {
            m_maxSize = item->GetSize();
        }
        AddItemToListView(item, indent);
        
        for (const auto& child : item->GetChildren()) {
            if (child->GetType() == FileSystemItem::ItemType::Directory) {
                collectItems(child, indent + 1);
            }
        }
    };
    
    collectItems(root, 0);
    
    // Sort by size initially
    SortBySize();
    
    UpdateStatusBar(L"Scan complete. Found " + 
                   std::to_wstring(root->GetFileCount()) + L" files in " +
                   std::to_wstring(root->GetDirectoryCount()) + L" folders.");
}

void MainWindow::AddItemToListView(std::shared_ptr<FileSystemItem> item, int indent) {
    std::wstring indentStr(indent * 2, L' ');
    std::wstring displayName = indentStr + item->GetName();
    
    LVITEMW lvi = {};
    lvi.mask = LVIF_TEXT | LVIF_PARAM;
    lvi.iItem = ListView_GetItemCount(m_listView);
    lvi.pszText = const_cast<LPWSTR>(displayName.c_str());
    lvi.lParam = reinterpret_cast<LPARAM>(item.get());
    
    int index = ListView_InsertItem(m_listView, &lvi);
    
    if (index != -1) {
        // Column 1: Size text
        ListView_SetItemText(m_listView, index, 1, 
                           const_cast<LPWSTR>(item->GetSizeFormatted().c_str()));
        
        // Column 2: Size bar (drawn via custom draw)
        ListView_SetItemText(m_listView, index, 2, const_cast<LPWSTR>(L""));
        
        // Column 3: Files
        std::wstring fileCount = std::to_wstring(item->GetFileCount());
        ListView_SetItemText(m_listView, index, 3, 
                           const_cast<LPWSTR>(fileCount.c_str()));
        
        // Column 4: Folders
        std::wstring dirCount = std::to_wstring(item->GetDirectoryCount());
        ListView_SetItemText(m_listView, index, 4, 
                           const_cast<LPWSTR>(dirCount.c_str()));
        
        // Column 5: Path
        ListView_SetItemText(m_listView, index, 5, 
                           const_cast<LPWSTR>(item->GetPath().c_str()));
    }
}

void MainWindow::SortBySize() {
    m_sortDescending = !m_sortDescending;
    
    std::sort(m_flatList.begin(), m_flatList.end(),
        [this](const std::shared_ptr<FileSystemItem>& a, 
               const std::shared_ptr<FileSystemItem>& b) {
            if (m_sortDescending) {
                return a->GetSize() > b->GetSize();
            } else {
                return a->GetSize() < b->GetSize();
            }
        });
    
    ListView_DeleteAllItems(m_listView);
    for (const auto& item : m_flatList) {
        AddItemToListView(item, 0);
    }
}

void MainWindow::ToggleTheme() {
    auto& themeMgr = ThemeManager::Instance();
    if (themeMgr.GetTheme() == ThemeManager::Theme::Light) {
        themeMgr.SetTheme(ThemeManager::Theme::Dark);
    } else {
        themeMgr.SetTheme(ThemeManager::Theme::Light);
    }
    
    themeMgr.ApplyToWindow(m_hwnd);
    
    // Update window background
    SetClassLongPtr(m_hwnd, GCLP_HBRBACKGROUND, 
                   reinterpret_cast<LONG_PTR>(themeMgr.GetBackgroundBrush()));
    
    InvalidateRect(m_hwnd, NULL, TRUE);
    UpdateWindow(m_hwnd);
}

void MainWindow::UpdateStatusBar(const std::wstring& text) {
    if (m_statusBar) {
        SendMessageW(m_statusBar, SB_SETTEXTW, 0, reinterpret_cast<LPARAM>(text.c_str()));
    }
}

void MainWindow::DrawProgressBar(HDC hdc, RECT rect, uint64_t size, uint64_t maxSize) {
    if (maxSize == 0) return;
    
    auto& theme = ThemeManager::Instance();
    
    // Add padding
    rect.left += 4;
    rect.right -= 4;
    rect.top += 2;
    rect.bottom -= 2;
    
    // Calculate bar width based on percentage
    double percentage = static_cast<double>(size) / static_cast<double>(maxSize);
    int barWidth = static_cast<int>((rect.right - rect.left) * percentage);
    
    // Draw background
    HBRUSH bgBrush = CreateSolidBrush(theme.GetColors().alternateRow);
    FillRect(hdc, &rect, bgBrush);
    DeleteObject(bgBrush);
    
    // Draw progress bar if there's any size
    if (barWidth > 0) {
        RECT barRect = rect;
        barRect.right = barRect.left + barWidth;
        
        // Get color based on size
        COLORREF barColor = GetSizeColor(size, maxSize);
        
        // Create gradient effect
        TRIVERTEX vertex[2];
        vertex[0].x = barRect.left;
        vertex[0].y = barRect.top;
        vertex[0].Red = GetRValue(barColor) << 8;
        vertex[0].Green = GetGValue(barColor) << 8;
        vertex[0].Blue = GetBValue(barColor) << 8;
        vertex[0].Alpha = 0x0000;
        
        // Slightly lighter color for gradient end
        int r = min(255, GetRValue(barColor) + 30);
        int g = min(255, GetGValue(barColor) + 30);
        int b = min(255, GetBValue(barColor) + 30);
        
        vertex[1].x = barRect.right;
        vertex[1].y = barRect.bottom;
        vertex[1].Red = r << 8;
        vertex[1].Green = g << 8;
        vertex[1].Blue = b << 8;
        vertex[1].Alpha = 0x0000;
        
        GRADIENT_RECT gRect = { 0, 1 };
        GradientFill(hdc, vertex, 2, &gRect, 1, GRADIENT_FILL_RECT_H);
        
        // Draw border around bar
        HBRUSH borderBrush = CreateSolidBrush(RGB(
            max(0, GetRValue(barColor) - 40),
            max(0, GetGValue(barColor) - 40),
            max(0, GetBValue(barColor) - 40)
        ));
        FrameRect(hdc, &barRect, borderBrush);
        DeleteObject(borderBrush);
    }
    
    // Draw border around entire cell
    HBRUSH frameBrush = CreateSolidBrush(theme.GetColors().border);
    FrameRect(hdc, &rect, frameBrush);
    DeleteObject(frameBrush);
}

COLORREF MainWindow::GetSizeColor(uint64_t size, uint64_t maxSize) {
    if (maxSize == 0) return RGB(150, 150, 150);
    
    double percentage = static_cast<double>(size) / static_cast<double>(maxSize);
    
    // Color scale: Blue -> Cyan -> Green -> Yellow -> Orange -> Red
    if (percentage >= 0.8) {
        // Red zone (80-100%)
        return RGB(220, 50, 50);
    } else if (percentage >= 0.6) {
        // Orange zone (60-80%)
        return RGB(255, 140, 50);
    } else if (percentage >= 0.4) {
        // Yellow zone (40-60%)
        return RGB(255, 200, 50);
    } else if (percentage >= 0.2) {
        // Green zone (20-40%)
        return RGB(100, 200, 100);
    } else if (percentage >= 0.1) {
        // Cyan zone (10-20%)
        return RGB(80, 180, 200);
    } else {
        // Blue zone (0-10%)
        return RGB(100, 150, 220);
    }
}
