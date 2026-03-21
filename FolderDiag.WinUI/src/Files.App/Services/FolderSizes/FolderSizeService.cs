// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Files.App.Services.FolderSizes
{
    /// <summary>
    /// Full implementation of IFolderSizeService — provides all FolderSizes-compatible
    /// reports: folder sizes, file reports (largest/oldest/newest/temp/duplicates),
    /// classification (types/owners/attributes), distribution analysis (age/depth/size),
    /// treemap chart data, advanced search, and multi-format export.
    /// </summary>
    public sealed class FolderSizeService : IFolderSizeService
    {
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<string, FolderSizeResult> _cache = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<FolderSizeProgressEventArgs>? ProgressChanged;

        // ── Public cached lookup (used by ViewModel) ──────────────────────────

        public bool TryGetCached(string path, out FolderSizeResult result)
        {
            if (_cache.TryGetValue(path, out var r)) { result = r; return true; }
            result = null!;
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Core scan
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<FolderSizeResult> AnalyzeAsync(string rootPath, CancellationToken ct = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _cache.Clear();
            return await Task.Run(() => ScanDirectory(rootPath, null, 0, _cts.Token), _cts.Token);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // File reports
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<IList<FileReportItem>> GetLargestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default)
            => await Task.Run(() => EnumerateFiles(rootPath, ct).OrderByDescending(f => f.SizeBytes).Take(topN).ToList(), ct);

        public async Task<IList<FileReportItem>> GetOldestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default)
            => await Task.Run(() => EnumerateFiles(rootPath, ct).Where(f => f.Modified.HasValue).OrderBy(f => f.Modified).Take(topN).ToList(), ct);

        public async Task<IList<FileReportItem>> GetNewestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default)
            => await Task.Run(() => EnumerateFiles(rootPath, ct).Where(f => f.Modified.HasValue).OrderByDescending(f => f.Modified).Take(topN).ToList(), ct);

        public async Task<IList<FileReportItem>> GetTempFilesAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() => EnumerateFiles(rootPath, ct).Where(f => f.IsTemp).OrderByDescending(f => f.SizeBytes).ToList(), ct);

        public async Task<IList<FileReportItem>> GetDuplicateFilesAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
                EnumerateFiles(rootPath, ct)
                    .GroupBy(f => f.SizeBytes)
                    .Where(g => g.Count() > 1 && g.Key > 0)
                    .SelectMany(g => g)
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList(), ct);

        // ═══════════════════════════════════════════════════════════════════════
        // Classification reports
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<IList<TypeClassificationItem>> GetClassificationByTypeAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
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
                        Percent   = grand > 0 ? 100.0 * g.Sum(f => f.SizeBytes) / grand : 0
                    })
                    .OrderByDescending(t => t.TotalSize)
                    .ToList<TypeClassificationItem>();
            }, ct);

        public async Task<IList<FileOwnerItem>> GetFilesByOwnerAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);
                return files
                    .GroupBy(f => f.Owner ?? "Unknown")
                    .Select(g => new FileOwnerItem
                    {
                        Owner     = g.Key,
                        Count     = g.Count(),
                        TotalSize = g.Sum(f => f.SizeBytes),
                        Percent   = grand > 0 ? 100.0 * g.Sum(f => f.SizeBytes) / grand : 0
                    })
                    .OrderByDescending(o => o.TotalSize)
                    .ToList<FileOwnerItem>();
            }, ct);

        public async Task<IList<FileAttributeItem>> GetFileAttributesReportAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);

                var groups = new[]
                {
                    ("Normal",    files.Where(f => !f.IsReadOnly && !f.IsHidden && !f.IsSystem).ToList(), "\uE8A5"),
                    ("Read-Only", files.Where(f => f.IsReadOnly).ToList(),  "\uE785"),
                    ("Hidden",    files.Where(f => f.IsHidden).ToList(),    "\uED1A"),
                    ("System",    files.Where(f => f.IsSystem).ToList(),    "\uE9F5"),
                    ("Temp/Junk", files.Where(f => f.IsTemp).ToList(),      "\uE74D"),
                };

                return groups
                    .Where(g => g.Item2.Count > 0)
                    .Select(g => new FileAttributeItem
                    {
                        AttributeLabel = g.Item1,
                        Count          = g.Item2.Count,
                        TotalSize      = g.Item2.Sum(f => f.SizeBytes),
                        Percent        = grand > 0 ? 100.0 * g.Item2.Sum(f => f.SizeBytes) / grand : 0,
                        Glyph          = g.Item3
                    })
                    .OrderByDescending(a => a.TotalSize)
                    .ToList<FileAttributeItem>();
            }, ct);

        // ═══════════════════════════════════════════════════════════════════════
        // Distribution reports
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<IList<FileAgeGroupItem>> GetFileAgeDistributionAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);

                var buckets = new (string Label, int MinDays, int MaxDays)[]
                {
                    ("< 1 day",      0,   1),
                    ("1–7 days",     1,   7),
                    ("1–4 weeks",    7,  30),
                    ("1–3 months",  30,  90),
                    ("3–12 months", 90, 365),
                    ("> 1 year",   365, int.MaxValue),
                };

                return buckets.Select(b =>
                {
                    var grp = files.Where(f => f.AgeInDays >= b.MinDays && f.AgeInDays < b.MaxDays).ToList();
                    return new FileAgeGroupItem
                    {
                        Label     = b.Label,
                        Count     = grp.Count,
                        TotalSize = grp.Sum(f => f.SizeBytes),
                        Percent   = grand > 0 ? 100.0 * grp.Sum(f => f.SizeBytes) / grand : 0
                    };
                })
                .Where(b => b.Count > 0)
                .ToList<FileAgeGroupItem>();
            }, ct);

        public async Task<IList<FileDepthItem>> GetFileDepthReportAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);

                return files
                    .GroupBy(f => f.Depth)
                    .Select(g => new FileDepthItem
                    {
                        Depth     = g.Key,
                        FileCount = g.Count(),
                        TotalSize = g.Sum(f => f.SizeBytes),
                        Percent   = grand > 0 ? 100.0 * g.Sum(f => f.SizeBytes) / grand : 0
                    })
                    .OrderBy(d => d.Depth)
                    .ToList<FileDepthItem>();
            }, ct);

        public async Task<IList<FileSizeGroupItem>> GetFileSizeDistributionAsync(string rootPath, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                var files = EnumerateFiles(rootPath, ct).ToList();
                long grand = files.Sum(f => f.SizeBytes);

                var buckets = new (string Label, long MinBytes, long MaxBytes)[]
                {
                    ("Empty (0 B)",      0,       1),
                    ("< 1 KB",           1,    1024),
                    ("1 KB – 100 KB",    1024,       102_400),
                    ("100 KB – 1 MB",    102_400,  1_048_576),
                    ("1 MB – 100 MB",    1_048_576, 104_857_600),
                    ("100 MB – 1 GB",    104_857_600, 1_073_741_824),
                    ("> 1 GB",           1_073_741_824, long.MaxValue),
                };

                return buckets.Select(b =>
                {
                    var grp = files.Where(f => f.SizeBytes >= b.MinBytes && f.SizeBytes < b.MaxBytes).ToList();
                    return new FileSizeGroupItem
                    {
                        Label     = b.Label,
                        Count     = grp.Count,
                        TotalSize = grp.Sum(f => f.SizeBytes),
                        Percent   = grand > 0 ? 100.0 * grp.Sum(f => f.SizeBytes) / grand : 0
                    };
                })
                .Where(b => b.Count > 0)
                .ToList<FileSizeGroupItem>();
            }, ct);

        // ═══════════════════════════════════════════════════════════════════════
        // Treemap chart data
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<IList<TreemapNode>> BuildTreemapAsync(string rootPath, int maxDepth = 2, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                if (!_cache.TryGetValue(rootPath, out var root)) return [];

                var palette = new[]
                {
                    "#0078D4","#107C10","#D83B01","#8E8CD8","#008B8B",
                    "#B146C2","#E87423","#007ACC","#497E2A","#7A2978",
                    "#005A9E","#00B294","#FF8C00","#6B69D6","#038387"
                };

                int colorIdx = 0;
                var children = BuildChildren(rootPath, root.SizeBytes, 0, maxDepth, ref colorIdx, palette, ct);
                return children;
            }, ct);

        private IList<TreemapNode> BuildChildren(string path, long parentSize, int depth, int maxDepth,
            ref int colorIdx, string[] palette, CancellationToken ct)
        {
            if (depth >= maxDepth) return [];

            var nodes = new List<TreemapNode>();
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!_cache.TryGetValue(sub, out var r)) continue;

                    string color = palette[colorIdx++ % palette.Length];
                    var node = new TreemapNode
                    {
                        Name      = System.IO.Path.GetFileName(sub),
                        SizeBytes = r.SizeBytes,
                        Percent   = parentSize > 0 ? 100.0 * r.SizeBytes / parentSize : 0,
                        Color     = color,
                        Depth     = depth,
                        Children  = [.. BuildChildren(sub, r.SizeBytes, depth + 1, maxDepth, ref colorIdx, palette, ct)]
                    };
                    nodes.Add(node);
                }
            }
            catch (UnauthorizedAccessException) { }

            return [.. nodes.OrderByDescending(n => n.SizeBytes)];
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Advanced search
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<IList<FileReportItem>> SearchFilesAsync(string rootPath, SearchFilter filter, CancellationToken ct = default)
            => await Task.Run(() =>
            {
                return EnumerateFiles(rootPath, ct)
                    .Where(f =>
                    {
                        if (!string.IsNullOrWhiteSpace(filter.NamePattern) &&
                            !f.Name.Contains(filter.NamePattern, StringComparison.OrdinalIgnoreCase)) return false;
                        if (filter.MinSizeBytes.HasValue && f.SizeBytes < filter.MinSizeBytes) return false;
                        if (filter.MaxSizeBytes.HasValue && f.SizeBytes > filter.MaxSizeBytes) return false;
                        if (filter.MinAgeDays.HasValue && f.AgeInDays < filter.MinAgeDays) return false;
                        if (filter.MaxAgeDays.HasValue && f.AgeInDays > filter.MaxAgeDays) return false;
                        if (!string.IsNullOrWhiteSpace(filter.Extension) &&
                            !f.Extension.Equals(filter.Extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) return false;
                        if (!string.IsNullOrWhiteSpace(filter.Owner) &&
                            !(f.Owner ?? "").Contains(filter.Owner, StringComparison.OrdinalIgnoreCase)) return false;
                        if (filter.IsReadOnly.HasValue && f.IsReadOnly != filter.IsReadOnly) return false;
                        if (filter.IsHidden.HasValue  && f.IsHidden  != filter.IsHidden)    return false;
                        if (filter.IsTemp.HasValue    && f.IsTemp    != filter.IsTemp)       return false;
                        return true;
                    })
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList<FileReportItem>();
            }, ct);

        // ═══════════════════════════════════════════════════════════════════════
        // Export
        // ═══════════════════════════════════════════════════════════════════════

        public async Task ExportReportAsync(string outputPath, ExportFormat format, IList<FileReportItem> items, CancellationToken ct = default)
        {
            string content = format switch
            {
                ExportFormat.Csv  => BuildFileCsv(items),
                ExportFormat.Xml  => BuildFileXml(items),
                ExportFormat.Html => BuildFileHtml(items),
                _                 => BuildFileCsv(items)
            };
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, ct);
        }

        public async Task ExportFolderSizesAsync(string outputPath, ExportFormat format, IList<FolderSizeResult> items, CancellationToken ct = default)
        {
            string content = format switch
            {
                ExportFormat.Csv  => BuildFolderCsv(items),
                ExportFormat.Xml  => BuildFolderXml(items),
                ExportFormat.Html => BuildFolderHtml(items),
                _                 => BuildFolderCsv(items)
            };
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, ct);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Control
        // ═══════════════════════════════════════════════════════════════════════

        public void Cancel() => _cts?.Cancel();
        public void Clear()  => _cache.Clear();

        // ═══════════════════════════════════════════════════════════════════════
        // Core recursive scan
        // ═══════════════════════════════════════════════════════════════════════

        private FolderSizeResult ScanDirectory(string path, string? parentPath, int depth, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            long totalSize = 0, totalAlloc = 0;
            int  fileCount = 0,  folderCount = 0;

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

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    folderCount++;
                    var child  = ScanDirectory(dir, path, depth + 1, ct);
                    totalSize  += child.SizeBytes;
                    totalAlloc += child.AllocatedBytes;
                    fileCount  += child.FileCount;
                    folderCount+= child.FolderCount;
                    _cache[dir] = child;
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

        // ═══════════════════════════════════════════════════════════════════════
        // File enumeration helper
        // ═══════════════════════════════════════════════════════════════════════

        private static IEnumerable<FileReportItem> EnumerateFiles(string rootPath, CancellationToken ct)
        {
            if (!Directory.Exists(rootPath)) yield break;

            IEnumerable<string> paths;
            try { paths = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories); }
            catch { yield break; }

            int rootDepth = rootPath.TrimEnd(System.IO.Path.DirectorySeparatorChar)
                                    .Split(System.IO.Path.DirectorySeparatorChar).Length;

            foreach (var file in paths)
            {
                ct.ThrowIfCancellationRequested();
                FileInfo? fi = null;
                try { fi = new FileInfo(file); } catch { continue; }

                string ext   = fi.Extension.ToLowerInvariant();
                var    attrs = fi.Attributes;
                int    depth = fi.FullName.Split(System.IO.Path.DirectorySeparatorChar).Length - rootDepth - 1;

                yield return new FileReportItem
                {
                    Name       = fi.Name,
                    Path       = fi.FullName,
                    FolderPath = fi.DirectoryName ?? "",
                    Extension  = ext,
                    SizeBytes  = fi.Length,
                    Modified   = fi.LastWriteTime,
                    Created    = fi.CreationTime,
                    Accessed   = fi.LastAccessTime,
                    IsTemp     = ext is ".tmp" or ".temp" or ".bak" or ".log" or ".dmp" or ".old",
                    IsReadOnly = attrs.HasFlag(FileAttributes.ReadOnly),
                    IsHidden   = attrs.HasFlag(FileAttributes.Hidden),
                    IsSystem   = attrs.HasFlag(FileAttributes.System),
                    Owner      = TryGetOwner(fi.FullName),
                    Depth      = Math.Max(0, depth)
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

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

        // ── CSV builders ──────────────────────────────────────────────────────

        private static string BuildFileCsv(IList<FileReportItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Path,Size,SizeBytes,Modified,Created,Category,Owner,AgeInDays,Attributes");
            foreach (var f in items)
                sb.AppendLine($"{CsvEsc(f.Name)},{CsvEsc(f.Path)},{f.SizeFormatted},{f.SizeBytes}," +
                              $"{f.ModifiedFormatted},{f.CreatedFormatted},{f.Category},{CsvEsc(f.Owner ?? "")}," +
                              $"{f.AgeInDays},{f.AttributeFlags}");
            return sb.ToString();
        }

        private static string BuildFolderCsv(IList<FolderSizeResult> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Path,Size,SizeBytes,AllocatedBytes,Files,Folders,PercentOfParent,Depth");
            foreach (var f in items)
                sb.AppendLine($"{CsvEsc(f.Name)},{CsvEsc(f.Path)},{f.SizeFormatted},{f.SizeBytes}," +
                              $"{f.AllocatedBytes},{f.FileCount},{f.FolderCount},{f.PercentFormatted},{f.Depth}");
            return sb.ToString();
        }

        // ── XML builders ──────────────────────────────────────────────────────

        private static string BuildFileXml(IList<FileReportItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<FileReport>");
            foreach (var f in items)
                sb.AppendLine($"  <File name=\"{XmlEsc(f.Name)}\" size=\"{f.SizeBytes}\" " +
                              $"modified=\"{f.ModifiedFormatted}\" category=\"{f.Category}\" " +
                              $"owner=\"{XmlEsc(f.Owner ?? "")}\" path=\"{XmlEsc(f.Path)}\"/>");
            sb.AppendLine("</FileReport>");
            return sb.ToString();
        }

        private static string BuildFolderXml(IList<FolderSizeResult> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<FolderSizeReport>");
            foreach (var f in items)
                sb.AppendLine($"  <Folder name=\"{XmlEsc(f.Name)}\" sizeBytes=\"{f.SizeBytes}\" " +
                              $"files=\"{f.FileCount}\" folders=\"{f.FolderCount}\" " +
                              $"depth=\"{f.Depth}\" path=\"{XmlEsc(f.Path)}\"/>");
            sb.AppendLine("</FolderSizeReport>");
            return sb.ToString();
        }

        // ── HTML builders ─────────────────────────────────────────────────────

        private static string BuildFileHtml(IList<FileReportItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<title>FolderDiag File Report</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;font-size:13px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:6px 10px}");
            sb.AppendLine("th{background:#0078d4;color:#fff}tr:nth-child(even){background:#f5f5f5}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h2>FolderDiag File Report — {DateTime.Now:yyyy-MM-dd HH:mm}</h2>");
            sb.AppendLine("<table><tr><th>Name</th><th>Size</th><th>Modified</th><th>Category</th><th>Owner</th><th>Folder</th></tr>");
            foreach (var f in items)
                sb.AppendLine($"<tr><td>{HtmlEsc(f.Name)}</td><td>{f.SizeFormatted}</td><td>{f.ModifiedFormatted}</td>" +
                              $"<td>{f.Category}</td><td>{HtmlEsc(f.Owner ?? "")}</td><td>{HtmlEsc(f.FolderPath)}</td></tr>");
            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        private static string BuildFolderHtml(IList<FolderSizeResult> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<title>FolderDiag Folder Size Report</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;font-size:13px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:6px 10px}");
            sb.AppendLine("th{background:#0078d4;color:#fff}tr:nth-child(even){background:#f5f5f5}");
            sb.AppendLine(".bar{background:#0078d4;height:12px;border-radius:3px;display:inline-block}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h2>FolderDiag Folder Sizes — {DateTime.Now:yyyy-MM-dd HH:mm}</h2>");
            sb.AppendLine("<table><tr><th>Name</th><th>Size</th><th>% of Parent</th><th>Files</th><th>Folders</th><th>Depth</th><th>Bar</th></tr>");
            foreach (var f in items)
                sb.AppendLine($"<tr><td>{HtmlEsc(f.Name)}</td><td>{f.SizeFormatted}</td><td>{f.PercentFormatted}</td>" +
                              $"<td>{f.FileCountFormatted}</td><td>{f.FolderCountFormatted}</td><td>{f.Depth}</td>" +
                              $"<td><span class='bar' style='width:{Math.Min(200, f.PercentOfParent * 2):F0}px'></span></td></tr>");
            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        // ── String escaping ───────────────────────────────────────────────────

        private static string CsvEsc(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private static string XmlEsc(string s) => s
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");

        private static string HtmlEsc(string s) => s
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
