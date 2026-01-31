#include "MainWindow.h"
#include <windows.h>
#include <objbase.h>

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, 
                    PWSTR pCmdLine, int nCmdShow) {
    // Initialize COM for file dialog
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    if (FAILED(hr)) {
        return -1;
    }
    
    MainWindow window;
    if (!window.Create()) {
        CoUninitialize();
        return -1;
    }
    
    window.Show(nCmdShow);
    int result = window.MessageLoop();
    
    CoUninitialize();
    return result;
}
