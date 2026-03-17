using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.ViewModels;

public sealed partial class SearchViewModel : ObservableObject
{
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _includeFiles = true;
    [ObservableProperty] private bool _includeFolders = true;
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _showAdvancedFilters;

    // Size filters
    [ObservableProperty] private long? _minSizeBytes;
    [ObservableProperty] private long? _maxSizeBytes;
    [ObservableProperty] private string _minSizeText = string.Empty;
    [ObservableProperty] private string _maxSizeText = string.Empty;
    [ObservableProperty] private string _selectedSizeFilter = "Any";

    // Date filters
    [ObservableProperty] private DateTime? _modifiedAfter;
    [ObservableProperty] private DateTime? _modifiedBefore;
    [ObservableProperty] private string _selectedDateFilter = "Any";

    // Type filter
    [ObservableProperty] private string _selectedTypeFilter = "All";
    [ObservableProperty] private string _extensionFilter = string.Empty;

    public ObservableCollection<string> SizeFilters { get; } = [
        "Any", "< 10 KB", "10 KB - 1 MB", "1 MB - 100 MB", "> 100 MB", "> 1 GB", "Custom"
    ];

    public ObservableCollection<string> DateFilters { get; } = [
        "Any", "Today", "This week", "This month", "This year", "Custom"
    ];

    public ObservableCollection<string> TypeFilters { get; } = [
        "All", "Documents", "Images", "Videos", "Audio", "Archives", "Executables", "Code", "Custom"
    ];

    public SearchQuery BuildQuery(string rootPath)
    {
        var query = new SearchQuery
        {
            Text = SearchText,
            SearchPath = rootPath,
            IncludeFiles = IncludeFiles,
            IncludeFolders = IncludeFolders,
            MatchCase = MatchCase,
            UseRegex = UseRegex,
            MaxResults = 1000
        };

        ApplySizeFilter(query);
        ApplyDateFilter(query);
        ApplyTypeFilter(query);

        return query;
    }

    private void ApplySizeFilter(SearchQuery query)
    {
        query.MinSize = SelectedSizeFilter switch
        {
            "10 KB - 1 MB" => 10 * 1024L,
            "1 MB - 100 MB" => 1024 * 1024L,
            "> 100 MB" => 100 * 1024 * 1024L,
            "> 1 GB" => 1024 * 1024 * 1024L,
            "Custom" => MinSizeBytes,
            _ => null
        };
        query.MaxSize = SelectedSizeFilter switch
        {
            "< 10 KB" => 10 * 1024L,
            "10 KB - 1 MB" => 1024 * 1024L,
            "1 MB - 100 MB" => 100 * 1024 * 1024L,
            "Custom" => MaxSizeBytes,
            _ => null
        };
    }

    private void ApplyDateFilter(SearchQuery query)
    {
        var now = DateTime.Now;
        query.ModifiedAfter = SelectedDateFilter switch
        {
            "Today" => now.Date,
            "This week" => now.Date.AddDays(-(int)now.DayOfWeek),
            "This month" => new DateTime(now.Year, now.Month, 1),
            "This year" => new DateTime(now.Year, 1, 1),
            "Custom" => ModifiedAfter,
            _ => null
        };
        if (SelectedDateFilter == "Custom") query.ModifiedBefore = ModifiedBefore;
    }

    private void ApplyTypeFilter(SearchQuery query)
    {
        query.Extensions = SelectedTypeFilter switch
        {
            "Documents" => [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt"],
            "Images" => [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff"],
            "Videos" => [".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v"],
            "Audio" => [".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"],
            "Archives" => [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"],
            "Executables" => [".exe", ".msi", ".bat", ".cmd", ".ps1"],
            "Code" => [".cs", ".vb", ".fs", ".js", ".ts", ".py", ".cpp", ".c", ".h", ".java", ".go", ".rs"],
            "Custom" => string.IsNullOrEmpty(ExtensionFilter) ? null :
                ExtensionFilter.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.StartsWith('.') ? e : "." + e).ToArray(),
            _ => null
        };
    }

    [RelayCommand]
    private void ToggleAdvancedFilters() => ShowAdvancedFilters = !ShowAdvancedFilters;

    [RelayCommand]
    private void ClearFilters()
    {
        MinSizeBytes = null;
        MaxSizeBytes = null;
        ModifiedAfter = null;
        ModifiedBefore = null;
        SelectedSizeFilter = "Any";
        SelectedDateFilter = "Any";
        SelectedTypeFilter = "All";
        ExtensionFilter = string.Empty;
    }
}
