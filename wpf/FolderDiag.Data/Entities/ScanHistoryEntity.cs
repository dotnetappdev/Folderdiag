using System.ComponentModel.DataAnnotations;

namespace FolderDiag.Data.Entities;

public sealed class ScanHistoryEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(1000)]
    public string RootPath { get; set; } = string.Empty;

    public DateTime ScanTime { get; set; }
    public long TotalSize { get; set; }
    public int TotalFiles { get; set; }
    public int TotalFolders { get; set; }
    public double ScanDurationSeconds { get; set; }
    public bool IsComplete { get; set; }
    public string? ErrorMessage { get; set; }
}
