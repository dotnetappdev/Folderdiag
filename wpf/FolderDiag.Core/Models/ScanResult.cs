namespace FolderDiag.Core.Models;

public sealed class ScanResult
{
    public string RootPath { get; set; } = string.Empty;
    public FileSystemEntry? Root { get; set; }
    public long TotalSize { get; set; }
    public int TotalFiles { get; set; }
    public int TotalFolders { get; set; }
    public TimeSpan ScanDuration { get; set; }
    public DateTime ScanTime { get; set; } = DateTime.UtcNow;
    public bool IsComplete { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ScanProgress
{
    public string CurrentPath { get; set; } = string.Empty;
    public int FilesScanned { get; set; }
    public int FoldersScanned { get; set; }
    public long BytesScanned { get; set; }
}
