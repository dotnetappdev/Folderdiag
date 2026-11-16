using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using FolderDiag.Core.Models;

namespace FolderDiag.Core.Scanning
{
    public sealed class ScanOptions
    {
        /// <summary>Max folders read at once. SSDs/NVMe benefit from 4–8; 1 = sequential.</summary>
        public int MaxParallelism { get; set; } = Math.Clamp(Environment.ProcessorCount, 2, 8);

        /// <summary>Folder levels below the root that are split across threads.</summary>
        public int ParallelDepth { get; set; } = 3;

        /// <summary>Optional owner lookup (path, isDirectory) → "DOMAIN\\user". Null = skip owners.</summary>
        public Func<string, bool, string?>? OwnerResolver { get; set; }

        /// <summary>Look up owners for files as well as folders (needed for "Files by Owner").</summary>
        public bool FileOwners { get; set; } = true;

        /// <summary>Cluster size used to estimate allocated size on disk.</summary>
        public long ClusterSize { get; set; } = 4096;
    }

    public readonly record struct ScanProgress(long Files, long Folders, long Bytes, string CurrentPath, TimeSpan Elapsed);

    public readonly record struct ScanTotals(long Size, long Allocated, int Files, int Folders);

    /// <summary>
    /// Fast recursive folder-size scanner.
    /// <list type="bullet">
    /// <item>One directory enumeration per folder (FindFirstFile data: size, attributes and
    ///       times come back with the listing — no extra FileInfo/stat call per file).</item>
    /// <item>Top levels are scanned in parallel; deeper levels run sequentially per worker.</item>
    /// <item>Inaccessible entries are skipped individually instead of aborting the folder.</item>
    /// <item>Junctions/symlinks are not followed (no double counting or loops), but
    ///       OneDrive/cloud placeholder folders are.</item>
    /// <item>Results are streamed to callbacks, so memory stays flat on huge drives.</item>
    /// </list>
    /// Callbacks are invoked from multiple threads and must be thread-safe.
    /// </summary>
    public static class FolderScanner
    {
        // Cloud-file attributes (OneDrive Files On-Demand etc.) not in the FileAttributes enum
        private const FileAttributes RecallOnOpen       = (FileAttributes)0x00040000;
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

        private static readonly EnumerationOptions EnumOpts = new()
        {
            IgnoreInaccessible       = true,
            RecurseSubdirectories    = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip         = 0,          // include hidden/system files in sizes
            BufferSize               = 64 * 1024,  // fewer kernel round-trips per folder
        };

        private static readonly HashSet<string> TempExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".tmp", ".temp", ".bak", ".log", ".dmp" };

        private readonly record struct Entry(
            string Name, string FullPath, bool IsDirectory, long Length, FileAttributes Attributes,
            DateTime Created, DateTime Modified, DateTime Accessed);

        public static ScanTotals Scan(
            string rootPath,
            ScanOptions options,
            Action<FolderItem> onFolder,
            Action<FileItem> onFile,
            IProgress<ScanProgress>? progress,
            CancellationToken ct)
        {
            var ctx = new Context(options, onFolder, onFile, progress, ct);
            string? parent = Path.GetDirectoryName(rootPath); // keeps the root linked to its parent in the DB
            DateTime? created = null, modified = null;
            try { var di = new DirectoryInfo(rootPath); created = di.CreationTime; modified = di.LastWriteTime; } catch { }
            var totals = ScanDirectory(ctx, rootPath, parent, 0, created, modified);
            ctx.Report(rootPath, force: true);
            return totals;
        }

        private sealed class Context
        {
            public readonly ScanOptions Options;
            public readonly Action<FolderItem> OnFolder;
            public readonly Action<FileItem> OnFile;
            public readonly IProgress<ScanProgress>? Progress;
            public readonly CancellationToken Ct;
            public readonly DateTime ScannedAt = DateTime.Now;
            public readonly Stopwatch Clock = Stopwatch.StartNew();
            public long Files, Folders, Bytes, LastReportMs;

            public Context(ScanOptions o, Action<FolderItem> f, Action<FileItem> fi, IProgress<ScanProgress>? p, CancellationToken ct)
            { Options = o; OnFolder = f; OnFile = fi; Progress = p; Ct = ct; }

            /// <summary>Throttled to ~5 updates/second so the UI isn't flooded.</summary>
            public void Report(string current, bool force = false)
            {
                if (Progress == null) return;
                long now = Clock.ElapsedMilliseconds, last = Interlocked.Read(ref LastReportMs);
                if (!force && now - last < 200) return;
                if (!force && Interlocked.CompareExchange(ref LastReportMs, now, last) != last) return;
                Progress.Report(new ScanProgress(Interlocked.Read(ref Files), Interlocked.Read(ref Folders),
                                                 Interlocked.Read(ref Bytes), current, Clock.Elapsed));
            }
        }

