#pragma once
#include <windows.h>
#include "Settings.h"

class PreferencesDialog {
public:
    static bool Show(HWND hwndParent);
    
private:
    static INT_PTR CALLBACK DialogProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
    static void InitDialog(HWND hwnd);
    static void UpdateColorPreview(HWND hwnd, int index);
    static void ChooseColor(HWND hwnd, int index);
    static void SaveSettings(HWND hwnd);
    static void ResetToDefaults(HWND hwnd);
    
    static std::vector<ColorThreshold> s_tempThresholds;
};
