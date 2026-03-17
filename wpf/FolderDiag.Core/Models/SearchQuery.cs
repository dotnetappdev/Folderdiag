namespace FolderDiag.Core.Models;

public sealed class SearchQuery
{
    public string Text { get; set; } = string.Empty;
    public string SearchPath { get; set; } = string.Empty;
    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;
    public bool MatchCase { get; set; } = false;
    public bool UseRegex { get; set; } = false;
    public bool SearchContents { get; set; } = false;

    // Filters
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public DateTime? ModifiedAfter { get; set; }
    public DateTime? ModifiedBefore { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? FileType { get; set; }
    public string[]? Extensions { get; set; }

    public int MaxResults { get; set; } = 1000;
}

public sealed class SearchResult
{
    public List<FileSystemEntry> Entries { get; set; } = [];
    public int TotalMatches { get; set; }
    public TimeSpan SearchDuration { get; set; }
    public bool IsTruncated { get; set; }
}
