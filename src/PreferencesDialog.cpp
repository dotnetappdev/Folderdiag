#include "PreferencesDialog.h"
#include "ThemeManager.h"
#include <commctrl.h>
#include <sstream>

std::vector<ColorThreshold> PreferencesDialog::s_tempThresholds;

bool PreferencesDialog::Show(HWND hwndParent) {
    s_tempThresholds = Settings::Instance().GetColorThresholds();
    
    INT_PTR result = DialogBoxW(
        GetModuleHandle(NULL),
        MAKEINTRESOURCEW(IDD_PREFERENCES),
        hwndParent,
        DialogProc
    );
    
    return result == IDOK;
}

INT_PTR CALLBACK PreferencesDialog::DialogProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
        case WM_INITDIALOG:
            InitDialog(hwnd);
            return TRUE;
            
        case WM_COMMAND:
            switch (LOWORD(wParam)) {
                case IDOK:
                    SaveSettings(hwnd);
                    EndDialog(hwnd, IDOK);
                    return TRUE;
                    
                case IDCANCEL:
                    EndDialog(hwnd, IDCANCEL);
                    return TRUE;
                    
                case IDC_RESET_COLORS:
                    ResetToDefaults(hwnd);
                    return TRUE;
                    
                case IDC_COLOR1:
                case IDC_COLOR2:
                case IDC_COLOR3:
                case IDC_COLOR4:
                case IDC_COLOR5:
                case IDC_COLOR6:
                    ChooseColor(hwnd, LOWORD(wParam) - IDC_COLOR1);
                    return TRUE;
            }
            break;
            
        case WM_CTLCOLORSTATIC: {
            HDC hdcStatic = (HDC)wParam;
            HWND hwndStatic = (HWND)lParam;
            int ctrlId = GetDlgCtrlID(hwndStatic);
            
            if (ctrlId >= IDC_PREVIEW1 && ctrlId <= IDC_PREVIEW6) {
                int index = ctrlId - IDC_PREVIEW1;
                if (index < (int)s_tempThresholds.size()) {
                    SetBkColor(hdcStatic, s_tempThresholds[index].color);
                    static HBRUSH hbrush = NULL;
                    if (hbrush) DeleteObject(hbrush);
                    hbrush = CreateSolidBrush(s_tempThresholds[index].color);
                    return (INT_PTR)hbrush;
                }
            }
            break;
        }
    }
    
    return FALSE;
}

void PreferencesDialog::InitDialog(HWND hwnd) {
    // Set dialog title
    SetWindowTextW(hwnd, L"Color Preferences");
    
    // Update all color previews and labels
    for (size_t i = 0; i < s_tempThresholds.size(); ++i) {
        UpdateColorPreview(hwnd, i);
    }
}

void PreferencesDialog::UpdateColorPreview(HWND hwnd, int index) {
    if (index >= (int)s_tempThresholds.size()) return;
    
    // Update percentage label
    int labelId = IDC_LABEL1 + index;
    double startPct = s_tempThresholds[index].percentage * 100;
    double endPct = (index + 1 < (int)s_tempThresholds.size()) 
                    ? s_tempThresholds[index + 1].percentage * 100 
                    : 100.0;
    
    std::wostringstream oss;
    oss << L"Zone " << (index + 1) << L" (" 
        << (int)startPct << L"% - " << (int)endPct << L"%):";
    
    SetDlgItemTextW(hwnd, labelId, oss.str().c_str());
    
    // Force preview to redraw
    InvalidateRect(GetDlgItem(hwnd, IDC_PREVIEW1 + index), NULL, TRUE);
}

void PreferencesDialog::ChooseColor(HWND hwnd, int index) {
    if (index >= (int)s_tempThresholds.size()) return;
    
    CHOOSECOLOR cc = {};
    static COLORREF customColors[16] = {0};
    
    cc.lStructSize = sizeof(CHOOSECOLOR);
    cc.hwndOwner = hwnd;
    cc.lpCustColors = customColors;
    cc.rgbResult = s_tempThresholds[index].color;
    cc.Flags = CC_FULLOPEN | CC_RGBINIT;
    
    if (ChooseColorW(&cc)) {
        s_tempThresholds[index].color = cc.rgbResult;
        UpdateColorPreview(hwnd, index);
    }
}

void PreferencesDialog::SaveSettings(HWND hwnd) {
    Settings::Instance().SetColorThresholds(s_tempThresholds);
}

void PreferencesDialog::ResetToDefaults(HWND hwnd) {
    Settings::Instance().ResetToDefaultColors();
    s_tempThresholds = Settings::Instance().GetColorThresholds();
    
    for (size_t i = 0; i < s_tempThresholds.size(); ++i) {
        UpdateColorPreview(hwnd, i);
    }
}
