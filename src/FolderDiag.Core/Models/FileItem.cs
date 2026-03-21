using System;

namespace FolderDiag.Core.Models
{
    public class FileItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public long AllocatedBytes { get; set; }
        public DateTime? Modified { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Accessed { get; set; }
        public string? Owner { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSystem { get; set; }
        public bool IsTemp { get; set; }
        public string? FileHash { get; set; }
        public string? ScanGroup { get; set; }
        public DateTime ScannedAt { get; set; }

        // Computed
        public string SizeFormatted => FolderItem.FormatBytes(SizeBytes);
        public string Category => GetCategory(Extension);
        public int AgeInDays => Modified.HasValue ? (int)(DateTime.Now - Modified.Value).TotalDays : 0;

        public static string GetCategory(string ext)
        {
            var e = ext.ToLowerInvariant().TrimStart('.');
            return e switch
            {
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "tiff" or "webp" or "svg" or "ico" => "Images",
                "mp4" or "avi" or "mkv" or "mov" or "wmv" or "flv" or "webm" or "m4v" => "Video",
                "mp3" or "wav" or "flac" or "aac" or "wma" or "ogg" or "m4a" => "Audio",
                "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "pdf" or "odt" or "ods" => "Documents",
                "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" => "Archives",
                "exe" or "msi" or "dll" or "sys" or "drv" or "ocx" => "Executables",
                "cs" or "vb" or "cpp" or "c" or "h" or "py" or "js" or "ts" or "java" or "rs" or "go" => "Source Code",
                "tmp" or "temp" or "bak" or "old" or "log" or "dmp" => "Temporary",
                "xml" or "json" or "yaml" or "yml" or "toml" or "ini" or "cfg" or "conf" => "Config",
                "txt" or "md" or "rtf" or "csv" or "tsv" => "Text",
                _ => "Other"
            };
        }
    }
}
