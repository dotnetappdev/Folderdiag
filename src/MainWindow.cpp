#include "MainWindow.h"
#include "ThemeManager.h"
#include "Settings.h"
#include "PreferencesDialog.h"
#include "resource.h"
#include <shlobj.h>
#include <shlwapi.h>
#include <algorithm>
#include <sstream>
#include <functional>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")

MainWindow::MainWindow() 
    : m_hwnd(nullptr), m_treeView(nullptr), m_listView(nullptr), m_statusBar(nullptr), 
      m_toolbar(nullptr), m_splitter(nullptr), m_sortDescending(true), m_maxSize(0),
      m_splitterPos(250), m_splitterDragging(false), m_viewMode(ViewMode::Details) {
    m_scanner = std::make_unique<FolderScanner>();
}

MainWindow::~MainWindow() {
    // Cleanup TreeView allocated strings
    CleanupTreeViewItems();
}

bool MainWindow::Create() {
    // Initialize common controls
    INITCOMMONCONTROLSEX icex = {};
    icex.dwSize = sizeof(INITCOMMONCONTROLSEX);
    icex.dwICC = ICC_LISTVIEW_CLASSES | ICC_BAR_CLASSES | ICC_TREEVIEW_CLASSES;
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
            
        case WM_LBUTTONDOWN: {
            int x = GET_X_LPARAM(lParam);
            int y = GET_Y_LPARAM(lParam);
            
            // Check if click is on splitter
            RECT splitterRect;
            GetWindowRect(m_splitter, &splitterRect);
            POINT pt = {x, y};
            ClientToScreen(m_hwnd, &pt);
            
            if (PtInRect(&splitterRect, pt)) {
                m_splitterDragging = true;
                SetCapture(m_hwnd);
                SetCursor(LoadCursor(NULL, IDC_SIZEWE));
            }
            return 0;
        }
        
        case WM_LBUTTONUP:
            if (m_splitterDragging) {
                m_splitterDragging = false;
                ReleaseCapture();
            }
            return 0;
            
        case WM_MOUSEMOVE: {
            int x = GET_X_LPARAM(lParam);
            
            if (m_splitterDragging) {
                // Update splitter position
                RECT clientRect;
                GetClientRect(m_hwnd, &clientRect);
                
                // Constrain splitter position
                if (x < 100) x = 100;
                if (x > clientRect.right - 200) x = clientRect.right - 200;
                
                m_splitterPos = x;
                
                // Trigger resize
                OnSize(clientRect.right, clientRect.bottom);
                SetCursor(LoadCursor(NULL, IDC_SIZEWE));
            } else {
                // Check if mouse is over splitter for cursor change
                RECT splitterRect;
                GetWindowRect(m_splitter, &splitterRect);
                POINT pt = {x, GET_Y_LPARAM(lParam)};
                ClientToScreen(m_hwnd, &pt);
                
                if (PtInRect(&splitterRect, pt)) {
                    SetCursor(LoadCursor(NULL, IDC_SIZEWE));
                }
            }
            return 0;
        }
        
        case WM_SETCURSOR: {
            if (LOWORD(lParam) == HTCLIENT) {
                POINT pt;
                GetCursorPos(&pt);
                
                RECT splitterRect;
                GetWindowRect(m_splitter, &splitterRect);
                
                if (PtInRect(&splitterRect, pt)) {
                    SetCursor(LoadCursor(NULL, IDC_SIZEWE));
                    return TRUE;
                }
            }
            break;
        }
            
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
    
    // Create Windows 11 style Ribbon Bar
    m_ribbon = std::make_unique<RibbonBar>();
    RECT clientRect;
    GetClientRect(m_hwnd, &clientRect);
    m_ribbon->Create(m_hwnd, 0, 0, clientRect.right, 0);
    
    // Add Home tab
    RibbonBar::RibbonTab homeTab;
    homeTab.title = L"Home";
    homeTab.active = true;
    
    // Clipboard group
    RibbonBar::RibbonGroup clipboardGroup;
    clipboardGroup.title = L"Clipboard";
    clipboardGroup.buttons.push_back({1, L"Copy", L"Copy selected items", true, 0});
    clipboardGroup.buttons.push_back({2, L"Paste", L"Paste items", true, 0});
    homeTab.groups.push_back(clipboardGroup);
    
    // Organize group
    RibbonBar::RibbonGroup organizeGroup;
    organizeGroup.title = L"Organize";
    organizeGroup.buttons.push_back({3, L"New Folder", L"Create a new folder", true, 0});
    organizeGroup.buttons.push_back({4, L"Delete", L"Delete selected items", true, 0});
    organizeGroup.buttons.push_back({5, L"Rename", L"Rename selected item", true, 0});
    homeTab.groups.push_back(organizeGroup);
    
    // Navigate group
    RibbonBar::RibbonGroup navigateGroup;
    navigateGroup.title = L"Navigate";
    navigateGroup.buttons.push_back({ID_BROWSE, L"Browse", L"Browse for folder", true, 0});
    navigateGroup.buttons.push_back({ID_REFRESH, L"Refresh", L"Refresh current view", true, 0});
    homeTab.groups.push_back(navigateGroup);
    
    m_ribbon->AddTab(homeTab);
    
    // Add View tab
    RibbonBar::RibbonTab viewTab;
    viewTab.title = L"View";
    viewTab.active = false;
    
    // Layout group
    RibbonBar::RibbonGroup layoutGroup;
    layoutGroup.title = L"Layout";
    layoutGroup.buttons.push_back({10, L"Details", L"Details view", true, 0});
    layoutGroup.buttons.push_back({11, L"List", L"List view", true, 0});
    layoutGroup.buttons.push_back({12, L"Icons", L"Large icons view", true, 0});
    viewTab.groups.push_back(layoutGroup);
    
    // Show/Hide group
    RibbonBar::RibbonGroup showHideGroup;
    showHideGroup.title = L"Show/Hide";
    showHideGroup.buttons.push_back({13, L"Hidden", L"Show hidden files", true, 0});
    showHideGroup.buttons.push_back({14, L"Extensions", L"Show file extensions", true, 0});
    viewTab.groups.push_back(showHideGroup);
    
    // Theme group
    RibbonBar::RibbonGroup themeGroup;
    themeGroup.title = L"Theme";
    themeGroup.buttons.push_back({ID_THEME, L"Dark Mode", L"Toggle dark/light theme", true, 0});
    themeGroup.buttons.push_back({15, L"Preferences", L"Customize colors", true, 0});
    viewTab.groups.push_back(themeGroup);
    
    m_ribbon->AddTab(viewTab);
    
    // Add Share tab
    RibbonBar::RibbonTab shareTab;
    shareTab.title = L"Share";
    shareTab.active = false;
    
    // Send group
    RibbonBar::RibbonGroup sendGroup;
    sendGroup.title = L"Send";
    sendGroup.buttons.push_back({20, L"Email", L"Send via email", true, 0});
    sendGroup.buttons.push_back({21, L"Compress", L"Compress and share", true, 0});
    shareTab.groups.push_back(sendGroup);
    
    m_ribbon->AddTab(shareTab);
    
    // Create TreeView for folder navigation
    m_treeView = CreateWindowExW(
        WS_EX_CLIENTEDGE, WC_TREEVIEWW, NULL,
        WS_CHILD | WS_VISIBLE | WS_BORDER | TVS_HASLINES | TVS_HASBUTTONS | TVS_LINESATROOT | TVS_SHOWSELALWAYS,
        0, 0, 0, 0,
        m_hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(ID_TREEVIEW)), hInst, NULL
    );
    
    // Populate TreeView with system drives
    PopulateTreeView();
    
    // Create splitter (resize handle between panes)
    m_splitter = CreateWindowExW(
        0, L"STATIC", NULL,
        WS_CHILD | WS_VISIBLE | SS_NOTIFY | SS_OWNERDRAW,
        0, 0, 0, 0,
        m_hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(ID_SPLITTER)), hInst, NULL
    );
    
    // Subclass splitter for custom drawing
    SetWindowSubclass(m_splitter, SplitterProc, 0, reinterpret_cast<DWORD_PTR>(this));
    
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
    // Update ribbon size
    int ribbonHeight = 0;
    if (m_ribbon) {
        m_ribbon->OnSize(width, height);
        ribbonHeight = m_ribbon->GetHeight();
    }
    
    RECT rcStatus = {};
    if (m_statusBar) {
        GetWindowRect(m_statusBar, &rcStatus);
        SendMessage(m_statusBar, WM_SIZE, 0, 0);
    }
    int statusHeight = rcStatus.bottom - rcStatus.top;
    
    int contentHeight = height - ribbonHeight - statusHeight;
    int contentTop = ribbonHeight;
    
    // Ensure splitter position is within bounds
    if (m_splitterPos < 100) m_splitterPos = 100;
    if (m_splitterPos > width - 200) m_splitterPos = width - 200;
    
    // Position TreeView (left pane)
    if (m_treeView) {
        SetWindowPos(m_treeView, NULL, 
            0, contentTop, 
            m_splitterPos, contentHeight,
            SWP_NOZORDER);
    }
    
    // Position splitter
    if (m_splitter) {
        SetWindowPos(m_splitter, NULL, 
            m_splitterPos, contentTop, 
            SPLITTER_WIDTH, contentHeight,
            SWP_NOZORDER);
    }
    
    // Position ListView (right pane)
    if (m_listView) {
        SetWindowPos(m_listView, NULL, 
            m_splitterPos + SPLITTER_WIDTH, contentTop, 
            width - m_splitterPos - SPLITTER_WIDTH, contentHeight,
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
            
        case ID_PREFERENCES:
            ShowPreferences();
            break;
            
        case ID_ABOUT:
            ShowAbout();
            break;
            
        case ID_VIEW_DETAILS:
            SetViewMode(ViewMode::Details);
            break;
            
        case ID_VIEW_LIST:
            SetViewMode(ViewMode::List);
            break;
            
        case ID_VIEW_ICONS:
            SetViewMode(ViewMode::Icons);
            break;
            
        case IDCANCEL:
            PostMessage(m_hwnd, WM_CLOSE, 0, 0);
            break;
    }
}

