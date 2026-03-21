// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

namespace Files.App.Services.FolderSizes
{
    /// <summary>Recursive folder size analysis result for one directory.</summary>
    public sealed class FolderSizeResult
    {
        public string  Path           { get; set; } = "";
        public string  Name           { get; set; } = "";
        public string? ParentPath     { get; set; }
        public long    SizeBytes      { get; set; }
        public long    AllocatedBytes { get; set; }
        public int     FileCount      { get; set; }
        public int     FolderCount    { get; set; }
        public int     Depth          { get; set; }
        public double  PercentOfParent { get; set; }
        public DateTime ScannedAt     { get; set; }

        // Formatted helpers
        public string SizeFormatted       => FormatBytes(SizeBytes);
        public string AllocatedFormatted  => FormatBytes(AllocatedBytes);
        public string FileCountFormatted  => FileCount.ToString("N0");
        public string FolderCountFormatted => FolderCount.ToString("N0");
        public string PercentFormatted    => $"{PercentOfParent:F1}%";

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1_099_511_627_776L) return $"{bytes / 1_099_511_627_776.0:F2} TB";
            if (bytes >= 1_073_741_824L)     return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576L)         return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024L)             return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    /// <summary>Individual file entry for file reports (Largest, Oldest, Temp, Duplicates).</summary>
    public sealed class FileReportItem
    {
        public string   Name       { get; set; } = "";
        public string   Path       { get; set; } = "";
        public string   FolderPath { get; set; } = "";
        public string   Extension  { get; set; } = "";
        public long     SizeBytes  { get; set; }
        public DateTime? Modified  { get; set; }
        public DateTime? Created   { get; set; }
        public bool     IsTemp     { get; set; }
        public string?  Owner      { get; set; }

        public string SizeFormatted => FolderSizeResult.FormatBytes(SizeBytes);
        public string Category      => GetCategory(Extension);
        public int    AgeInDays     => Modified.HasValue ? (int)(DateTime.Now - Modified.Value).TotalDays : 0;

        public static string GetCategory(string ext) => ext.ToLowerInvariant().TrimStart('.') switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "tiff" or "webp" or "svg" or "ico" or "heic" or "raw" => "Images",
            "mp4" or "avi" or "mkv" or "mov" or "wmv" or "flv" or "webm" or "m4v" or "mpg" or "mpeg" => "Video",
            "mp3" or "wav" or "flac" or "aac" or "wma" or "ogg" or "m4a" or "opus" => "Audio",
            "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "pdf" or "odt" or "ods" or "rtf" => "Documents",
            "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "cab" or "iso" => "Archives",
            "exe" or "msi" or "dll" or "sys" or "drv" or "ocx" or "com" => "Executables",
            "cs" or "vb" or "cpp" or "c" or "h" or "py" or "js" or "ts" or "java" or "rs" or "go" or "swift" or "kt" or "rb" => "Source Code",
            "tmp" or "temp" or "bak" or "old" or "log" or "dmp" or "cache" => "Temporary",
            "xml" or "json" or "yaml" or "yml" or "toml" or "ini" or "cfg" or "conf" or "reg" => "Config / Data",
            "txt" or "md" or "rst" or "csv" or "tsv" or "nfo" => "Text",
            _ => "Other"
        };
    }

    /// <summary>File type classification grouping.</summary>
    public sealed class TypeClassificationItem
    {
        public string Category  { get; set; } = "";
        public int    Count     { get; set; }
        public long   TotalSize { get; set; }
        public double Percent   { get; set; }

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string PercentFormatted   => $"{Percent:F1}%";
        public string CategoryIcon => Category switch
        {
            "Images"        => "\uE8B9", // Photo
            "Video"         => "\uE714", // Video
            "Audio"         => "\uE8D6", // Music
            "Documents"     => "\uE8A5", // Document
            "Archives"      => "\uE7B8", // ZipFolder
            "Executables"   => "\uE756", // Settings
            "Source Code"   => "\uE943", // Code
            "Temporary"     => "\uE74D", // Delete
            "Config / Data" => "\uE9F5", // DataFlow
            "Text"          => "\uE8A4", // Page
            _               => "\uE8B7"  // OpenFile
        };
    }

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

        public string TotalFormatted => FolderSizeResult.FormatBytes(TotalBytes);
        public string FreeFormatted  => FolderSizeResult.FormatBytes(FreeBytes);
        public string UsedFormatted  => FolderSizeResult.FormatBytes(UsedBytes);
        public string UsedPercentFormatted => $"{UsedPercent:F1}%";

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
