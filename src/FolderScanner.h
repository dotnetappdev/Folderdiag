#pragma once
#include "FileSystemItem.h"
#include <functional>
#include <atomic>

class FolderScanner {
public:
    using ProgressCallback = std::function<void(const std::wstring&)>;
    using CompletionCallback = std::function<void(std::shared_ptr<FileSystemItem>)>;
    
    FolderScanner();
    ~FolderScanner();
    
    // Scan a folder and build the file system tree
    void ScanAsync(const std::wstring& path, ProgressCallback progressCallback, CompletionCallback completionCallback);
    
    // Cancel ongoing scan
    void Cancel();
    
    // Check if scan is in progress
    bool IsScanning() const { return m_isScanning; }
    
private:
    std::shared_ptr<FileSystemItem> ScanDirectory(const std::wstring& path);
    uint64_t CalculateDirectorySize(std::shared_ptr<FileSystemItem> item);
    
    std::atomic<bool> m_isScanning;
    std::atomic<bool> m_cancelRequested;
    ProgressCallback m_progressCallback;
};