void MainWindow::OnNotify(LPNMHDR pnmhdr) {
    if (pnmhdr->idFrom == ID_TREEVIEW) {
        if (pnmhdr->code == TVN_SELCHANGED) {
            LPNMTREEVIEW pnmtv = reinterpret_cast<LPNMTREEVIEW>(pnmhdr);
            OnTreeSelectionChanged(pnmtv->itemNew.hItem);
        }
    } else if (pnmhdr->idFrom == ID_LISTVIEW) {
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
    // Get the ListView control's position
    if (!m_listView) return;
    
    // Convert screen coordinates to ListView coordinates
    POINT pt = { x, y };
    ScreenToClient(m_listView, &pt);
    
    // Check if right-click was on the ListView
    LVHITTESTINFO hitTest = {};
    hitTest.pt = pt;
    int itemIndex = ListView_HitTest(m_listView, &hitTest);
    
    if (itemIndex == -1) {
        // No item was clicked, show background context menu
        return;
    }
    
    // Get the item data
    LVITEMW item = {};
    item.mask = LVIF_PARAM;
    item.iItem = itemIndex;
    if (!ListView_GetItem(m_listView, &item)) return;
    
    FileSystemItem* pItem = reinterpret_cast<FileSystemItem*>(item.lParam);
    if (!pItem) return;
    
    std::wstring itemPath = pItem->GetPath();
    
    // Get shell context menu for the item
    HRESULT hr;
    IShellFolder* pDesktop = nullptr;
    IShellFolder* pParentFolder = nullptr;
    LPITEMIDLIST pidl = nullptr;
    IContextMenu* pContextMenu = nullptr;
    
    hr = SHGetDesktopFolder(&pDesktop);
    if (FAILED(hr)) return;
    
    // Make a non-const copy for ParseDisplayName
    std::wstring pathCopy = itemPath;
    
    // Parse the display name to get PIDL
    hr = pDesktop->ParseDisplayName(NULL, NULL, &pathCopy[0], NULL, &pidl, NULL);
    if (SUCCEEDED(hr) && pidl) {
        // Get the parent folder
        LPITEMIDLIST pidlParent = ILClone(pidl);
        if (pidlParent) {
            ILRemoveLastID(pidlParent);
            
            LPCITEMIDLIST pidlChild = ILFindLastID(pidl);
            
            hr = pDesktop->BindToObject(pidlParent, NULL, IID_IShellFolder, 
                                         reinterpret_cast<void**>(&pParentFolder));
            if (SUCCEEDED(hr)) {
                // Get the context menu
                hr = pParentFolder->GetUIObjectOf(m_hwnd, 1, &pidlChild, IID_IContextMenu, 
                                                  NULL, reinterpret_cast<void**>(&pContextMenu));
                if (SUCCEEDED(hr)) {
                    // Create and display the menu
                    HMENU hMenu = CreatePopupMenu();
                    if (hMenu) {
                        // Query the context menu for items
                        const UINT MAX_CONTEXT_MENU_CMD_ID = 0x7FFF;
                        hr = pContextMenu->QueryContextMenu(hMenu, 0, 1, MAX_CONTEXT_MENU_CMD_ID, CMF_NORMAL | CMF_EXPLORE);
                        if (SUCCEEDED(hr)) {
                            // Convert back to screen coordinates
                            POINT screenPt = { x, y };
                            
                            // Display the menu
                            int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN | TPM_RIGHTBUTTON, 
                                                   screenPt.x, screenPt.y, 0, m_hwnd, NULL);
                            if (cmd > 0) {
                                // Execute the selected command
                                CMINVOKECOMMANDINFO info = {};
                                info.cbSize = sizeof(info);
                                info.fMask = 0;
                                info.hwnd = m_hwnd;
                                info.lpVerb = MAKEINTRESOURCEA(cmd - 1);
                                info.nShow = SW_SHOWNORMAL;
                                
                                pContextMenu->InvokeCommand(&info);
                            }
                        }
                        DestroyMenu(hMenu);
                    }
                    pContextMenu->Release();
                }
                pParentFolder->Release();
            }
            ILFree(pidlParent);
        }
        ILFree(pidl);
    }
    pDesktop->Release();
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

void MainWindow::ShowPreferences() {
    if (PreferencesDialog::Show(m_hwnd)) {
        // Colors have been updated, refresh the list view
        if (m_listView) {
            InvalidateRect(m_listView, NULL, TRUE);
            UpdateWindow(m_listView);
        }
    }
}

void MainWindow::ShowAbout() {
    DialogBox(GetModuleHandle(NULL), MAKEINTRESOURCE(IDD_ABOUT), m_hwnd, 
        [](HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) -> INT_PTR {
            switch (msg) {
                case WM_INITDIALOG:
                    return TRUE;
                case WM_COMMAND:
                    if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL) {
                        EndDialog(hwnd, LOWORD(wParam));
                        return TRUE;
                    }
                    break;
            }
            return FALSE;
        });
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
    
    // Get color thresholds from settings
    const auto& thresholds = Settings::Instance().GetColorThresholds();
    
    // Find the appropriate color based on percentage
    // Thresholds should be sorted in ascending order
    COLORREF color = RGB(150, 150, 150); // Default gray
    
    for (size_t i = 0; i < thresholds.size(); ++i) {
        if (percentage >= thresholds[i].percentage) {
            color = thresholds[i].color;
        } else {
            break;
        }
    }
    
    return color;
}

