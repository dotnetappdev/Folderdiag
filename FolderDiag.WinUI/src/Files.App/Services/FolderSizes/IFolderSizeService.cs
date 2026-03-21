// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

namespace Files.App.Services.FolderSizes
{
    /// <summary>
    /// Provides recursive folder size analysis with file/subfolder counts.
    /// </summary>
    public interface IFolderSizeService
    {
        event EventHandler<FolderSizeProgressEventArgs> ProgressChanged;

        Task<FolderSizeResult> AnalyzeAsync(string rootPath, CancellationToken cancellationToken = default);
        Task<IList<FileReportItem>> GetLargestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetOldestFilesAsync(string rootPath, int topN = 50, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetTempFilesAsync(string rootPath, CancellationToken ct = default);
        Task<IList<FileReportItem>> GetDuplicateFilesAsync(string rootPath, CancellationToken ct = default);
        Task<IList<TypeClassificationItem>> GetClassificationByTypeAsync(string rootPath, CancellationToken ct = default);
        void Cancel();
        void Clear();
    }

    public sealed class FolderSizeProgressEventArgs : EventArgs
    {
        public string CurrentPath { get; init; } = "";
        public long TotalSizeBytes { get; init; }
        public int FileCount { get; init; }
        public int FolderCount { get; init; }
    }
}
