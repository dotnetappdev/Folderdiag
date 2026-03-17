using FolderDiag.Core.Models;

namespace FolderDiag.Core.Services;

public interface IFileSystemScanner
{
    Task<ScanResult> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<FileSystemEntry?> ScanDirectoryAsync(
        string path,
        int depth = 0,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    IEnumerable<DriveInfo> GetDrives();
}
