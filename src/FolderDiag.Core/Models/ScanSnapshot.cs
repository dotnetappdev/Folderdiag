using System;

namespace FolderDiag.Core.Models
{
    public class ScanSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long TotalSizeBytes { get; set; }
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
        public string? Notes { get; set; }

        public string TotalSizeFormatted => FolderItem.FormatBytes(TotalSizeBytes);
        public string CreatedFormatted => CreatedAt.ToString("yyyy-MM-dd HH:mm");
    }
}
