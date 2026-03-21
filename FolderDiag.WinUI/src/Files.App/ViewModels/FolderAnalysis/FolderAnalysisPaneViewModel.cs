// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Files.App.Services.FolderSizes;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.IO;

namespace Files.App.ViewModels.FolderAnalysis
{
    /// <summary>
    /// ViewModel for FolderAnalysisPane — drives the complete FolderSizes-compatible
    /// analysis panel with all report types, charts, search, trends, and export.
    /// </summary>
    public sealed partial class FolderAnalysisPaneViewModel : ObservableObject
    {
        private readonly IFolderSizeService _svc = new FolderSizeService();
        private readonly DispatcherQueue    _dispatcher;
        private CancellationTokenSource?    _cts;

        // ══════════════════════════════════════════════════════════════════════
        // Observable properties — scan state
        // ══════════════════════════════════════════════════════════════════════

        [ObservableProperty] private string _currentPath    = "";
        [ObservableProperty] private bool   _isScanning     = false;
        [ObservableProperty] private string _scanStatus     = "Select a folder then click Scan";
        [ObservableProperty] private double _scanProgress   = 0;
        [ObservableProperty] private string _totalSizeLabel = "—";
        [ObservableProperty] private string _fileCountLabel = "—";
        [ObservableProperty] private string _folderCountLabel = "—";
        [ObservableProperty] private string _allocatedLabel = "—";
        [ObservableProperty] private string _scanDuration   = "";

        // ── Report selector ────────────────────────────────────────────────
        // 0=Largest 1=Oldest 2=Newest 3=Temp 4=Duplicates 5=Owners 6=Attributes
        [ObservableProperty] private int _selectedReportIndex = 0;
        [ObservableProperty] private int _topN               = 50;

        // ── Distribution selector: 0=Age 1=Depth 2=Size ───────────────────
        [ObservableProperty] private int _selectedDistributionIndex = 0;

        // ── Search fields ──────────────────────────────────────────────────
        [ObservableProperty] private string _searchName      = "";
        [ObservableProperty] private string _searchExtension = "";
        [ObservableProperty] private string _searchMinSize   = "";
        [ObservableProperty] private string _searchMaxSize   = "";
        [ObservableProperty] private string _searchMinAge    = "";
        [ObservableProperty] private string _searchMaxAge    = "";
        [ObservableProperty] private string _searchOwner     = "";
        [ObservableProperty] private bool   _searchReadOnly  = false;
        [ObservableProperty] private bool   _searchHidden    = false;
        [ObservableProperty] private bool   _searchTempOnly  = false;
        [ObservableProperty] private int    _searchResultCount = 0;

        // ── Settings ────────────────────────────────────────────────────────
        [ObservableProperty] private string _exportFolder    = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        [ObservableProperty] private int    _selectedExportFormat = 0; // 0=CSV 1=XML 2=HTML
        [ObservableProperty] private bool   _scanHiddenFiles = true;
        [ObservableProperty] private bool   _scanSystemFiles = false;
        [ObservableProperty] private int    _treemapDepth    = 2;
        [ObservableProperty] private string _lastExportPath  = "";

        // ══════════════════════════════════════════════════════════════════════
        // Observable collections
        // ══════════════════════════════════════════════════════════════════════

        // Sizes tab
        public ObservableCollection<FolderSizeResult>       FolderSizes    { get; } = [];
        public ObservableCollection<ChartSlice>             ChartSlices    { get; } = [];   // bar chart
        public ObservableCollection<TreemapNode>            TreemapNodes   { get; } = [];   // treemap / sunburst

        // Reports tab
        public ObservableCollection<FileReportItem>         FileReports    { get; } = [];

        // Distribution tab
        public ObservableCollection<FileAgeGroupItem>       AgeDistribution  { get; } = [];
        public ObservableCollection<FileDepthItem>          DepthReport      { get; } = [];
        public ObservableCollection<FileSizeGroupItem>      SizeDistribution { get; } = [];

