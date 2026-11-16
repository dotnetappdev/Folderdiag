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

        /// <summary>File categories and their extensions (lower case, with the dot as stored).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> CategoryExtensions = new Dictionary<string, string[]>
        {
            ["Images"]      = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".svg", ".ico" },
            ["Video"]       = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" },
            ["Audio"]       = new[] { ".mp3", ".wav", ".flac", ".aac", ".wma", ".ogg", ".m4a" },
            ["Documents"]   = new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".odt", ".ods" },
            ["Archives"]    = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz" },
            ["Executables"] = new[] { ".exe", ".msi", ".dll", ".sys", ".drv", ".ocx" },
            ["Source Code"] = new[] { ".cs", ".vb", ".cpp", ".c", ".h", ".py", ".js", ".ts", ".java", ".rs", ".go" },
            ["Temporary"]   = new[] { ".tmp", ".temp", ".bak", ".old", ".log", ".dmp" },
            ["Config"]      = new[] { ".xml", ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf" },
            ["Text"]        = new[] { ".txt", ".md", ".rtf", ".csv", ".tsv" },
        };

        private static readonly Dictionary<string, string> CategoryByExtension =
            CategoryExtensions.SelectMany(kv => kv.Value.Select(e => (e, kv.Key)))
                              .ToDictionary(x => x.e, x => x.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>All extensions that belong to a named category (used to filter in SQL).</summary>
        public static readonly string[] AllCategorisedExtensions = CategoryByExtension.Keys.ToArray();

        public static string GetCategory(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "Other";
            string e = ext.StartsWith('.') ? ext : "." + ext;
            return CategoryByExtension.TryGetValue(e, out var cat) ? cat : "Other";
        }
    }
}
