// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Files.App.Services.FolderSizes;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace Files.App.ViewModels.FolderAnalysis
{
    /// <summary>
    /// ViewModel for the FolderAnalysisPane — drives the FolderSizes analysis panel
    /// that overlays the standard Files app browsing experience.
    /// </summary>
    public sealed partial class FolderAnalysisPaneViewModel : ObservableObject
    {
        private readonly IFolderSizeService _sizeService = new FolderSizeService();
        private readonly DispatcherQueue _dispatcher;
        private CancellationTokenSource? _cts;

        // ── Observable properties ─────────────────────────────────────────

        [ObservableProperty] private string _currentPath = "";
        [ObservableProperty] private bool   _isScanning  = false;
        [ObservableProperty] private string _scanStatus  = "Select a folder then click Scan";
        [ObservableProperty] private double _scanProgress = 0;

        // Folder sizes list
        public ObservableCollection<FolderSizeResult>       FolderSizes    { get; } = [];
        // File reports
        public ObservableCollection<FileReportItem>         FileReports    { get; } = [];
        // Classification
        public ObservableCollection<TypeClassificationItem> Classification { get; } = [];
        // Disk space
        public ObservableCollection<DriveSpaceItem>         DriveSpaces    { get; } = [];
        // Snapshot list
        public ObservableCollection<ScanSnapshotEntry>      Snapshots      { get; } = [];
        // Chart data (name + % pairs)
        public ObservableCollection<ChartSlice>             ChartSlices    { get; } = [];

        // Current report mode
        [ObservableProperty] private int _selectedReportIndex = 0; // 0=Largest,1=Oldest,2=Temp,3=Duplicates
        [ObservableProperty] private int _topN = 50;

        // Totals
        [ObservableProperty] private string _totalSizeLabel   = "—";
        [ObservableProperty] private string _fileCountLabel   = "—";
        [ObservableProperty] private string _folderCountLabel = "—";

        public FolderAnalysisPaneViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            LoadDriveSpaces();
        }

        // ── Commands ──────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            IsScanning = true;
            ScanStatus = $"Scanning {CurrentPath}…";
            ScanProgress = 0;
            FolderSizes.Clear();
            ChartSlices.Clear();

            _sizeService.ProgressChanged += OnSizeProgress;

            try
            {
                var root = await _sizeService.AnalyzeAsync(CurrentPath, _cts.Token);

                // Populate FolderSizes with immediate children + root
                _dispatcher.TryEnqueue(() =>
                {
                    FolderSizes.Clear();
                    // Add root
                    root.PercentOfParent = 100;
                    FolderSizes.Add(root);

                    // Add immediate subdirs (use service cache via re-scan top-level dirs)
                    if (Directory.Exists(CurrentPath))
                    {
                        long parentSize = root.SizeBytes > 0 ? root.SizeBytes : 1;
                        foreach (var sub in Directory.GetDirectories(CurrentPath))
                        {
                            if (_sizeService is FolderSizeService fs && fs.TryGetCached(sub, out var cached))
                            {
                                cached.PercentOfParent = 100.0 * cached.SizeBytes / parentSize;
                                FolderSizes.Add(cached);
                            }
                        }
                    }

                    // Sort by size desc
                    var sorted = FolderSizes.Skip(1).OrderByDescending(f => f.SizeBytes).ToList();
                    FolderSizes.Clear();
                    FolderSizes.Add(root);
                    foreach (var f in sorted) FolderSizes.Add(f);

                    // Build chart slices from top 12 subfolders
                    BuildChartSlices(sorted.Take(12).ToList());

                    TotalSizeLabel   = FolderSizeResult.FormatBytes(root.SizeBytes);
                    FileCountLabel   = root.FileCount.ToString("N0");
                    FolderCountLabel = root.FolderCount.ToString("N0");

                    ScanStatus  = $"Done — {root.FileCount:N0} files in {FolderSizeResult.FormatBytes(root.SizeBytes)}";
                    ScanProgress = 100;
                });

                // Auto-load classification
                await LoadClassificationAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                ScanStatus = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                _sizeService.ProgressChanged -= OnSizeProgress;
            }
        }

        [RelayCommand]
        private void StopScan()
        {
            _cts?.Cancel();
            ScanStatus = "Stopping…";
        }

        [RelayCommand]
        private async Task LoadReportAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            _cts = new CancellationTokenSource();
            IsScanning = true;
            FileReports.Clear();

            try
            {
                IList<FileReportItem> items = SelectedReportIndex switch
                {
                    0 => await _sizeService.GetLargestFilesAsync(CurrentPath, TopN, _cts.Token),
                    1 => await _sizeService.GetOldestFilesAsync(CurrentPath, TopN, _cts.Token),
                    2 => await _sizeService.GetTempFilesAsync(CurrentPath, _cts.Token),
                    3 => await _sizeService.GetDuplicateFilesAsync(CurrentPath, _cts.Token),
                    _ => []
                };

                _dispatcher.TryEnqueue(() =>
                {
                    foreach (var i in items) FileReports.Add(i);
                    ScanStatus = $"Report: {items.Count} items";
                });
            }
            catch (OperationCanceledException) { }
            finally { IsScanning = false; }
        }

        [RelayCommand]
        private async Task LoadClassificationAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            var items = await _sizeService.GetClassificationByTypeAsync(CurrentPath, ct);
            _dispatcher.TryEnqueue(() =>
            {
                Classification.Clear();
                foreach (var i in items) Classification.Add(i);
            });
        }

        [RelayCommand]
        private void LoadDriveSpaces()
        {
            DriveSpaces.Clear();
            foreach (var d in DriveSpaceItem.GetAll()) DriveSpaces.Add(d);
        }

        [RelayCommand]
        private void SaveSnapshot()
        {
            Snapshots.Add(new ScanSnapshotEntry
            {
                Name      = $"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm}",
                Path      = CurrentPath,
                SavedAt   = DateTime.Now,
                TotalSize = FolderSizes.FirstOrDefault()?.SizeBytes ?? 0,
                Files     = FolderSizes.FirstOrDefault()?.FileCount ?? 0
            });
        }

        // ── Internal ──────────────────────────────────────────────────────

        private void OnSizeProgress(object? sender, FolderSizeProgressEventArgs e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                ScanStatus   = $"Scanning: {System.IO.Path.GetFileName(e.CurrentPath)}…  ({e.FileCount:N0} files)";
                ScanProgress = Math.Min(99, ScanProgress + 0.5);
            });
        }

        private void BuildChartSlices(IList<FolderSizeResult> items)
        {
            ChartSlices.Clear();
            long total = items.Sum(f => f.SizeBytes);
            if (total == 0) return;

            var palette = new[]
            {
                "#0078D4","#107C10","#D83B01","#8E8CD8","#008B8B",
                "#B146C2","#E87423","#007ACC","#497E2A","#7A2978"
            };
            for (int i = 0; i < items.Count; i++)
            {
                ChartSlices.Add(new ChartSlice
                {
                    Label   = items[i].Name,
                    Value   = items[i].SizeBytes,
                    Percent = total > 0 ? 100.0 * items[i].SizeBytes / total : 0,
                    Color   = palette[i % palette.Length]
                });
            }
        }
    }

    // ── Supporting DTOs ───────────────────────────────────────────────────

    public sealed class ScanSnapshotEntry
    {
        public string   Name      { get; set; } = "";
        public string   Path      { get; set; } = "";
        public DateTime SavedAt   { get; set; }
        public long     TotalSize { get; set; }
        public int      Files     { get; set; }
        public string   TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string   SavedAtFormatted   => SavedAt.ToString("yyyy-MM-dd HH:mm");
    }

    public sealed class ChartSlice
    {
        public string Label   { get; set; } = "";
        public long   Value   { get; set; }
        public double Percent { get; set; }
        public string Color   { get; set; } = "#0078D4";
        public string PercentFormatted => $"{Percent:F1}%";
        public string ValueFormatted   => FolderSizeResult.FormatBytes(Value);
    }
}
