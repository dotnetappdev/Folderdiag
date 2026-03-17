using System.Collections.Concurrent;
using FolderDiag.Core.Models;
using Microsoft.Extensions.Logging;

namespace FolderDiag.Core.Services;

public sealed class FileSystemScanner : IFileSystemScanner
{
    private readonly ILogger<FileSystemScanner> _logger;
    private int _filesScanned;
    private int _foldersScanned;
    private long _bytesScanned;

    public FileSystemScanner(ILogger<FileSystemScanner> logger)
    {
        _logger = logger;
    }

    public IEnumerable<DriveInfo> GetDrives()
    {
        return DriveInfo.GetDrives().Where(d => d.IsReady);
    }

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _filesScanned = 0;
        _foldersScanned = 0;
        _bytesScanned = 0;

        var result = new ScanResult { RootPath = rootPath };
        try
        {
            result.Root = await ScanDirectoryAsync(rootPath, 0, progress, cancellationToken);
            if (result.Root != null)
            {
                result.TotalSize = result.Root.TotalSize;
                result.TotalFiles = result.Root.FileCount;
                result.TotalFolders = result.Root.FolderCount;
                result.IsComplete = true;
            }
        }
        catch (OperationCanceledException)
        {
            result.IsComplete = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed for {Path}", rootPath);
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ScanDuration = sw.Elapsed;
        return result;
    }

    public async Task<FileSystemEntry?> ScanDirectoryAsync(
        string path,
        int depth = 0,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists) return null;

            var entry = new FileSystemEntry
            {
                FullPath = dirInfo.FullName,
                Name = depth == 0 ? dirInfo.FullName : dirInfo.Name,
                IsDirectory = true,
                Created = SafeGetDate(() => dirInfo.CreationTime),
                LastModified = SafeGetDate(() => dirInfo.LastWriteTime),
                LastAccessed = SafeGetDate(() => dirInfo.LastAccessTime),
                IsHidden = (dirInfo.Attributes & FileAttributes.Hidden) != 0,
                IsSystem = (dirInfo.Attributes & FileAttributes.System) != 0,
                IsScanning = true
            };

            Interlocked.Increment(ref _foldersScanned);

            // Report progress every 50 folders
            if (_foldersScanned % 50 == 0)
            {
                progress?.Report(new ScanProgress
                {
                    CurrentPath = path,
                    FilesScanned = _filesScanned,
                    FoldersScanned = _foldersScanned,
                    BytesScanned = _bytesScanned
                });
            }

            // Get files in this directory
            FileInfo[] files = [];
            try
            {
                files = dirInfo.GetFiles();
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            long ownSize = 0;
            foreach (var fi in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fileEntry = new FileSystemEntry
                    {
                        FullPath = fi.FullName,
                        Name = fi.Name,
                        IsDirectory = false,
                        Size = fi.Length,
                        TotalSize = fi.Length,
                        Extension = fi.Extension.ToLowerInvariant(),
                        Created = SafeGetDate(() => fi.CreationTime),
                        LastModified = SafeGetDate(() => fi.LastWriteTime),
                        LastAccessed = SafeGetDate(() => fi.LastAccessTime),
                        IsHidden = (fi.Attributes & FileAttributes.Hidden) != 0,
                        IsSystem = (fi.Attributes & FileAttributes.System) != 0,
                        Parent = entry,
                        ScanComplete = true
                    };
                    fileEntry.FileType = GetFileType(fi.Extension);
                    entry.Children.Add(fileEntry);
                    ownSize += fi.Length;
                    Interlocked.Increment(ref _filesScanned);
                    Interlocked.Add(ref _bytesScanned, fi.Length);
                }
                catch { /* skip inaccessible files */ }
            }

            entry.Size = ownSize;
            entry.FileCount = files.Length;

            // Scan subdirectories in parallel (bounded)
            DirectoryInfo[] subdirs = [];
            try
            {
                subdirs = dirInfo.GetDirectories();
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            entry.FolderCount = subdirs.Length;

            // Use bounded parallelism: top-level gets more threads, deep gets fewer
            int maxParallel = depth == 0 ? Environment.ProcessorCount : Math.Max(1, Environment.ProcessorCount / 2);

            var subTasks = new List<Task<FileSystemEntry?>>(subdirs.Length);
            using var semaphore = new SemaphoreSlim(maxParallel);

            foreach (var sub in subdirs)
            {
                await semaphore.WaitAsync(cancellationToken);
                var subDir = sub;
                subTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await ScanDirectoryAsync(subDir.FullName, depth + 1, progress, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            var subResults = await Task.WhenAll(subTasks);

            long childrenTotalSize = 0;
            int totalFiles = entry.FileCount;
            int totalFolders = entry.FolderCount;

            foreach (var sub in subResults)
            {
                if (sub == null) continue;
                sub.Parent = entry;
                entry.Children.Add(sub);
                childrenTotalSize += sub.TotalSize;
                totalFiles += sub.FileCount;
                totalFolders += sub.FolderCount;
            }

            entry.TotalSize = ownSize + childrenTotalSize;
            entry.FileCount = totalFiles;
            entry.FolderCount = totalFolders;

            // Sort children by total size descending
            var sorted = entry.Children.OrderByDescending(c => c.TotalSize).ToList();
            entry.Children.Clear();
            foreach (var c in sorted) entry.Children.Add(c);

            // Calculate percentages
            if (entry.TotalSize > 0)
            {
                foreach (var child in entry.Children)
                    child.PercentOfParent = child.TotalSize / (double)entry.TotalSize * 100.0;
            }

            entry.IsScanning = false;
            entry.ScanComplete = true;
            return entry;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not scan directory {Path}", path);
            return null;
        }
    }

    private static DateTime SafeGetDate(Func<DateTime> getter)
    {
        try { return getter(); }
        catch { return DateTime.MinValue; }
    }

    private static string GetFileType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" => "Image",
        ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => "Video",
        ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => "Audio",
        ".pdf" => "PDF",
        ".doc" or ".docx" => "Word Document",
        ".xls" or ".xlsx" => "Excel Spreadsheet",
        ".ppt" or ".pptx" => "PowerPoint",
        ".txt" or ".log" or ".md" => "Text",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "Archive",
        ".exe" or ".msi" => "Executable",
        ".dll" => "Library",
        ".cs" or ".vb" or ".fs" => "C# Source",
        ".js" or ".ts" => "JavaScript",
        ".py" => "Python",
        ".cpp" or ".c" or ".h" => "C/C++",
        ".xml" or ".json" or ".yaml" or ".yml" => "Data",
        ".html" or ".htm" or ".css" => "Web",
        ".iso" or ".img" or ".vhd" or ".vmdk" => "Disk Image",
        "" => "File",
        _ => $"{ext.TrimStart('.').ToUpperInvariant()} File"
    };
}
