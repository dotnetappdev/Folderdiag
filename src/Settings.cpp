#include "Settings.h"
#include <shlobj.h>
#include <fstream>
#include <sstream>

Settings& Settings::Instance() {
    static Settings instance;
    return instance;
}

Settings::Settings() {
    InitializeDefaultColors();
    Load();
}

Settings::~Settings() {
    Save();
}

void Settings::InitializeDefaultColors() {
    m_colorThresholds.clear();
    
    // Default color scheme: Blue -> Cyan -> Green -> Yellow -> Orange -> Red
    m_colorThresholds.push_back(ColorThreshold(0.0, RGB(100, 150, 220)));  // Blue (0-10%)
    m_colorThresholds.push_back(ColorThreshold(0.1, RGB(80, 180, 200)));   // Cyan (10-20%)
    m_colorThresholds.push_back(ColorThreshold(0.2, RGB(100, 200, 100)));  // Green (20-40%)
    m_colorThresholds.push_back(ColorThreshold(0.4, RGB(255, 200, 50)));   // Yellow (40-60%)
    m_colorThresholds.push_back(ColorThreshold(0.6, RGB(255, 140, 50)));   // Orange (60-80%)
    m_colorThresholds.push_back(ColorThreshold(0.8, RGB(220, 50, 50)));    // Red (80-100%)
}

void Settings::SetColorThresholds(const std::vector<ColorThreshold>& thresholds) {
    m_colorThresholds = thresholds;
    Save();
}

void Settings::ResetToDefaultColors() {
    InitializeDefaultColors();
    Save();
}

std::wstring Settings::GetSettingsPath() {
    wchar_t path[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, path))) {
        std::wstring settingsPath = path;
        settingsPath += L"\\FoldersDiag";
        CreateDirectoryW(settingsPath.c_str(), NULL);
        settingsPath += L"\\settings.ini";
        return settingsPath;
    }
    return L"";
}

bool Settings::Load() {
    std::wstring path = GetSettingsPath();
    if (path.empty()) return false;
    
    std::ifstream file(path);
    if (!file.is_open()) return false;
    
    std::vector<ColorThreshold> tempThresholds;
    std::string line;
    
    while (std::getline(file, line)) {
        if (line.empty() || line[0] == '#') continue;
        
        std::istringstream iss(line);
        double percentage;
        int r, g, b;
        char comma;
        
        if (iss >> percentage >> comma >> r >> comma >> g >> comma >> b) {
            tempThresholds.push_back(ColorThreshold(percentage, RGB(r, g, b)));
        }
    }
    
    if (!tempThresholds.empty()) {
        m_colorThresholds = tempThresholds;
        return true;
    }
    
    return false;
}

bool Settings::Save() {
    std::wstring path = GetSettingsPath();
    if (path.empty()) return false;
    
    std::ofstream file(path);
    if (!file.is_open()) return false;
    
    file << "# FoldersDiag Color Settings\n";
    file << "# Format: percentage,R,G,B\n";
    file << "# Percentage: 0.0 to 1.0 (0% to 100%)\n\n";
    
    for (const auto& threshold : m_colorThresholds) {
        file << threshold.percentage << ","
             << GetRValue(threshold.color) << ","
             << GetGValue(threshold.color) << ","
             << GetBValue(threshold.color) << "\n";
    }
    
    return true;
}