        // Classification tabs
        public ObservableCollection<TypeClassificationItem> Classification  { get; } = [];
        public ObservableCollection<FileOwnerItem>          OwnerReport     { get; } = [];
        public ObservableCollection<FileAttributeItem>      AttributeReport { get; } = [];
        public ObservableCollection<ChartSlice>             PieSlices       { get; } = [];  // type pie

        // Disk tab
        public ObservableCollection<DriveSpaceItem>         DriveSpaces     { get; } = [];

        // Search tab
        public ObservableCollection<FileReportItem>         SearchResults   { get; } = [];

        // Trend tab
        public ObservableCollection<TrendPoint>             TrendPoints     { get; } = [];

        // Snapshots tab
        public ObservableCollection<ScanSnapshotEntry>      Snapshots       { get; } = [];
        public ObservableCollection<SnapshotDiffItem>       DiffItems       { get; } = [];

        // ══════════════════════════════════════════════════════════════════════
        // Constructor
        // ══════════════════════════════════════════════════════════════════════

        public FolderAnalysisPaneViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            LoadDriveSpaces();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Scan
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            IsScanning  = true;
            ScanStatus  = $"Scanning {CurrentPath}…";
            ScanProgress= 0;
            FolderSizes.Clear();
            ChartSlices.Clear();
            TreemapNodes.Clear();

            _svc.ProgressChanged += OnProgress;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var root = await _svc.AnalyzeAsync(CurrentPath, _cts.Token);
                sw.Stop();

                _dispatcher.TryEnqueue(() =>
                {
                    // Root
                    root.PercentOfParent = 100;
                    FolderSizes.Add(root);

                    // Immediate subdirs
                    long parentSize = root.SizeBytes > 0 ? root.SizeBytes : 1;
                    var  svc        = (FolderSizeService)_svc;
                    var  subs       = new List<FolderSizeResult>();

                    if (Directory.Exists(CurrentPath))
                    {
                        foreach (var sub in Directory.GetDirectories(CurrentPath))
                        {
                            if (svc.TryGetCached(sub, out var cached))
                            {
                                cached.PercentOfParent = 100.0 * cached.SizeBytes / parentSize;
                                subs.Add(cached);
                            }
                        }
                    }

                    foreach (var s in subs.OrderByDescending(f => f.SizeBytes))
                        FolderSizes.Add(s);

                    BuildChartSlices(subs.OrderByDescending(f => f.SizeBytes).Take(15).ToList());

                    TotalSizeLabel   = FolderSizeResult.FormatBytes(root.SizeBytes);
                    AllocatedLabel   = FolderSizeResult.FormatBytes(root.AllocatedBytes);
                    FileCountLabel   = root.FileCount.ToString("N0");
                    FolderCountLabel = root.FolderCount.ToString("N0");
                    ScanDuration     = $"{sw.Elapsed.TotalSeconds:F1}s";
                    ScanStatus       = $"Done — {root.FileCount:N0} files, {FolderSizeResult.FormatBytes(root.SizeBytes)} in {sw.Elapsed.TotalSeconds:F1}s";
                    ScanProgress     = 100;
                });

