#pragma once
#include <windows.h>
#include <string>
#include <vector>

struct ColorThreshold {
    double percentage;  // 0.0 to 1.0
    COLORREF color;
    
    ColorThreshold(double p = 0.0, COLORREF c = RGB(0, 0, 0)) 
        : percentage(p), color(c) {}
};

class Settings {
public:
    static Settings& Instance();
    
    // Color threshold management
    const std::vector<ColorThreshold>& GetColorThresholds() const { return m_colorThresholds; }
    void SetColorThresholds(const std::vector<ColorThreshold>& thresholds);
    void ResetToDefaultColors();
    
    // Persistence
    bool Load();
    bool Save();
    
private:
    Settings();
    ~Settings();
    
    void InitializeDefaultColors();
    std::wstring GetSettingsPath();
    
    std::vector<ColorThreshold> m_colorThresholds;
};
