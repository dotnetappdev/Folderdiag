using System;

namespace FolderDiag.Core.Models
{
    public class FolderItem
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime ScannedAt { get; set; }
    }
}
