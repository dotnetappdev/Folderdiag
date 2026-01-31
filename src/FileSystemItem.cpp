#include "FileSystemItem.h"
#include <sstream>
#include <iomanip>

FileSystemItem::FileSystemItem(const std::wstring& name, const std::wstring& path, ItemType type)
    : m_name(name), m_path(path), m_type(type), m_size(0), m_fileCount(0), m_directoryCount(0) {
}

void FileSystemItem::AddChild(std::shared_ptr<FileSystemItem> child) {
    m_children.push_back(child);
}

std::wstring FileSystemItem::GetSizeFormatted() const {
    const wchar_t* units[] = { L"B", L"KB", L"MB", L"GB", L"TB" };
    double size = static_cast<double>(m_size);
    int unitIndex = 0;
    
    while (size >= 1024.0 && unitIndex < 4) {
        size /= 1024.0;
        unitIndex++;
    }
    
    std::wostringstream oss;
    oss << std::fixed << std::setprecision(2) << size << L" " << units[unitIndex];
    return oss.str();
}
