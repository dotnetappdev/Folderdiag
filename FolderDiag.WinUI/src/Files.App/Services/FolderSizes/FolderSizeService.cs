// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Files.App.Services.FolderSizes
{
    /// <summary>
    /// Performs recursive folder size analysis producing per-folder results
    /// compatible with the Files app ListedItem model.
    /// </summary>
    public sealed class FolderSizeService : IFolderSizeService
    {
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<string, FolderSizeResult> _cache = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<FolderSizeProgressEventArgs>? ProgressChanged;

        // ── Public API ────────────────────────────────────────────────────

        public async Task<FolderSizeResult> AnalyzeAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cache.Clear();
            return await Task.Run(() => ScanDirectory(rootPath, null, 0, _cts.Token), _cts.Token);
        }

        public async Task<IList<FileReportItem>> GetLargestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default)
        {
            return await Task.Run(() =>
                EnumerateFiles(rootPath, ct)
                    .OrderByDescending(f => f.SizeBytes)
                    .Take(topN)
                    .ToList(), ct);
        }

        public async Task<IList<FileReportItem>> GetOldestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default)
        {
            return await Task.Run(() =>
                EnumerateFiles(rootPath, ct)
                    .OrderBy(f => f.Modified)
                    .Take(topN)
                    .ToList(), ct);
        }

        public async Task<IList<FileReportItem>> GetTempFilesAsync(string rootPath, CancellationToken ct = default)
        {
            return await Task.Run(() =>
                EnumerateFiles(rootPath, ct)
                    .Where(f => f.IsTemp)
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList(), ct);
        }

        public async Task<IList<FileReportItem>> GetDuplicateFilesAsync(string rootPath, CancellationToken ct = default)
        {
            return await Task.Run(() =>
                EnumerateFiles(rootPath, ct)
                    .GroupBy(f => f.SizeBytes)
                    .Where(g => g.Count() > 1 && g.Key > 0)
                    .SelectMany(g => g)
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList(), ct);
        }

        public async Task<IList<TypeClassificationItem>> GetClassificationByTypeAsync(string rootPath, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);

                return files
                    .GroupBy(f => FileReportItem.GetCategory(f.Extension))
                    .Select(g => new TypeClassificationItem
                    {
                        Category  = g.Key,
                        Count     = g.Count(),
                        TotalSize = g.Sum(f => f.SizeBytes),
                        Percent   = grand > 0 ? (double)g.Sum(f => f.SizeBytes) / grand * 100.0 : 0
                    })
                    .OrderByDescending(t => t.TotalSize)
                    .ToList();
            }, ct);
        }

        public void Cancel() => _cts?.Cancel();

        public void Clear() => _cache.Clear();

        // ── Core recursive scan ───────────────────────────────────────────

        private FolderSizeResult ScanDirectory(string path, string? parentPath, int depth, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            long totalSize = 0, totalAlloc = 0;
            int  fileCount = 0, folderCount = 0;

            // Files in this directory
            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    totalSize  += fi.Length;
                    totalAlloc += AllocatedSize(fi.Length);
                    fileCount++;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException)  { }

            // Recurse subdirectories
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    folderCount++;
                    var child = ScanDirectory(dir, path, depth + 1, ct);
                    totalSize    += child.SizeBytes;
                    totalAlloc   += child.AllocatedBytes;
                    fileCount    += child.FileCount;
                    folderCount  += child.FolderCount;
                    _cache[dir]   = child;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException)  { }

            var result = new FolderSizeResult
            {
                Path           = path,
                Name           = depth == 0 ? path : System.IO.Path.GetFileName(path),
                ParentPath     = parentPath,
                SizeBytes      = totalSize,
                AllocatedBytes = totalAlloc,
                FileCount      = fileCount,
                FolderCount    = folderCount,
                Depth          = depth,
                ScannedAt      = DateTime.Now
            };

            _cache[path] = result;

            ProgressChanged?.Invoke(this, new FolderSizeProgressEventArgs
            {
                CurrentPath    = path,
                TotalSizeBytes = totalSize,
                FileCount      = fileCount,
                FolderCount    = folderCount
            });

            return result;
        }

        private static IEnumerable<FileReportItem> EnumerateFiles(string rootPath, CancellationToken ct)
        {
            if (!Directory.Exists(rootPath)) yield break;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories); }
            catch { yield break; }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                FileInfo? fi = null;
                try { fi = new FileInfo(file); } catch { continue; }

                string ext = fi.Extension.ToLowerInvariant();
                yield return new FileReportItem
                {
                    Name       = fi.Name,
                    Path       = fi.FullName,
                    FolderPath = fi.DirectoryName ?? "",
                    Extension  = ext,
                    SizeBytes  = fi.Length,
                    Modified   = fi.LastWriteTime,
                    Created    = fi.CreationTime,
                    IsTemp     = ext is ".tmp" or ".temp" or ".bak" or ".log" or ".dmp" or ".old",
                    Owner      = TryGetOwner(fi.FullName)
                };
            }
        }

        private bool TryGetSize(string path, out ulong size)
        {
            if (_cache.TryGetValue(path, out var r)) { size = (ulong)r.SizeBytes; return true; }
            size = 0; return false;
        }

        private static long AllocatedSize(long bytes, long cluster = 4096)
            => ((bytes + cluster - 1) / cluster) * cluster;

        private static string? TryGetOwner(string path)
        {
            try
            {
                var fs = new FileInfo(path).GetAccessControl();
                return fs.GetOwner(typeof(NTAccount))?.ToString();
            }
            catch { return null; }
        }
    }
}

// Extension for ViewModel to retrieve cached sub-dir results
public partial class FolderSizeService
{
    public bool TryGetCached(string path, out FolderSizeResult result)
    {
        if (_cache.TryGetValue(path, out var r)) { result = r; return true; }
        result = null!; return false;
    }
}