                // Run supporting reports in parallel
                var t1 = LoadClassificationInternalAsync(_cts.Token);
                var t2 = BuildTreemapInternalAsync(_cts.Token);
                await Task.WhenAll(t1, t2);
            }
            catch (OperationCanceledException) { ScanStatus = "Scan cancelled."; }
            catch (Exception ex)               { ScanStatus = $"Error: {ex.Message}"; }
            finally
            {
                IsScanning = false;
                _svc.ProgressChanged -= OnProgress;
            }
        }

        [RelayCommand]
        private void StopScan() { _cts?.Cancel(); ScanStatus = "Stopping…"; }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — File Reports tab
        // ══════════════════════════════════════════════════════════════════════

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
                    0 => await _svc.GetLargestFilesAsync(CurrentPath,  TopN, _cts.Token),
                    1 => await _svc.GetOldestFilesAsync(CurrentPath,   TopN, _cts.Token),
                    2 => await _svc.GetNewestFilesAsync(CurrentPath,   TopN, _cts.Token),
                    3 => await _svc.GetTempFilesAsync(CurrentPath,           _cts.Token),
                    4 => await _svc.GetDuplicateFilesAsync(CurrentPath,      _cts.Token),
                    5 => (IList<FileReportItem>)((await _svc.GetFilesByOwnerAsync(CurrentPath, _cts.Token))
                            .SelectMany(o => Enumerable.Range(0, 1).Select(_ => new FileReportItem
                                { Name = o.Owner, SizeBytes = o.TotalSize, FolderPath = $"{o.Count} files ({o.PercentFormatted})" }))
                            .ToList()),
                    6 => (IList<FileReportItem>)((await _svc.GetFileAttributesReportAsync(CurrentPath, _cts.Token))
                            .SelectMany(a => Enumerable.Range(0, 1).Select(_ => new FileReportItem
                                { Name = a.AttributeLabel, SizeBytes = a.TotalSize, FolderPath = $"{a.Count} files ({a.PercentFormatted})" }))
                            .ToList()),
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

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Distribution tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task LoadDistributionAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            _cts = new CancellationTokenSource();
            IsScanning = true;

            try
            {
                switch (SelectedDistributionIndex)
                {
                    case 0:
                        var ages = await _svc.GetFileAgeDistributionAsync(CurrentPath, _cts.Token);
                        _dispatcher.TryEnqueue(() => { AgeDistribution.Clear(); foreach (var i in ages) AgeDistribution.Add(i); });
                        break;
                    case 1:
                        var depths = await _svc.GetFileDepthReportAsync(CurrentPath, _cts.Token);
                        _dispatcher.TryEnqueue(() => { DepthReport.Clear(); foreach (var i in depths) DepthReport.Add(i); });
                        break;
                    case 2:
                        var sizes = await _svc.GetFileSizeDistributionAsync(CurrentPath, _cts.Token);
                        _dispatcher.TryEnqueue(() => { SizeDistribution.Clear(); foreach (var i in sizes) SizeDistribution.Add(i); });
                        break;
                }
            }
            catch (OperationCanceledException) { }
            finally { IsScanning = false; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Classification tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task LoadClassificationAsync(CancellationToken ct = default)
            => await LoadClassificationInternalAsync(ct);

        private async Task LoadClassificationInternalAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            var items = await _svc.GetClassificationByTypeAsync(CurrentPath, ct);
            _dispatcher.TryEnqueue(() =>
            {
                Classification.Clear();
                PieSlices.Clear();
                long total = items.Sum(i => i.TotalSize);
                var  pal   = new[] { "#0078D4","#107C10","#D83B01","#8E8CD8","#008B8B","#B146C2","#E87423","#007ACC","#497E2A","#7A2978" };
                int  ci    = 0;
                foreach (var i in items)
                {
                    Classification.Add(i);
                    PieSlices.Add(new ChartSlice { Label = i.Category, Value = i.TotalSize, Percent = i.Percent, Color = pal[ci++ % pal.Length] });
                }
            });
        }

        [RelayCommand]
        private async Task LoadOwnerReportAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            _cts = new CancellationTokenSource();
            IsScanning = true;
            try
            {
                var items = await _svc.GetFilesByOwnerAsync(CurrentPath, _cts.Token);
                _dispatcher.TryEnqueue(() => { OwnerReport.Clear(); foreach (var i in items) OwnerReport.Add(i); });
            }
            catch (OperationCanceledException) { }
            finally { IsScanning = false; }
        }

        [RelayCommand]
        private async Task LoadAttributeReportAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            _cts = new CancellationTokenSource();
            IsScanning = true;
            try
            {
                var items = await _svc.GetFileAttributesReportAsync(CurrentPath, _cts.Token);
                _dispatcher.TryEnqueue(() => { AttributeReport.Clear(); foreach (var i in items) AttributeReport.Add(i); });
            }
            catch (OperationCanceledException) { }
            finally { IsScanning = false; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Chart (Treemap/Sunburst)
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task BuildTreemapAsync() => await BuildTreemapInternalAsync(CancellationToken.None);

        private async Task BuildTreemapInternalAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            var nodes = await _svc.BuildTreemapAsync(CurrentPath, TreemapDepth, ct);
            _dispatcher.TryEnqueue(() => { TreemapNodes.Clear(); foreach (var n in nodes) TreemapNodes.Add(n); });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Disk Space tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void LoadDriveSpaces()
        {
            DriveSpaces.Clear();
            foreach (var d in DriveSpaceItem.GetAll()) DriveSpaces.Add(d);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Search tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task SearchFilesAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            _cts = new CancellationTokenSource();
            IsScanning = true;
            SearchResults.Clear();

            try
            {
                var filter = new SearchFilter
                {
                    NamePattern   = SearchName,
                    Extension     = string.IsNullOrWhiteSpace(SearchExtension) ? null : SearchExtension,
                    Owner         = string.IsNullOrWhiteSpace(SearchOwner) ? null : SearchOwner,
                    MinSizeBytes  = TryParseSize(SearchMinSize),
                    MaxSizeBytes  = TryParseSize(SearchMaxSize),
                    MinAgeDays    = TryParseInt(SearchMinAge),
                    MaxAgeDays    = TryParseInt(SearchMaxAge),
                    IsReadOnly    = SearchReadOnly ? true : null,
                    IsHidden      = SearchHidden   ? true : null,
                    IsTemp        = SearchTempOnly ? true : null,
                };

                var results = await _svc.SearchFilesAsync(CurrentPath, filter, _cts.Token);
                _dispatcher.TryEnqueue(() =>
                {
                    foreach (var r in results) SearchResults.Add(r);
                    SearchResultCount = results.Count;
                    ScanStatus = $"Search: {results.Count} matches";
                });
            }
            catch (OperationCanceledException) { }
            finally { IsScanning = false; }
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchName = SearchExtension = SearchMinSize = SearchMaxSize = SearchMinAge = SearchMaxAge = SearchOwner = "";
            SearchReadOnly = SearchHidden = SearchTempOnly = false;
            SearchResults.Clear();
            SearchResultCount = 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Trend tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void AddTrendPoint()
        {
            var root = FolderSizes.FirstOrDefault();
            if (root == null) return;

            TrendPoints.Add(new TrendPoint
            {
                Label     = $"Scan {TrendPoints.Count + 1}",
                Timestamp = DateTime.Now,
                SizeBytes = root.SizeBytes,
                FileCount = root.FileCount
            });

            // Normalise %
            long max = TrendPoints.Max(p => p.SizeBytes);
            foreach (var p in TrendPoints)
                p.Percent = max > 0 ? 100.0 * p.SizeBytes / max : 0;
        }

        [RelayCommand]
        private void ClearTrend() => TrendPoints.Clear();

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Snapshots tab
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void SaveSnapshot()
        {
            var root = FolderSizes.FirstOrDefault();
            if (root == null) return;

            Snapshots.Add(new ScanSnapshotEntry
            {
                Name       = $"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm}",
                Path       = CurrentPath,
                SavedAt    = DateTime.Now,
                TotalSize  = root.SizeBytes,
                Files      = root.FileCount,
                Folders    = root.FolderCount,
                Allocated  = root.AllocatedBytes,
                FolderData = [.. FolderSizes]
            });
            ScanStatus = $"Snapshot saved ({Snapshots.Count} total)";
        }

        [RelayCommand]
        private void CompareSnapshots()
        {
            if (Snapshots.Count < 2) { ScanStatus = "Need at least 2 snapshots to compare."; return; }

            var before = Snapshots[Snapshots.Count - 2];
            var after  = Snapshots[Snapshots.Count - 1];
            DiffItems.Clear();

            var beforeDict = before.FolderData.ToDictionary(f => f.Name, f => f.SizeBytes);
            var afterDict  = after.FolderData.ToDictionary(f => f.Name,  f => f.SizeBytes);

            var allNames = beforeDict.Keys.Union(afterDict.Keys).Distinct();
            foreach (var name in allNames)
            {
                beforeDict.TryGetValue(name, out long bSize);
                afterDict.TryGetValue(name, out long aSize);
                if (bSize != aSize)
                    DiffItems.Add(new SnapshotDiffItem { Name = name, SizeBefore = bSize, SizeAfter = aSize });
            }

            DiffItems.Add(new SnapshotDiffItem
            {
                Name       = "TOTAL",
                SizeBefore = before.TotalSize,
                SizeAfter  = after.TotalSize
            });

            ScanStatus = $"Compare: {DiffItems.Count - 1} folders changed";
        }

        // ══════════════════════════════════════════════════════════════════════
        // Commands — Export
        // ══════════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task ExportFileReportAsync()
        {
            if (FileReports.Count == 0) { ScanStatus = "No report data to export."; return; }
            var fmt  = (ExportFormat)SelectedExportFormat;
            var ext  = fmt switch { ExportFormat.Xml => "xml", ExportFormat.Html => "html", _ => "csv" };
            var path = Path.Combine(ExportFolder, $"FolderDiag_Report_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
            await _svc.ExportReportAsync(path, fmt, [.. FileReports]);
            LastExportPath = path;
            ScanStatus     = $"Exported → {path}";
        }

        [RelayCommand]
        private async Task ExportFolderSizesAsync()
        {
            if (FolderSizes.Count == 0) { ScanStatus = "No folder size data to export."; return; }
            var fmt  = (ExportFormat)SelectedExportFormat;
            var ext  = fmt switch { ExportFormat.Xml => "xml", ExportFormat.Html => "html", _ => "csv" };
            var path = Path.Combine(ExportFolder, $"FolderDiag_Sizes_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
            await _svc.ExportFolderSizesAsync(path, fmt, [.. FolderSizes]);
            LastExportPath = path;
            ScanStatus     = $"Exported → {path}";
        }

        // ══════════════════════════════════════════════════════════════════════
        // Internal helpers
        // ══════════════════════════════════════════════════════════════════════

        private void OnProgress(object? sender, FolderSizeProgressEventArgs e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                ScanStatus   = $"Scanning: {Path.GetFileName(e.CurrentPath)}…  ({e.FileCount:N0} files)";
                ScanProgress = Math.Min(99, ScanProgress + 0.4);
            });
        }

        private void BuildChartSlices(IList<FolderSizeResult> items)
        {
            ChartSlices.Clear();
            long total = items.Sum(f => f.SizeBytes);
            if (total == 0) return;
            var pal = new[] { "#0078D4","#107C10","#D83B01","#8E8CD8","#008B8B","#B146C2","#E87423","#007ACC","#497E2A","#7A2978","#005A9E","#00B294","#FF8C00","#6B69D6","#038387" };
            for (int i = 0; i < items.Count; i++)
                ChartSlices.Add(new ChartSlice { Label = items[i].Name, Value = items[i].SizeBytes, Percent = total > 0 ? 100.0 * items[i].SizeBytes / total : 0, Color = pal[i % pal.Length] });
        }

        private static long? TryParseSize(string s)
        {
            s = s.Trim().ToUpper();
            if (string.IsNullOrEmpty(s)) return null;
            if (s.EndsWith("GB") && long.TryParse(s[..^2], out long gb)) return gb * 1_073_741_824;
            if (s.EndsWith("MB") && long.TryParse(s[..^2], out long mb)) return mb * 1_048_576;
            if (s.EndsWith("KB") && long.TryParse(s[..^2], out long kb)) return kb * 1024;
            return long.TryParse(s, out long b) ? b : null;
        }

        private static int? TryParseInt(string s) => int.TryParse(s.Trim(), out int v) ? v : null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Supporting DTOs
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class ScanSnapshotEntry
    {
        public string              Name       { get; set; } = "";
        public string              Path       { get; set; } = "";
        public DateTime            SavedAt    { get; set; }
        public long                TotalSize  { get; set; }
        public int                 Files      { get; set; }
        public int                 Folders    { get; set; }
        public long                Allocated  { get; set; }
        public List<FolderSizeResult> FolderData { get; set; } = [];

        public string TotalSizeFormatted => FolderSizeResult.FormatBytes(TotalSize);
        public string AllocatedFormatted => FolderSizeResult.FormatBytes(Allocated);
        public string SavedAtFormatted   => SavedAt.ToString("yyyy-MM-dd HH:mm");
        public string SummaryLine        => $"{Files:N0} files · {FolderSizeResult.FormatBytes(TotalSize)} · {Folders:N0} folders";
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