void MainWindow::PopulateTreeView() {
    if (!m_treeView) return;
    
    // Cleanup previously allocated strings
    CleanupTreeViewItems();
    
    TreeView_DeleteAllItems(m_treeView);
    
    TVINSERTSTRUCTW tvis = {};
    tvis.hParent = TVI_ROOT;
    tvis.hInsertAfter = TVI_LAST;
    tvis.item.mask = TVIF_TEXT | TVIF_PARAM | TVIF_CHILDREN;
    
    // Helper lambda to add special folders
    auto AddSpecialFolder = [this, &tvis](HTREEITEM hParent, const wchar_t* displayName, int csidl) {
        wchar_t path[MAX_PATH];
        if (SUCCEEDED(SHGetFolderPathW(NULL, csidl, NULL, 0, path))) {
            std::wstring folderPath = path;
            if (folderPath.back() != L'\\') {
                folderPath += L'\\';
            }
            
            tvis.hParent = hParent;
            tvis.item.pszText = const_cast<LPWSTR>(displayName);
            tvis.item.lParam = reinterpret_cast<LPARAM>(new std::wstring(folderPath));
            tvis.item.cChildren = 1;
            
            return TreeView_InsertItem(m_treeView, &tvis);
        }
        return (HTREEITEM)NULL;
    };
    
    // Add special folders at root level (like Windows 11 Explorer)
    AddSpecialFolder(TVI_ROOT, L"Desktop", CSIDL_DESKTOP);
    AddSpecialFolder(TVI_ROOT, L"Documents", CSIDL_MYDOCUMENTS);
    
    // Add Downloads folder (construct from user profile)
    wchar_t profilePath[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_PROFILE, NULL, 0, profilePath))) {
        std::wstring downloadsPath = profilePath;
        downloadsPath += L"\\Downloads\\";
        
        tvis.hParent = TVI_ROOT;
        tvis.item.pszText = const_cast<LPWSTR>(L"Downloads");
        tvis.item.lParam = reinterpret_cast<LPARAM>(new std::wstring(downloadsPath));
        tvis.item.cChildren = 1;
        TreeView_InsertItem(m_treeView, &tvis);
    }
    
    // Add "This PC" root
    tvis.hParent = TVI_ROOT;
    tvis.item.pszText = const_cast<LPWSTR>(L"This PC");
    tvis.item.lParam = 0;
    tvis.item.cChildren = 1;
    
    HTREEITEM hThisPC = TreeView_InsertItem(m_treeView, &tvis);
    
    // Get all drives
    DWORD drives = GetLogicalDrives();
    for (int i = 0; i < 26; ++i) {
        if (drives & (1 << i)) {
            wchar_t driveLetter[4] = { static_cast<wchar_t>(L'A' + i), L':', L'\\', L'\0' };
            
            UINT driveType = GetDriveTypeW(driveLetter);
            if (driveType == DRIVE_FIXED || driveType == DRIVE_REMOVABLE || driveType == DRIVE_RAMDISK) {
                wchar_t volumeName[MAX_PATH];
                if (GetVolumeInformationW(driveLetter, volumeName, MAX_PATH, NULL, NULL, NULL, NULL, 0)) {
                    std::wstring displayName = driveLetter;
                    if (wcslen(volumeName) > 0) {
                        displayName += L" ";
                        displayName += volumeName;
                    }
                    
                    tvis.hParent = hThisPC;
                    tvis.item.pszText = const_cast<LPWSTR>(displayName.c_str());
                    tvis.item.lParam = reinterpret_cast<LPARAM>(new std::wstring(driveLetter));
                    tvis.item.cChildren = 1;
                    
                    TreeView_InsertItem(m_treeView, &tvis);
                }
            }
        }
    }
    
    TreeView_Expand(m_treeView, hThisPC, TVE_EXPAND);
}

