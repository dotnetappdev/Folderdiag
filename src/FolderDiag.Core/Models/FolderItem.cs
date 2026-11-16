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
        // View-only flags (not stored): the Folder Sizes view lists files and not-yet-scanned
        // folders alongside scanned folders, like File Explorer.
        public bool IsFile    { get; set; }
        public bool IsScanned { get; set; } = true;
        /// <summary>The ".." row that goes up to the parent folder.</summary>
        public bool IsParentLink { get; set; }

        public string SizeFormatted => IsParentLink ? "" : IsScanned ? FormatBytes(SizeBytes) : "Not scanned";
        public string AllocatedFormatted => IsScanned && !IsParentLink ? FormatBytes(AllocatedBytes) : "";
        public string FileCountFormatted => IsFile || !IsScanned || IsParentLink ? "" : FileCount.ToString("N0");
        public string FolderCountFormatted => IsFile || !IsScanned || IsParentLink ? "" : FolderCount.ToString("N0");
        public string PercentFormatted => IsParentLink ? "" : $"{PercentOfParent:F1}%";

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
