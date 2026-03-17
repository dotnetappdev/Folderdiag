using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderDiag.Core.Models;
using FolderDiag.Data.Repositories;

namespace FolderDiag.Wpf.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _repo;

    [ObservableProperty] private bool _showHiddenFiles;
    [ObservableProperty] private bool _showSystemFiles;
    [ObservableProperty] private bool _followSymlinks;
    [ObservableProperty] private bool _showFileExtensions = true;
    [ObservableProperty] private bool _showSizeBars = true;
    [ObservableProperty] private bool _showPercentage = true;
    [ObservableProperty] private bool _darkMode = true;
    [ObservableProperty] private bool _showDetailPane = true;
    [ObservableProperty] private bool _indexForSearch = true;
    [ObservableProperty] private bool _autoRefresh;
    [ObservableProperty] private int _autoRefreshInterval = 30;
    [ObservableProperty] private double _treePaneWidth = 280;
    [ObservableProperty] private double _detailPaneWidth = 340;

    public SettingsViewModel(ISettingsRepository repo)
    {
        _repo = repo;
    }

    public void LoadFrom(AppSettings settings)
    {
        ShowHiddenFiles = settings.ShowHiddenFiles;
        ShowSystemFiles = settings.ShowSystemFiles;
        FollowSymlinks = settings.FollowSymlinks;
        ShowFileExtensions = settings.ShowFileExtensions;
        ShowSizeBars = settings.ShowSizeBars;
        ShowPercentage = settings.ShowPercentage;
        DarkMode = settings.DarkMode;
        ShowDetailPane = settings.ShowDetailPane;
        IndexForSearch = settings.IndexForSearch;
        AutoRefresh = settings.AutoRefresh;
        AutoRefreshInterval = settings.AutoRefreshIntervalSeconds;
        TreePaneWidth = settings.TreePaneWidth;
        DetailPaneWidth = settings.DetailPaneWidth;
    }

    public AppSettings ToSettings() => new()
    {
        ShowHiddenFiles = ShowHiddenFiles,
        ShowSystemFiles = ShowSystemFiles,
        FollowSymlinks = FollowSymlinks,
        ShowFileExtensions = ShowFileExtensions,
        ShowSizeBars = ShowSizeBars,
        ShowPercentage = ShowPercentage,
        DarkMode = DarkMode,
        ShowDetailPane = ShowDetailPane,
        IndexForSearch = IndexForSearch,
        AutoRefresh = AutoRefresh,
        AutoRefreshIntervalSeconds = AutoRefreshInterval,
        TreePaneWidth = TreePaneWidth,
        DetailPaneWidth = DetailPaneWidth
    };

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repo.SaveAsync(ToSettings());
    }
}
