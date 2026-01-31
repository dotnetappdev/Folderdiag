#pragma once
#include <windows.h>
#include <commctrl.h>
#include "FolderScanner.h"
#include "FileSystemItem.h"
#include "RibbonBar.h"
#include <memory>
#include <vector>

enum class ViewMode {
    Details,
    List,
    Icons
};

class MainWindow {
public:
    MainWindow();
    ~MainWindow();
    
    bool Create();
    void Show(int nCmdShow);
    int MessageLoop();
    
private:
    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(UINT uMsg, WPARAM wParam, LPARAM lParam);
    
    void OnCreate();
    void OnSize(int width, int height);
    void OnPaint();
    void OnCommand(WPARAM wParam);
    void OnNotify(LPNMHDR pnmhdr);
    void OnContextMenu(int x, int y);
    
    void CreateControls();
    void CreateMenuBar();
    void BrowseFolder();
    void ScanFolder(const std::wstring& path);
    void PopulateListView(std::shared_ptr<FileSystemItem> root);
    void AddItemToListView(std::shared_ptr<FileSystemItem> item, int indent = 0);
    void SortBySize();
    void ToggleTheme();
    void ShowPreferences();
    void ShowAbout();
    void UpdateStatusBar(const std::wstring& text);
    
    // View mode support
    void SetViewMode(ViewMode mode);
    void ApplyViewMode();
    
    // TreeView support for folder navigation
    void PopulateTreeView();
    void PopulateTreeNode(HTREEITEM hParent, const std::wstring& path);
    void OnTreeSelectionChanged(HTREEITEM hItem);
    std::wstring GetTreeItemPath(HTREEITEM hItem);
    void CleanupTreeViewItems();
    
    // Splitter support
    static LRESULT CALLBACK SplitterProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
    
    // Custom draw support for progress bars
    void DrawProgressBar(HDC hdc, RECT rect, uint64_t size, uint64_t maxSize);
    COLORREF GetSizeColor(uint64_t size, uint64_t maxSize);
    
    HWND m_hwnd;
    HWND m_treeView;
    HWND m_listView;
    HWND m_statusBar;
    HWND m_toolbar;
    HWND m_splitter;
    
    std::unique_ptr<RibbonBar> m_ribbon;
    std::unique_ptr<FolderScanner> m_scanner;
    std::shared_ptr<FileSystemItem> m_rootItem;
    std::vector<std::shared_ptr<FileSystemItem>> m_flatList;
    
    bool m_sortDescending;
    uint64_t m_maxSize;
    int m_splitterPos;
    bool m_splitterDragging;
    ViewMode m_viewMode;
    
    static constexpr int ID_BROWSE = 1001;
    static constexpr int ID_REFRESH = 1002;
    static constexpr int ID_THEME = 1003;
    static constexpr int ID_VIEW_DETAILS = 10;
    static constexpr int ID_VIEW_LIST = 11;
    static constexpr int ID_VIEW_ICONS = 12;
    static constexpr int ID_LISTVIEW = 2001;
    static constexpr int ID_TREEVIEW = 2002;
    static constexpr int ID_SPLITTER = 2003;
    static constexpr int SPLITTER_WIDTH = 4;
};
