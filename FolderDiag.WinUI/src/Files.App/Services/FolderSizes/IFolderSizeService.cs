// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

namespace Files.App.Services.FolderSizes
{
    /// <summary>
    /// Full FolderSizes-compatible analysis service — mirrors every report type
    /// available in foldersizes.com: folder sizes, file reports, classification,
    /// distribution analysis, search, trend tracking, and disk space.
    /// </summary>
    public interface IFolderSizeService
    {
        // ── Progress ──────────────────────────────────────────────────────────
        event EventHandler<FolderSizeProgressEventArgs> ProgressChanged;

        // ── Core scan ─────────────────────────────────────────────────────────
        Task<FolderSizeResult> AnalyzeAsync(string rootPath, CancellationToken ct = default);

        // ── File Reports (FolderSizes "File Instance Reports") ────────────────
        Task<IList<FileReportItem>> GetLargestFilesAsync(string rootPath,  int topN = 50, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetOldestFilesAsync(string rootPath,   int topN = 50, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetNewestFilesAsync(string rootPath,   int topN = 50, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetTempFilesAsync(string rootPath,     CancellationToken ct = default);
        Task<IList<FileReportItem>> GetDuplicateFilesAsync(string rootPath, CancellationToken ct = default);

        // ── Classification Reports (FolderSizes "File Classification Reports") ─
        Task<IList<TypeClassificationItem>> GetClassificationByTypeAsync(string rootPath, CancellationToken ct = default);
        Task<IList<FileOwnerItem>>          GetFilesByOwnerAsync(string rootPath,         CancellationToken ct = default);
        Task<IList<FileAttributeItem>>      GetFileAttributesReportAsync(string rootPath, CancellationToken ct = default);

        // ── Distribution Reports ──────────────────────────────────────────────
        Task<IList<FileAgeGroupItem>>   GetFileAgeDistributionAsync(string rootPath,  CancellationToken ct = default);
        Task<IList<FileDepthItem>>      GetFileDepthReportAsync(string rootPath,      CancellationToken ct = default);
        Task<IList<FileSizeGroupItem>>  GetFileSizeDistributionAsync(string rootPath, CancellationToken ct = default);

        // ── Chart data (Treemap / Sunburst) ───────────────────────────────────
        Task<IList<TreemapNode>> BuildTreemapAsync(string rootPath, int maxDepth = 2, CancellationToken ct = default);

        // ── Advanced Search ───────────────────────────────────────────────────
        Task<IList<FileReportItem>> SearchFilesAsync(string rootPath, SearchFilter filter, CancellationToken ct = default);

        // ── Export ────────────────────────────────────────────────────────────
        Task ExportReportAsync(string outputPath, ExportFormat format, IList<FileReportItem> items, CancellationToken ct = default);
        Task ExportFolderSizesAsync(string outputPath, ExportFormat format, IList<FolderSizeResult> items, CancellationToken ct = default);

        // ── Control ───────────────────────────────────────────────────────────
        void Cancel();
        void Clear();
    }

    // ── Supporting types ──────────────────────────────────────────────────────

    public sealed class FolderSizeProgressEventArgs : EventArgs
    {
        public string CurrentPath    { get; init; } = "";
        public long   TotalSizeBytes { get; init; }
        public int    FileCount      { get; init; }
        public int    FolderCount    { get; init; }
    }

    public enum ExportFormat { Csv, Xml, Html }
}
