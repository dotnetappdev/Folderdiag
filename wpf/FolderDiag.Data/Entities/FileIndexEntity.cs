using System.ComponentModel.DataAnnotations;

namespace FolderDiag.Data.Entities;

/// <summary>Indexed file entries for fast search.</summary>
public sealed class FileIndexEntity
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(1000)]
    public string FullPath { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Extension { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public long TotalSize { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    public int ScanHistoryId { get; set; }
    public ScanHistoryEntity? ScanHistory { get; set; }
}