void MainWindow::PopulateTreeNode(HTREEITEM hParent, const std::wstring& path) {
    if (!m_treeView) return;
    
    // Remove placeholder child if exists
    HTREEITEM hChild = TreeView_GetChild(m_treeView, hParent);
    if (hChild) {
        TVITEMW item = {};
        item.mask = TVIF_PARAM;
        item.hItem = hChild;
        TreeView_GetItem(m_treeView, &item);
        if (item.lParam == 0) {
            TreeView_DeleteItem(m_treeView, hChild);
        }
    }
    
    // Enumerate subdirectories
    std::wstring searchPath = path + L"*";
    WIN32_FIND_DATAW findData;
    HANDLE hFind = FindFirstFileW(searchPath.c_str(), &findData);
    
    if (hFind != INVALID_HANDLE_VALUE) {
        do {
            if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) &&
                wcscmp(findData.cFileName, L".") != 0 &&
                wcscmp(findData.cFileName, L"..") != 0) {
                
                std::wstring subPath = path + findData.cFileName + L"\\";
                
                TVINSERTSTRUCTW tvis = {};
                tvis.hParent = hParent;
                tvis.hInsertAfter = TVI_LAST;
                tvis.item.mask = TVIF_TEXT | TVIF_PARAM | TVIF_CHILDREN;
                tvis.item.pszText = findData.cFileName;
                tvis.item.lParam = reinterpret_cast<LPARAM>(new std::wstring(subPath));
                tvis.item.cChildren = 1;
                
                TreeView_InsertItem(m_treeView, &tvis);
            }
        } while (FindNextFileW(hFind, &findData));
        FindClose(hFind);
    }
}

