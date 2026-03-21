using System;

namespace FolderDiag.Core.Models
{
    public class FolderItem
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentPath { get; set; }
        public long SizeBytes { get; set; }
        public long AllocatedBytes { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public DateTime ScannedAt { get; set; }
        public DateTime? LastModified { get; set; }
        public DateTime? Created { get; set; }
        public string? Owner { get; set; }
        public double PercentOfParent { get; set; }
        public int Depth { get; set; }
        public string? ScanGroup { get; set; }

        // Computed (not stored)
        public string SizeFormatted => FormatBytes(SizeBytes);
        public string AllocatedFormatted => FormatBytes(AllocatedBytes);
        public string FileCountFormatted => FileCount.ToString("N0");
        public string FolderCountFormatted => FolderCount.ToString("N0");
        public string PercentFormatted => $"{PercentOfParent:F1}%";

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1_099_511_627_776L) return $"{bytes / 1_099_511_627_776.0:F2} TB";
            if (bytes >= 1_073_741_824L)     return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576L)         return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024L)             return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
