#pragma once
#include <windows.h>
#include <commctrl.h>
#include "FolderScanner.h"
#include "FileSystemItem.h"
#include <memory>
#include <vector>

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
    void UpdateStatusBar(const std::wstring& text);
    
    // Custom draw support for progress bars
    void DrawProgressBar(HDC hdc, RECT rect, uint64_t size, uint64_t maxSize);
    COLORREF GetSizeColor(uint64_t size, uint64_t maxSize);
    
    HWND m_hwnd;
    HWND m_listView;
    HWND m_statusBar;
    HWND m_toolbar;
    
    std::unique_ptr<FolderScanner> m_scanner;
    std::shared_ptr<FileSystemItem> m_rootItem;
    std::vector<std::shared_ptr<FileSystemItem>> m_flatList;
    
    bool m_sortDescending;
    uint64_t m_maxSize;
    
    static constexpr int ID_BROWSE = 1001;
    static constexpr int ID_REFRESH = 1002;
    static constexpr int ID_THEME = 1003;
    static constexpr int ID_LISTVIEW = 2001;
};
