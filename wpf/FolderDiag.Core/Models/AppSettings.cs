namespace FolderDiag.Core.Models;

public sealed class AppSettings
{
    public bool ShowHiddenFiles { get; set; } = false;
    public bool ShowSystemFiles { get; set; } = false;
    public bool FollowSymlinks { get; set; } = false;
    public bool ShowFileExtensions { get; set; } = true;
    public bool ShowSizeBars { get; set; } = true;
    public bool ShowPercentage { get; set; } = true;
    public bool AutoRefresh { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
    public SortColumn DefaultSortColumn { get; set; } = SortColumn.TotalSize;
    public bool DefaultSortDescending { get; set; } = true;
    public ViewMode DefaultViewMode { get; set; } = ViewMode.Details;
    public bool DarkMode { get; set; } = true;
    public double TreePaneWidth { get; set; } = 280;
    public double DetailPaneWidth { get; set; } = 340;
    public bool ShowDetailPane { get; set; } = true;
    public int MaxScanDepth { get; set; } = -1; // -1 = unlimited
    public string LastScannedPath { get; set; } = string.Empty;
    public List<string> RecentPaths { get; set; } = [];
    public int MaxRecentPaths { get; set; } = 20;
    public bool IndexForSearch { get; set; } = true;
    public SizeUnit SizeDisplayUnit { get; set; } = SizeUnit.Auto;
    public bool ShowGitignored { get; set; } = true;
}

public enum SortColumn
{
    Name,
    TotalSize,
    OwnSize,
    FileCount,
    FolderCount,
    LastModified,
    Created,
    Percent,
    Type
}

public enum ViewMode
{
    Details,
    LargeIcons,
    SmallIcons,
    List,
    Tiles,
    Treemap,
    BarChart
}

public enum SizeUnit
{
    Auto,
    Bytes,
    KB,
    MB,
    GB
}
