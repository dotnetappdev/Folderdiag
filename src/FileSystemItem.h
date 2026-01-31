#pragma once
#include <string>
#include <vector>
#include <memory>
#include <cstdint>

class FileSystemItem {
public:
    enum class ItemType {
        File,
        Directory
    };

    FileSystemItem(const std::wstring& name, const std::wstring& path, ItemType type);
    
    // Getters
    const std::wstring& GetName() const { return m_name; }
    const std::wstring& GetPath() const { return m_path; }
    ItemType GetType() const { return m_type; }
    uint64_t GetSize() const { return m_size; }
    uint64_t GetFileCount() const { return m_fileCount; }
    uint64_t GetDirectoryCount() const { return m_directoryCount; }
    
    // Setters
    void SetSize(uint64_t size) { m_size = size; }
    void AddSize(uint64_t size) { m_size += size; }
    void SetFileCount(uint64_t count) { m_fileCount = count; }
    void SetDirectoryCount(uint64_t count) { m_directoryCount = count; }
    void IncrementFileCount() { m_fileCount++; }
    void IncrementDirectoryCount() { m_directoryCount++; }
    
    // Children management
    void AddChild(std::shared_ptr<FileSystemItem> child);
    const std::vector<std::shared_ptr<FileSystemItem>>& GetChildren() const { return m_children; }
    
    // Formatting
    std::wstring GetSizeFormatted() const;
    
private:
    std::wstring m_name;
    std::wstring m_path;
    ItemType m_type;
    uint64_t m_size;
    uint64_t m_fileCount;
    uint64_t m_directoryCount;
    std::vector<std::shared_ptr<FileSystemItem>> m_children;
};