void MainWindow::OnTreeSelectionChanged(HTREEITEM hItem) {
    if (!hItem) return;
    
    TVITEMW item = {};
    item.mask = TVIF_PARAM;
    item.hItem = hItem;
    
    if (!TreeView_GetItem(m_treeView, &item)) return;
    
    if (item.lParam != 0) {
        std::wstring* pPath = reinterpret_cast<std::wstring*>(item.lParam);
        if (pPath) {
            // Populate tree node on first expansion
            if (TreeView_GetChild(m_treeView, hItem) == NULL || 
                TreeView_GetChild(m_treeView, hItem) != NULL) {
                PopulateTreeNode(hItem, *pPath);
            }
            
            // Scan and display folder contents in ListView
            ScanFolder(*pPath);
        }
    }
}

std::wstring MainWindow::GetTreeItemPath(HTREEITEM hItem) {
    if (!hItem) return L"";
    
    TVITEMW item = {};
    item.mask = TVIF_PARAM;
    item.hItem = hItem;
    
    if (!TreeView_GetItem(m_treeView, &item)) return L"";
    
    if (item.lParam != 0) {
        std::wstring* pPath = reinterpret_cast<std::wstring*>(item.lParam);
        if (pPath) return *pPath;
    }
    
    return L"";
}

void MainWindow::CleanupTreeViewItems() {
    if (!m_treeView) return;
    
    // Helper lambda to recursively delete path strings
    std::function<void(HTREEITEM)> CleanupNode = [&](HTREEITEM hItem) {
        if (!hItem) return;
        
        // Get item data
        TVITEMW item = {};
        item.mask = TVIF_PARAM;
        item.hItem = hItem;
        
        if (TreeView_GetItem(m_treeView, &item) && item.lParam != 0) {
            // Free the allocated string
            std::wstring* pPath = reinterpret_cast<std::wstring*>(item.lParam);
            delete pPath;
        }
        
        // Recursively cleanup children
        HTREEITEM hChild = TreeView_GetChild(m_treeView, hItem);
        while (hChild) {
            HTREEITEM hNext = TreeView_GetNextSibling(m_treeView, hChild);
            CleanupNode(hChild);
            hChild = hNext;
        }
    };
    
    // Cleanup all root items
    HTREEITEM hRoot = TreeView_GetRoot(m_treeView);
    while (hRoot) {
        HTREEITEM hNext = TreeView_GetNextSibling(m_treeView, hRoot);
        CleanupNode(hRoot);
        hRoot = hNext;
    }
}

