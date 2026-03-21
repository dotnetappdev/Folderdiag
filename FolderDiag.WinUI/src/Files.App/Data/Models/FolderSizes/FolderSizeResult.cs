// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

namespace Files.App.Services.FolderSizes
{
    // ─────────────────────────────────────────────────────────────────────────
    // Core results
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Recursive folder size analysis result for one directory.</summary>
    public sealed class FolderSizeResult
    {
        public string  Path            { get; set; } = "";
        public string  Name            { get; set; } = "";
        public string? ParentPath      { get; set; }
        public long    SizeBytes       { get; set; }
        public long    AllocatedBytes  { get; set; }
        public int     FileCount       { get; set; }
        public int     FolderCount     { get; set; }
        public int     Depth           { get; set; }
        public double  PercentOfParent { get; set; }
        public DateTime ScannedAt      { get; set; }

        public string SizeFormatted        => FormatBytes(SizeBytes);
        public string AllocatedFormatted   => FormatBytes(AllocatedBytes);
        public string FileCountFormatted   => FileCount.ToString("N0");
        public string FolderCountFormatted => FolderCount.ToString("N0");
        public string PercentFormatted     => $"{PercentOfParent:F1}%";

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1_099_511_627_776L) return $"{bytes / 1_099_511_627_776.0:F2} TB";
            if (bytes >= 1_073_741_824L)     return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576L)         return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024L)             return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // File report items
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Individual file entry for file reports (Largest, Oldest, Temp, Duplicates, etc.).</summary>
    public sealed class FileReportItem
    {
        public string    Name       { get; set; } = "";
        public string    Path       { get; set; } = "";
        public string    FolderPath { get; set; } = "";
        public string    Extension  { get; set; } = "";
        public long      SizeBytes  { get; set; }
        public DateTime? Modified   { get; set; }
        public DateTime? Created    { get; set; }
        public DateTime? Accessed   { get; set; }
        public bool      IsTemp     { get; set; }
        public bool      IsReadOnly { get; set; }
        public bool      IsHidden   { get; set; }
        public bool      IsSystem   { get; set; }
        public string?   Owner      { get; set; }
        public int       Depth      { get; set; }

        public string SizeFormatted    => FolderSizeResult.FormatBytes(SizeBytes);
        public string Category         => GetCategory(Extension);
        public string CategoryIcon     => GetCategoryIcon(Category);
        public int    AgeInDays        => Modified.HasValue ? (int)(DateTime.Now - Modified.Value).TotalDays : 0;
        public string AgeFormatted     => FormatAge(AgeInDays);
        public string ModifiedFormatted => Modified?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        public string CreatedFormatted  => Created?.ToString("yyyy-MM-dd") ?? "—";
        public string AttributeFlags   => $"{(IsReadOnly ? "R" : "-")}{(IsHidden ? "H" : "-")}{(IsSystem ? "S" : "-")}";

        public static string FormatAge(int days) => days switch
        {
            0       => "Today",
            1       => "Yesterday",
            <= 7    => $"{days}d ago",
            <= 30   => $"{days / 7}w ago",
            <= 365  => $"{days / 30}mo ago",
            _       => $"{days / 365}yr ago"
        };

        public static string GetCategory(string ext) => ext.ToLowerInvariant().TrimStart('.') switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "tiff" or "webp" or "svg" or "ico" or "heic" or "raw" => "Images",
            "mp4" or "avi" or "mkv" or "mov" or "wmv" or "flv" or "webm" or "m4v" or "mpg" or "mpeg"           => "Video",
            "mp3" or "wav" or "flac" or "aac" or "wma" or "ogg" or "m4a" or "opus"                              => "Audio",
            "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "pdf" or "odt" or "ods" or "rtf"          => "Documents",
            "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "cab" or "iso"                         => "Archives",
            "exe" or "msi" or "dll" or "sys" or "drv" or "ocx" or "com"                                         => "Executables",
            "cs" or "vb" or "cpp" or "c" or "h" or "py" or "js" or "ts" or "java" or "rs" or "go"              => "Source Code",
            "tmp" or "temp" or "bak" or "old" or "log" or "dmp" or "cache"                                      => "Temporary",
            "xml" or "json" or "yaml" or "yml" or "toml" or "ini" or "cfg" or "conf" or "reg"                   => "Config / Data",
            "txt" or "md" or "rst" or "csv" or "tsv" or "nfo"                                                   => "Text",
            _                                                                                                    => "Other"
        };

        public static string GetCategoryIcon(string category) => category switch
        {
            "Images"        => "\uE8B9",
            "Video"         => "\uE714",
            "Audio"         => "\uE8D6",
            "Documents"     => "\uE8A5",
            "Archives"      => "\uE7B8",
            "Executables"   => "\uE756",
            "Source Code"   => "\uE943",
            "Temporary"     => "\uE74D",
            "Config / Data" => "\uE9F5",
            "Text"          => "\uE8A4",
            _               => "\uE8B7"
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Classification / grouping items
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>File type classification grouping.</summary>
    public sealed class TypeClassificationItem
    {
        public string Category  { get; set; } = "";
        public int    Count     { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
        public string CategoryIcon       => FileReportItem.GetCategoryIcon(Category);
    }

    /// <summary>File age distribution bucket (e.g. "< 1 week", "1–4 weeks", ...).</summary>
    public sealed class FileAgeGroupItem
    {
        public string Label     { get; set; } = "";
        public int    Count     { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
    }

    /// <summary>File depth distribution — how many files/bytes exist at each directory depth.</summary>
    public sealed class FileDepthItem
    {
        public int    Depth     { get; set; }
        public int    FileCount { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string Label              => $"Depth {Depth}";
        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
    }

    /// <summary>File size distribution bucket (e.g. "< 1 KB", "1 KB–100 KB", ...).</summary>
    public sealed class FileSizeGroupItem
    {
        public string Label     { get; set; } = "";
        public int    Count     { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
        public string CountFormatted     => Count.ToString("N0");
    }

    /// <summary>Per-owner file grouping for File Owners report.</summary>
    public sealed class FileOwnerItem
    {
        public string Owner     { get; set; } = "Unknown";
        public int    Count     { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
    }

    /// <summary>File attributes grouping (ReadOnly / Hidden / System / Normal).</summary>
    public sealed class FileAttributeItem
    {
        public string AttributeLabel { get; set; } = "";
        public int    Count          { get; set; }
        public long   TotalSize      { get; set; }
        public double Percent        { get; set; }
        public string Glyph          { get; set; } = "\uE8B7";

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Treemap / Sunburst chart nodes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Node in a treemap/sunburst hierarchy.</summary>
    public sealed class TreemapNode
    {
        public string  Name      { get; set; } = "";
        public long    SizeBytes { get; set; }
        public double  Percent   { get; set; }
        public string  Color     { get; set; } = "#0078D4";
        public int     Depth     { get; set; }
        public List<TreemapNode> Children { get; set; } = [];

        public string SizeFormatted    => FolderSizeResult.FormatBytes(SizeBytes);
        public string PercentFormatted => $"{Percent:F1}%";
        // Width multiplier 0–1 used by XAML GridUnitType.Star bindings via converter
        public double WidthFraction    => Percent / 100.0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trend / snapshot comparison
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>A single point on the trend timeline.</summary>
    public sealed class TrendPoint
    {
        public string   Label     { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public long     SizeBytes { get; set; }
        public int      FileCount { get; set; }
        public double   Percent   { get; set; }  // % of max in series

        public string SizeFormatted => FolderSizeResult.FormatBytes(SizeBytes);
        public string DateFormatted => Timestamp.ToString("MM/dd HH:mm");
    }

    /// <summary>Result of comparing two snapshots side by side.</summary>
    public sealed class SnapshotDiffItem
    {
        public string Name        { get; set; } = "";
        public long   SizeBefore  { get; set; }
        public long   SizeAfter   { get; set; }
        public long   Delta       => SizeAfter - SizeBefore;
        public string DeltaSign   => Delta >= 0 ? "+" : "";
        public string DeltaLabel  => $"{DeltaSign}{FolderSizeResult.FormatBytes(Math.Abs(Delta))}";
        public string BeforeLabel => FolderSizeResult.FormatBytes(SizeBefore);
        public string AfterLabel  => FolderSizeResult.FormatBytes(SizeAfter);
        public bool   IsGrowth    => Delta > 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Search
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parameters for advanced file system search.</summary>
    public sealed class SearchFilter
    {
        public string  NamePattern    { get; set; } = "";   // wildcard/substring
        public long?   MinSizeBytes   { get; set; }
        public long?   MaxSizeBytes   { get; set; }
        public int?    MinAgeDays     { get; set; }
        public int?    MaxAgeDays     { get; set; }
        public string? Extension      { get; set; }
        public string? Owner          { get; set; }
        public bool?   IsReadOnly     { get; set; }
        public bool?   IsHidden       { get; set; }
        public bool?   IsTemp         { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drive / disk space
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Per-drive disk space snapshot shown in Disk Space panel.</summary>
    public sealed class DriveSpaceItem
    {
        public string DriveName   { get; set; } = "";
        public string Label       { get; set; } = "";
        public string DriveType   { get; set; } = "";
        public string FileSystem  { get; set; } = "";
        public long   TotalBytes  { get; set; }
        public long   FreeBytes   { get; set; }
        public long   UsedBytes   => TotalBytes - FreeBytes;
        public double UsedPercent => TotalBytes > 0 ? 100.0 * UsedBytes / TotalBytes : 0;

        public string TotalFormatted      => FolderSizeResult.FormatBytes(TotalBytes);
        public string FreeFormatted       => FolderSizeResult.FormatBytes(FreeBytes);
        public string UsedFormatted       => FolderSizeResult.FormatBytes(UsedBytes);
        public string UsedPercentFormatted => $"{UsedPercent:F1}%";
        // Colour coding: yellow at 70%, red at 85%
        public string BarColor            => UsedPercent >= 85 ? "#C42B1C" : UsedPercent >= 70 ? "#CA5010" : "#0078D4";

        public static IList<DriveSpaceItem> GetAll()
        {
            var list = new List<DriveSpaceItem>();
            foreach (var d in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                list.Add(new DriveSpaceItem
                {
                    DriveName  = d.Name,
                    Label      = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                    DriveType  = d.DriveType.ToString(),
                    FileSystem = d.DriveFormat,
                    TotalBytes = d.TotalSize,
                    FreeBytes  = d.AvailableFreeSpace
                });
            }
            return list;
        }
    }
}
