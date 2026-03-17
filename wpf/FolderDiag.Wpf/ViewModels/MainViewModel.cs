using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderDiag.Core.Models;
using FolderDiag.Core.Services;
using FolderDiag.Data.Repositories;
using FolderDiag.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace FolderDiag.Wpf.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFileSystemScanner _scanner;
    private readonly ISearchService _searchService;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IScanHistoryRepository _historyRepo;
    private readonly ILogger<MainViewModel> _logger;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private FileTreeViewModel _fileTree;
    [ObservableProperty] private SearchViewModel _search;
    [ObservableProperty] private SettingsViewModel _settingsVm;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _scanProgressText = string.Empty;
    [ObservableProperty] private ScanResult? _lastScanResult;
    [ObservableProperty] private AppSettings _settings = new();

    [ObservableProperty] private string _currentPath = string.Empty;
    [ObservableProperty] private FileSystemEntry? _selectedEntry;
    [ObservableProperty] private FileSystemEntry? _currentDirectory;

    [ObservableProperty] private ObservableCollection<FileSystemEntry> _currentItems = [];
    [ObservableProperty] private ObservableCollection<BreadcrumbItem> _breadcrumbs = [];
    [ObservableProperty] private ViewMode _viewMode = ViewMode.Details;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchActive;
    [ObservableProperty] private ObservableCollection<FileSystemEntry> _searchResults = [];

    [ObservableProperty] private SortColumn _sortColumn = SortColumn.TotalSize;
    [ObservableProperty] private bool _sortDescending = true;

    [ObservableProperty] private string _totalSizeText = string.Empty;
    [ObservableProperty] private string _fileCountText = string.Empty;
    [ObservableProperty] private int _selectedCount;

    public ObservableCollection<DriveViewModel> Drives { get; } = [];
    public ObservableCollection<string> RecentPaths { get; } = [];
    public ObservableCollection<ScanHistoryItemViewModel> ScanHistory { get; } = [];

    public MainViewModel(
        IFileSystemScanner scanner,
        ISearchService searchService,
        ISettingsRepository settingsRepo,
        IScanHistoryRepository historyRepo,
        ILogger<MainViewModel> logger,
        FileTreeViewModel fileTree,
        SearchViewModel search,
        SettingsViewModel settingsVm)
    {
        _scanner = scanner;
        _searchService = searchService;
        _settingsRepo = settingsRepo;
        _historyRepo = historyRepo;
        _logger = logger;
        _fileTree = fileTree;
        _search = search;
        _settingsVm = settingsVm;
    }

    public async Task InitializeAsync()
    {
        Settings = await _settingsRepo.LoadAsync();
        ViewMode = Settings.DefaultViewMode;
        SortColumn = Settings.DefaultSortColumn;
        SortDescending = Settings.DefaultSortDescending;

        LoadDrives();
        await LoadScanHistoryAsync();
        await LoadRecentPathsAsync();

        if (!string.IsNullOrEmpty(Settings.LastScannedPath))
            CurrentPath = Settings.LastScannedPath;
    }

    private void LoadDrives()
    {
        Drives.Clear();
        foreach (var drive in _scanner.GetDrives())
        {
            Drives.Add(new DriveViewModel(drive));
        }
    }

    private async Task LoadScanHistoryAsync()
    {
        ScanHistory.Clear();
        var history = await _historyRepo.GetRecentScansAsync(20);
        foreach (var h in history)
        {
            ScanHistory.Add(new ScanHistoryItemViewModel
            {
                Path = h.RootPath,
                ScanTime = h.ScanTime,
                TotalSize = FileSizeHelper.Format(h.TotalSize),
                TotalFiles = h.TotalFiles
            });
        }
    }

    private async Task LoadRecentPathsAsync()
    {
        RecentPaths.Clear();
        foreach (var p in Settings.RecentPaths.Take(10))
            RecentPaths.Add(p);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ScanAsync(string? path = null)
    {
        var target = path ?? CurrentPath;
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
        {
            StatusMessage = "Please select a valid folder to scan.";
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        IsBusy = true;
        ScanProgress = 0;
        StatusMessage = $"Scanning {target}...";
        CurrentPath = target;

        var progress = new Progress<ScanProgress>(p =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ScanProgressText = $"Scanned {p.FoldersScanned:N0} folders, {p.FilesScanned:N0} files ({FileSizeHelper.FormatShort(p.BytesScanned)})";
                StatusMessage = $"Scanning: {p.CurrentPath.Length > 60 ? "..." + p.CurrentPath[^57..] : p.CurrentPath}";
            });
        });

        try
        {
            var result = await _scanner.ScanAsync(target, progress, _scanCts.Token);
            LastScanResult = result;

            if (result.Root != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FileTree.SetRoot(result.Root);
                    NavigateTo(result.Root);
                    UpdateBreadcrumbs(result.Root.FullPath);
                    TotalSizeText = result.Root.TotalSizeFormatted;
                    FileCountText = $"{result.TotalFiles:N0} files in {result.TotalFolders:N0} folders";
                    StatusMessage = $"Scan complete: {result.Root.TotalSizeFormatted} in {result.TotalFiles:N0} files, {result.TotalFolders:N0} folders ({result.ScanDuration.TotalSeconds:F1}s)";
                });
            }

            AddToRecentPaths(target);
            await _settingsRepo.SaveAsync(Settings);
            await _historyRepo.SaveScanAsync(result);
            await LoadScanHistoryAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
            _logger.LogError(ex, "Scan failed for {Path}", target);
        }
        finally
        {
            IsScanning = false;
            IsBusy = false;
            ScanProgress = 0;
            ScanProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
        StatusMessage = "Cancelling scan...";
    }

    [RelayCommand]
    private void NavigateTo(FileSystemEntry entry)
    {
        CurrentDirectory = entry;
        CurrentItems.Clear();

        if (entry.IsDirectory)
        {
            var items = entry.Children.ToList();
            items = SortItems(items);
            foreach (var item in items)
                CurrentItems.Add(item);
        }

        UpdateBreadcrumbs(entry.FullPath);
        SelectedEntry = entry;
        UpdateStatusForDirectory(entry);
        FileTree.SelectEntry(entry);
    }

    [RelayCommand]
    private void NavigateUp()
    {
        if (CurrentDirectory?.Parent != null)
            NavigateTo(CurrentDirectory.Parent);
        else if (CurrentDirectory != null)
        {
            var parent = Path.GetDirectoryName(CurrentDirectory.FullPath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                _ = ScanAsync(parent);
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ClearSearch();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        IsSearchActive = true;
        SearchResults.Clear();
        StatusMessage = $"Searching for '{SearchText}'...";

        var query = new SearchQuery
        {
            Text = SearchText,
            SearchPath = CurrentPath,
            MaxResults = 500
        };

        try
        {
            int count = 0;
            await foreach (var entry in _searchService.SearchLiveAsync(CurrentPath, query, _searchCts.Token))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SearchResults.Add(entry);
                    count++;
                    if (count % 10 == 0)
                        StatusMessage = $"Found {count} matches...";
                });
            }
            StatusMessage = $"Search complete: {SearchResults.Count} results for '{SearchText}'";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        _searchCts?.Cancel();
        SearchText = string.Empty;
        IsSearchActive = false;
        SearchResults.Clear();
        StatusMessage = "Ready";
    }

    [RelayCommand]
    private void SetViewMode(ViewMode mode)
    {
        ViewMode = mode;
    }

    [RelayCommand]
    private void SortBy(SortColumn column)
    {
        if (SortColumn == column)
            SortDescending = !SortDescending;
        else
        {
            SortColumn = column;
            SortDescending = column == SortColumn.TotalSize;
        }
        RefreshSort();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to analyze",
            ShowNewFolderButton = false,
            SelectedPath = CurrentPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _ = ScanAsync(dialog.SelectedPath);
    }

    [RelayCommand]
    private void OpenInExplorer(FileSystemEntry? entry)
    {
        var path = entry?.FullPath ?? CurrentPath;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                if (File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to open explorer"); }
        }
    }

    [RelayCommand]
    private void DeleteEntry(FileSystemEntry? entry)
    {
        if (entry == null) return;
        var msg = $"Delete {(entry.IsDirectory ? "folder" : "file")}:\n{entry.FullPath}\n\nThis cannot be undone.";
        var result = MessageBox.Show(msg, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (entry.IsDirectory) Directory.Delete(entry.FullPath, true);
                else File.Delete(entry.FullPath);
                entry.Parent?.Children.Remove(entry);
                CurrentItems.Remove(entry);
                StatusMessage = $"Deleted: {entry.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (LastScanResult?.Root == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"FolderDiag_{Path.GetFileName(LastScanResult.RootPath)}_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (dlg.ShowDialog() == true)
        {
            await ExportService.ExportToCsvAsync(LastScanResult.Root, dlg.FileName);
            StatusMessage = $"Exported to {dlg.FileName}";
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (!string.IsNullOrEmpty(CurrentPath))
            _ = ScanAsync(CurrentPath);
    }

    private void RefreshSort()
    {
        var sorted = SortItems(CurrentItems.ToList());
        CurrentItems.Clear();
        foreach (var i in sorted) CurrentItems.Add(i);
    }

    private List<FileSystemEntry> SortItems(List<FileSystemEntry> items)
    {
        IEnumerable<FileSystemEntry> sorted = SortColumn switch
        {
            SortColumn.Name => SortDescending
                ? items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            SortColumn.TotalSize => SortDescending
                ? items.OrderByDescending(x => x.TotalSize > 0 ? x.TotalSize : x.Size)
                : items.OrderBy(x => x.TotalSize > 0 ? x.TotalSize : x.Size),
            SortColumn.LastModified => SortDescending
                ? items.OrderByDescending(x => x.LastModified)
                : items.OrderBy(x => x.LastModified),
            SortColumn.FileCount => SortDescending
                ? items.OrderByDescending(x => x.FileCount)
                : items.OrderBy(x => x.FileCount),
            SortColumn.Type => SortDescending
                ? items.OrderByDescending(x => x.IsDirectory).ThenByDescending(x => x.Extension)
                : items.OrderBy(x => x.IsDirectory).ThenBy(x => x.Extension),
            _ => SortDescending
                ? items.OrderByDescending(x => x.TotalSize > 0 ? x.TotalSize : x.Size)
                : items.OrderBy(x => x.TotalSize > 0 ? x.TotalSize : x.Size)
        };

        // Always directories first
        return sorted
            .OrderByDescending(x => x.IsDirectory)
            .ThenBy(x => SortColumn == SortColumn.Name ? 0 : 1) // preserve inner sort
            .ToList();
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        var parts = new List<string>();
        var current = path;

        while (!string.IsNullOrEmpty(current))
        {
            parts.Insert(0, current);
            var parent = Path.GetDirectoryName(current);
            if (parent == current || parent == null) break;
            current = parent;
        }

        foreach (var part in parts)
        {
            var name = Path.GetFileName(part);
            if (string.IsNullOrEmpty(name)) name = part; // drive root
            Breadcrumbs.Add(new BreadcrumbItem { Path = part, Name = name });
        }
    }

    private void UpdateStatusForDirectory(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            TotalSizeText = entry.TotalSizeFormatted;
            FileCountText = entry.TotalItemsFormatted;
        }
    }

    private void AddToRecentPaths(string path)
    {
        Settings.RecentPaths.Remove(path);
        Settings.RecentPaths.Insert(0, path);
        while (Settings.RecentPaths.Count > Settings.MaxRecentPaths)
            Settings.RecentPaths.RemoveAt(Settings.RecentPaths.Count - 1);
        Settings.LastScannedPath = path;
        RecentPaths.Clear();
        foreach (var p in Settings.RecentPaths.Take(10)) RecentPaths.Add(p);
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
            ClearSearch();
    }
}

public sealed class BreadcrumbItem
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class DriveViewModel : ObservableObject
{
    public string Name { get; }
    public string Label { get; }
    public long TotalSize { get; }
    public long FreeSpace { get; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double UsedPercent => TotalSize > 0 ? UsedSpace / (double)TotalSize * 100 : 0;
    public string TotalSizeText => FileSizeHelper.Format(TotalSize);
    public string FreeSpaceText => FileSizeHelper.Format(FreeSpace);
    public string UsedSpaceText => FileSizeHelper.Format(UsedSpace);
    public string DriveType { get; }
    public string RootPath { get; }

    public DriveViewModel(DriveInfo drive)
    {
        RootPath = drive.RootDirectory.FullName;
        Name = drive.Name;
        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name})";
        TotalSize = drive.TotalSize;
        FreeSpace = drive.AvailableFreeSpace;
        DriveType = drive.DriveType.ToString();
    }
}

public sealed class ScanHistoryItemViewModel
{
    public string Path { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public string TotalSize { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public string TimeAgo => GetTimeAgo();

    private string GetTimeAgo()
    {
        var diff = DateTime.Now - ScanTime;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return ScanTime.ToString("MMM dd, yyyy");
    }
}