LRESULT CALLBACK MainWindow::SplitterProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    switch (uMsg) {
        case WM_PAINT: {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hwnd, &ps);
            
            RECT rect;
            GetClientRect(hwnd, &rect);
            
            // Get theme colors
            const auto& theme = ThemeManager::Instance();
            const auto& colors = theme.GetColors();
            
            // Fill with splitter color (slightly darker than background)
            HBRUSH brush = CreateSolidBrush(colors.border);
            FillRect(hdc, &rect, brush);
            DeleteObject(brush);
            
            // Draw a subtle highlight on the left edge
            HBRUSH highlightBrush = CreateSolidBrush(RGB(200, 200, 200));
            RECT highlightRect = rect;
            highlightRect.right = highlightRect.left + 1;
            FillRect(hdc, &highlightRect, highlightBrush);
            DeleteObject(highlightBrush);
            
            EndPaint(hwnd, &ps);
            return 0;
        }
        
        case WM_NCDESTROY:
            RemoveWindowSubclass(hwnd, SplitterProc, uIdSubclass);
            break;
    }
    
    return DefSubclassProc(hwnd, uMsg, wParam, lParam);
}

void MainWindow::SetViewMode(ViewMode mode) {
    m_viewMode = mode;
    ApplyViewMode();
    
    // Repopulate the list with current items to reflect new view
    if (m_rootItem) {
        PopulateListView(m_rootItem);
    }
    
    UpdateStatusBar(L"View mode changed");
}

void MainWindow::ApplyViewMode() {
    if (!m_listView) return;
    
    // Get current ListView style
    LONG style = GetWindowLong(m_listView, GWL_STYLE);
    
    // Remove all view style bits
    style &= ~(LVS_ICON | LVS_SMALLICON | LVS_LIST | LVS_REPORT);
    
    // Apply new view style
    switch (m_viewMode) {
        case ViewMode::Details:
            style |= LVS_REPORT;
            break;
            
        case ViewMode::List:
            style |= LVS_LIST;
            break;
            
        case ViewMode::Icons:
            style |= LVS_ICON;
            break;
    }
    
    // Apply the style
    SetWindowLong(m_listView, GWL_STYLE, style);
    
    // Force redraw
    InvalidateRect(m_listView, NULL, TRUE);
    UpdateWindow(m_listView);
}
