using System.Collections.ObjectModel;
using FolderDiag.Core.Helpers;

namespace FolderDiag.Core.Models;

public sealed class FileSystemEntry
{
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }           // Own file size (bytes)
    public long TotalSize { get; set; }      // Including all children
    public bool IsDirectory { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSystem { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastAccessed { get; set; }
    public string Extension { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public double PercentOfParent { get; set; }
    public FileSystemEntry? Parent { get; set; }
    public ObservableCollection<FileSystemEntry> Children { get; } = [];
    public string FileType { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool IsScanning { get; set; }
    public bool ScanComplete { get; set; }

    // Computed
    public string SizeFormatted => FileSizeHelper.Format(TotalSize > 0 ? TotalSize : Size);
    public string OwnSizeFormatted => FileSizeHelper.Format(Size);
    public string TotalSizeFormatted => FileSizeHelper.Format(TotalSize);
    public string PercentFormatted => $"{PercentOfParent:F1}%";
    public string TotalItemsFormatted => IsDirectory
        ? $"{FileCount:N0} files, {FolderCount:N0} folders"
        : string.Empty;
}
