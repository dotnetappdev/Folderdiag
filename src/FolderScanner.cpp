#include "FolderScanner.h"
#include <windows.h>
#include <thread>
#include <algorithm>

FolderScanner::FolderScanner() : m_isScanning(false), m_cancelRequested(false) {
}

FolderScanner::~FolderScanner() {
    Cancel();
}

void FolderScanner::ScanAsync(const std::wstring& path, ProgressCallback progressCallback, CompletionCallback completionCallback) {
    if (m_isScanning) {
        return;
    }
    
    m_progressCallback = progressCallback;
    m_isScanning = true;
    m_cancelRequested = false;
    
    std::thread scanThread([this, path, completionCallback]() {
        auto root = ScanDirectory(path);
        
        if (root && !m_cancelRequested) {
            CalculateDirectorySize(root);
        }
        
        m_isScanning = false;
        
        if (completionCallback && !m_cancelRequested) {
            completionCallback(root);
        }
    });
    
    scanThread.detach();
}

void FolderScanner::Cancel() {
    m_cancelRequested = true;
}

std::shared_ptr<FileSystemItem> FolderScanner::ScanDirectory(const std::wstring& path) {
    if (m_cancelRequested) {
        return nullptr;
    }
    
    auto dirItem = std::make_shared<FileSystemItem>(
        path.substr(path.find_last_of(L"\\/") + 1),
        path,
        FileSystemItem::ItemType::Directory
    );
    
    if (m_progressCallback) {
        m_progressCallback(path);
    }
    
    std::wstring searchPath = path + L"\\*";
    WIN32_FIND_DATAW findData;
    HANDLE hFind = FindFirstFileW(searchPath.c_str(), &findData);
    
    if (hFind == INVALID_HANDLE_VALUE) {
        return dirItem;
    }
    
    do {
        if (m_cancelRequested) {
            FindClose(hFind);
            return nullptr;
        }
        
        std::wstring fileName = findData.cFileName;
        
        if (fileName == L"." || fileName == L"..") {
            continue;
        }
        
        std::wstring fullPath = path + L"\\" + fileName;
        
        if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            auto childDir = ScanDirectory(fullPath);
            if (childDir) {
                dirItem->AddChild(childDir);
                dirItem->IncrementDirectoryCount();
            }
        } else {
            LARGE_INTEGER fileSize;
            fileSize.LowPart = findData.nFileSizeLow;
            fileSize.HighPart = findData.nFileSizeHigh;
            
            auto fileItem = std::make_shared<FileSystemItem>(
                fileName,
                fullPath,
                FileSystemItem::ItemType::File
            );
            fileItem->SetSize(fileSize.QuadPart);
            
            dirItem->AddChild(fileItem);
            dirItem->IncrementFileCount();
        }
    } while (FindNextFileW(hFind, &findData) != 0);
    
    FindClose(hFind);
    return dirItem;
}

uint64_t FolderScanner::CalculateDirectorySize(std::shared_ptr<FileSystemItem> item) {
    if (!item || m_cancelRequested) {
        return 0;
    }
    
    uint64_t totalSize = 0;
    uint64_t totalFiles = 0;
    uint64_t totalDirs = 0;
    
    for (const auto& child : item->GetChildren()) {
        if (child->GetType() == FileSystemItem::ItemType::Directory) {
            uint64_t childSize = CalculateDirectorySize(child);
            totalSize += childSize;
            totalFiles += child->GetFileCount();
            totalDirs += child->GetDirectoryCount() + 1; // +1 for the directory itself
        } else {
            totalSize += child->GetSize();
        }
    }
    
    item->AddSize(totalSize);
    item->SetFileCount(item->GetFileCount() + totalFiles);
    item->SetDirectoryCount(totalDirs);
    
    return item->GetSize();
}