        private static ScanTotals ScanDirectory(Context ctx, string path, string? parentPath, int depth,
                                                DateTime? created, DateTime? modified)
        {
            ctx.Ct.ThrowIfCancellationRequested();

            long size = 0, alloc = 0;
            int files = 0, folders = 0;
            List<Entry>? subdirs = null; // dates come from the parent listing (no extra stat per folder)
            var opts = ctx.Options;

            try
            {
                var listing = new FileSystemEnumerable<Entry>(path,
                    (ref FileSystemEntry e) => new Entry(
                        e.FileName.ToString(), e.ToFullPath(), e.IsDirectory, e.Length, e.Attributes,
                        e.CreationTimeUtc.LocalDateTime, e.LastWriteTimeUtc.LocalDateTime, e.LastAccessTimeUtc.LocalDateTime),
                    EnumOpts);

                foreach (var e in listing)
                {
                    if (e.IsDirectory)
                    {
                        if ((e.Attributes & FileAttributes.ReparsePoint) != 0 && IsLink(e.FullPath)) continue;
                        (subdirs ??= new()).Add(e);
                        continue;
                    }

                    // Cloud-only placeholders take no space on this disk
                    bool cloudOnly = (e.Attributes & (FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0;
                    long fileAlloc = cloudOnly ? 0 : RoundUp(e.Length, opts.ClusterSize);
                    size += e.Length; alloc += fileAlloc; files++;

                    string ext = Path.GetExtension(e.Name).ToLowerInvariant();
                    ctx.OnFile(new FileItem
                    {
                        Name = e.Name, Path = e.FullPath, FolderPath = path, Extension = ext,
                        SizeBytes = e.Length, AllocatedBytes = fileAlloc,
                        Modified = e.Modified, Created = e.Created, Accessed = e.Accessed,
                        Owner = opts.FileOwners ? opts.OwnerResolver?.Invoke(e.FullPath, false) : null,
                        IsReadOnly = (e.Attributes & FileAttributes.ReadOnly) != 0,
                        IsHidden   = (e.Attributes & FileAttributes.Hidden)   != 0,
                        IsSystem   = (e.Attributes & FileAttributes.System)   != 0,
                        IsTemp     = TempExtensions.Contains(ext),
                        ScannedAt  = ctx.ScannedAt,
                    });
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                // Folder itself unreadable (e.g. access denied): record it with what we have
            }

            Interlocked.Add(ref ctx.Files, files);
            Interlocked.Add(ref ctx.Bytes, size);

            if (subdirs != null)
            {
                folders += subdirs.Count;
                if (depth < opts.ParallelDepth && subdirs.Count > 1 && opts.MaxParallelism > 1)
                {
                    long pSize = 0, pAlloc = 0; int pFiles = 0, pFolders = 0;
                    Parallel.ForEach(subdirs,
                        new ParallelOptions { MaxDegreeOfParallelism = opts.MaxParallelism, CancellationToken = ctx.Ct },
                        dir =>
                        {
                            var t = ScanDirectory(ctx, dir.FullPath, path, depth + 1, dir.Created, dir.Modified);
                            Interlocked.Add(ref pSize, t.Size);
                            Interlocked.Add(ref pAlloc, t.Allocated);
                            Interlocked.Add(ref pFiles, t.Files);
                            Interlocked.Add(ref pFolders, t.Folders);
                        });
                    size += pSize; alloc += pAlloc; files += pFiles; folders += pFolders;
                }
                else
                {
                    foreach (var dir in subdirs)
                    {
                        var t = ScanDirectory(ctx, dir.FullPath, path, depth + 1, dir.Created, dir.Modified);
                        size += t.Size; alloc += t.Allocated; files += t.Files; folders += t.Folders;
                    }
                }
            }

            Interlocked.Increment(ref ctx.Folders);

            ctx.OnFolder(new FolderItem
            {
                Path = path,
                Name = depth == 0 ? path : Path.GetFileName(path),
                ParentPath = parentPath,
                SizeBytes = size, AllocatedBytes = alloc,
                FileCount = files, FolderCount = folders,
                LastModified = modified, Created = created,
                Owner = opts.OwnerResolver?.Invoke(path, true),
                Depth = depth,
                ScannedAt = ctx.ScannedAt,
            });

            ctx.Report(path);
            return new ScanTotals(size, alloc, files, folders);
        }

        /// <summary>
        /// True for junctions and symbolic links (which we don't follow); false for other
        /// reparse points such as OneDrive cloud folders, whose contents are real.
        /// </summary>
        private static bool IsLink(string dir)
        {
            try { return new DirectoryInfo(dir).LinkTarget != null; }
            catch { return true; } // can't tell: be safe and don't follow
        }

        private static long RoundUp(long bytes, long cluster)
            => cluster <= 1 ? bytes : (bytes + cluster - 1) / cluster * cluster;
    }
}
