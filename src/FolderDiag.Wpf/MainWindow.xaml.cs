using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using FolderDiag.Core.Models;
using FolderDiag.Data;

namespace FolderDiag.Wpf
{
    // ── View-model for file browser rows (files AND folders) ──────────────
    public class BrowserEntry
    {
        public bool   IsFolder   { get; set; }
        public string Name       { get; set; } = "";
        public string Path       { get; set; } = "";
        public string Extension  { get; set; } = "";
        public long   SizeBytes  { get; set; }
        public DateTime? Modified { get; set; }
        public DateTime? Created  { get; set; }
        public string? Owner     { get; set; }
        public string Icon       => IsFolder ? "📁" : GetFileIcon(Extension);

        private static string GetFileIcon(string ext) => ext.ToLowerInvariant() switch
        {
            ".exe" or ".msi"                     => "⚙",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" or ".webp" => "🖼",
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv"  => "🎬",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "🎵",
            ".doc" or ".docx"                    => "📝",
            ".xls" or ".xlsx"                    => "📊",
            ".ppt" or ".pptx"                    => "📑",
            ".pdf"                               => "📕",
            ".zip" or ".rar" or ".7z" or ".tar"  => "🗜",
            ".txt" or ".md"                      => "📄",
            ".cs" or ".vb" or ".cpp" or ".py" or ".js" or ".ts" or ".go" or ".rs" => "💻",
            ".xml" or ".json" or ".yaml" or ".yml" or ".toml" => "⚙",
            _                                    => "📄"
        };
    }

    // ── View-model for classification rows ────────────────────────────────
    public class ClassifyRow
    {
        public string Category  { get; set; } = "";
        public string Extension { get; set; } = "";
        public long   TotalSize { get; set; }
        public int    Count     { get; set; }
        public string Percent   { get; set; } = "0%";
    }

    // ── View-model for snapshot comparison rows ───────────────────────────
    public class CompareRow
    {
        public string FolderPath    { get; set; } = "";
        public long   SizeBefore    { get; set; }
        public long   SizeAfter     { get; set; }
        public long   Delta         => SizeAfter - SizeBefore;
        public string DeltaFormatted => Delta >= 0
            ? "+" + FolderItem.FormatBytes(Delta)
            : "-"  + FolderItem.FormatBytes(Math.Abs(Delta));
        public string Status        => SizeBefore == 0 ? "New" : SizeAfter == 0 ? "Deleted" : Delta > 0 ? "Grew" : Delta < 0 ? "Shrank" : "Same";
        public Brush  DeltaColor    => Delta > 0 ? Brushes.OrangeRed : Delta < 0 ? Brushes.SeaGreen : Brushes.Gray;
    }

    // ── Main Window ───────────────────────────────────────────────────────
    public partial class MainWindow : Window
    {
        // ─── State ────────────────────────────────────────────────────────
        private DbContextOptions<FolderDiagDbContext> _dbOpts;
        private CancellationTokenSource? _scanCts;
        private string _currentPath = "";
        private readonly List<string> _navHistory = new();
        private int _navIndex = -1;
        private readonly List<string> _pinnedPaths = new();

        // Observable collections bound to grids
        private readonly ObservableCollection<FolderItem>   _folderItems   = new();
        private readonly ObservableCollection<BrowserEntry> _browserItems  = new();
        private readonly ObservableCollection<FileItem>     _reportItems   = new();
        private readonly ObservableCollection<ClassifyRow>  _classifyItems = new();
        private readonly ObservableCollection<FileItem>     _searchItems   = new();
        private readonly ObservableCollection<ScanSnapshot> _snapshots     = new();
        private readonly ObservableCollection<CompareRow>   _compareItems  = new();

        // Chart state
        private enum ChartType { Bar, Pie, Treemap }
        private ChartType _chartType = ChartType.Bar;

        // ─── Constructor ──────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();

            _dbOpts = new DbContextOptionsBuilder<FolderDiagDbContext>()
                .UseSqlite("Data Source=folderdiag.db")
                .Options;

            EnsureDb();
            BindGrids();
            LoadNavigationPane();
            LoadQuickAccess();
            LoadSnapshots();
            LoadDiskSpacePanel();

            // Load pinned paths from registry/file
            LoadPinnedPaths();

            // Navigate to user's home on start
            NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        // ─── DB Setup ─────────────────────────────────────────────────────
        private void EnsureDb()
        {
            using var db = new FolderDiagDbContext(_dbOpts);
            db.Database.EnsureCreated();
        }

        // ─── Bind grids ───────────────────────────────────────────────────
        private void BindGrids()
        {
            FoldersGrid.ItemsSource   = _folderItems;
            FileBrowserGrid.ItemsSource = _browserItems;
            ReportsGrid.ItemsSource   = _reportItems;
            ClassifyGrid.ItemsSource  = _classifyItems;
            SearchGrid.ItemsSource    = _searchItems;
            SnapshotsGrid.ItemsSource = _snapshots;
            CompareGrid.ItemsSource   = _compareItems;
        }

        // ─── Navigation Pane (Drives, Quick Access, Cloud, Network) ───────
        private void LoadNavigationPane()
        {
            LoadDrives();
            LoadCloudStorage();
            LoadNetworkDrives();
        }

        private void LoadDrives()
        {
            DrivesTree.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                string icon = drive.DriveType switch
                {
                    DriveType.CDRom    => "💿",
                    DriveType.Network  => "🌐",
                    DriveType.Removable => "💾",
                    _                  => "💽"
                };

                string label = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"{icon}  {drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})"
                    : $"{icon}  Local Disk ({drive.Name.TrimEnd('\\')})";

                var tvi = new TreeViewItem
                {
                    Header = label,
                    Tag    = drive.RootDirectory.FullName
                };

                if (drive.IsReady)
                {
                    tvi.Items.Add(new TreeViewItem { Header = "Loading…" });
                    tvi.Expanded += NavTreeItem_Expanded;
                }

                DrivesTree.Items.Add(tvi);
            }
        }

        private void LoadCloudStorage()
        {
            CloudTree.Items.Clear();

            // OneDrive — standard locations
            var oneDrivePaths = new[]
            {
                Environment.GetEnvironmentVariable("OneDrive"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive - Personal"),
            };

            foreach (var p in oneDrivePaths.Distinct())
            {
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                {
                    AddCloudNode("☁  OneDrive", p);
                    break;
                }
            }

            // Google Drive (DriveFS or Backup & Sync)
            var googlePaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive Stream"),
                @"G:\My Drive",
                @"H:\My Drive",
            };
            foreach (var p in googlePaths)
            {
                if (Directory.Exists(p)) { AddCloudNode("☁  Google Drive", p); break; }
            }

            // Dropbox
            var dropboxPaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox (Personal)"),
            };
            foreach (var p in dropboxPaths)
            {
                if (Directory.Exists(p)) { AddCloudNode("☁  Dropbox", p); break; }
            }

            // iCloud Drive
            var icloudPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "iCloudDrive");
            if (Directory.Exists(icloudPath)) AddCloudNode("☁  iCloud Drive", icloudPath);

            // Box
            var boxPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Box");
            if (Directory.Exists(boxPath)) AddCloudNode("☁  Box", boxPath);

            if (CloudTree.Items.Count == 0)
            {
                var none = new TreeViewItem { Header = "  No cloud storage detected", IsEnabled = false };
                CloudTree.Items.Add(none);
            }
        }

        private void AddCloudNode(string label, string path)
        {
            var tvi = new TreeViewItem { Header = label, Tag = path };
            tvi.Items.Add(new TreeViewItem { Header = "Loading…" });
            tvi.Expanded += NavTreeItem_Expanded;
            CloudTree.Items.Add(tvi);
        }

        private void LoadNetworkDrives()
        {
            NetworkTree.Items.Clear();
            var netDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Network);

            foreach (var d in netDrives)
            {
                var tvi = new TreeViewItem
                {
                    Header = $"🌐  {d.Name.TrimEnd('\\')}",
                    Tag    = d.RootDirectory.FullName
                };
                tvi.Items.Add(new TreeViewItem { Header = "Loading…" });
                tvi.Expanded += NavTreeItem_Expanded;
                NetworkTree.Items.Add(tvi);
            }

            if (!NetworkTree.Items.Cast<TreeViewItem>().Any())
            {
                NetworkTree.Items.Add(new TreeViewItem { Header = "  No network drives", IsEnabled = false });
            }
        }

        private void NavTreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem tvi) return;
            if (tvi.Items.Count == 1 && tvi.Items[0] is TreeViewItem placeholder && placeholder.Header?.ToString() == "Loading…")
            {
                tvi.Items.Clear();
                string? path = tvi.Tag as string;
                if (path == null) return;

                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        var sub = new TreeViewItem
                        {
                            Header = $"📁  {System.IO.Path.GetFileName(dir)}",
                            Tag    = dir
                        };
                        try
                        {
                            if (Directory.GetDirectories(dir).Length > 0)
                            {
                                sub.Items.Add(new TreeViewItem { Header = "Loading…" });
                                sub.Expanded += NavTreeItem_Expanded;
                            }
                        }
                        catch { }
                        tvi.Items.Add(sub);
                    }
                }
                catch { }
            }
        }

        private void LoadQuickAccess()
        {
            QuickAccessTree.Items.Clear();

            // Always-present Windows 11 special folders
            var special = new (string Icon, string Label, Environment.SpecialFolder Folder)[]
            {
                ("📌", "Desktop",    Environment.SpecialFolder.Desktop),
                ("⬇", "Downloads",  Environment.SpecialFolder.UserProfile),
                ("📄", "Documents",  Environment.SpecialFolder.MyDocuments),
                ("🖼", "Pictures",   Environment.SpecialFolder.MyPictures),
                ("🎵", "Music",      Environment.SpecialFolder.MyMusic),
                ("🎬", "Videos",     Environment.SpecialFolder.MyVideos),
            };

            foreach (var (icon, label, folder) in special)
            {
                string path = folder == Environment.SpecialFolder.UserProfile
                    ? System.IO.Path.Combine(Environment.GetFolderPath(folder), "Downloads")
                    : Environment.GetFolderPath(folder);

                var tvi = new TreeViewItem
                {
                    Header = $"{icon}  {label}",
                    Tag    = path
                };
                QuickAccessTree.Items.Add(tvi);
            }

            // User-pinned folders
            foreach (var p in _pinnedPaths.Where(Directory.Exists))
            {
                var tvi = new TreeViewItem
                {
                    Header = $"📌  {System.IO.Path.GetFileName(p)}",
                    Tag    = p,
                    ToolTip = p
                };
                tvi.ContextMenu = BuildPinContextMenu(p);
                QuickAccessTree.Items.Add(tvi);
            }
        }

        private ContextMenu BuildPinContextMenu(string path) => new()
        {
            Items =
            {
                new MenuItem
                {
                    Header = "📌  Unpin from Quick Access",
                    Tag    = path,
                    CommandParameter = path
                }
                    .Also(m => m.Click += (s, _) =>
                    {
                        _pinnedPaths.Remove(path);
                        SavePinnedPaths();
                        LoadQuickAccess();
                    })
            }
        };

        // ─── Pinned path persistence (JSON file next to exe) ──────────────
        private string PinnedFile =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pinned.txt");

        private void LoadPinnedPaths()
        {
            _pinnedPaths.Clear();
            if (File.Exists(PinnedFile))
                foreach (var line in File.ReadAllLines(PinnedFile))
                    if (!string.IsNullOrWhiteSpace(line) && !_pinnedPaths.Contains(line))
                        _pinnedPaths.Add(line);
        }

        private void SavePinnedPaths()
            => File.WriteAllLines(PinnedFile, _pinnedPaths);

        // ─── Navigation ───────────────────────────────────────────────────
        private void NavigateTo(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return;
            if (Directory.Exists(path)) path = path.TrimEnd('\\', '/');

            // History
            if (_navIndex < _navHistory.Count - 1)
                _navHistory.RemoveRange(_navIndex + 1, _navHistory.Count - _navIndex - 1);
            _navHistory.Add(path);
            _navIndex = _navHistory.Count - 1;

            BtnNavBack.IsEnabled    = _navIndex > 0;
            BtnNavForward.IsEnabled = false;

            _currentPath = path;
            TxtAddressBar.Text = path;
            BreadcrumbPath.Text = path;

            // Refresh active tab
            RefreshCurrentTab();
        }

        private void RefreshCurrentTab()
        {
            switch (MainTabCtrl.SelectedIndex)
            {
                case 0: LoadFolderSizes(_currentPath); break;
                case 1: LoadFileBrowser(_currentPath); break;
                case 2: LoadDiskSpacePanel();          break;
                default: break;
            }
        }

        // ─── Load Folder Sizes ─────────────────────────────────────────────
        private void LoadFolderSizes(string path)
        {
            _folderItems.Clear();
            if (string.IsNullOrEmpty(path)) return;

            using var db = new FolderDiagDbContext(_dbOpts);
            var rows = db.FolderItems
                .Where(f => f.ParentPath == path || f.Path == path)
                .OrderByDescending(f => f.SizeBytes)
                .ToList();

            long parentTotal = rows.Sum(r => r.SizeBytes);
            foreach (var r in rows)
            {
                r.PercentOfParent = parentTotal > 0 ? 100.0 * r.SizeBytes / parentTotal : 0;
                _folderItems.Add(r);
            }

            UpdateStatus();
            DrawChart();
        }

        // ─── Load File Browser ────────────────────────────────────────────
        private void LoadFileBrowser(string path)
        {
            _browserItems.Clear();
            BreadcrumbPath.Text = path;
            if (!Directory.Exists(path)) return;

            bool showHidden = ChkShowHidden.IsChecked == true;
            bool showSystem = ChkShowSystem.IsChecked == true;
            bool showExt    = ChkShowExtensions.IsChecked == true;

            try
            {
                // Subdirectories
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var di = new DirectoryInfo(dir);
                    if (!showHidden && (di.Attributes & FileAttributes.Hidden) != 0) continue;
                    if (!showSystem && (di.Attributes & FileAttributes.System) != 0) continue;

                    _browserItems.Add(new BrowserEntry
                    {
                        IsFolder  = true,
                        Name      = di.Name,
                        Path      = di.FullName,
                        Extension = "",
                        Modified  = di.LastWriteTime,
                        Created   = di.CreationTime,
                        Owner     = TryGetOwner(di.FullName)
                    });
                }

                // Files
                foreach (var file in Directory.GetFiles(path))
                {
                    var fi = new FileInfo(file);
                    if (!showHidden && (fi.Attributes & FileAttributes.Hidden) != 0) continue;
                    if (!showSystem && (fi.Attributes & FileAttributes.System) != 0) continue;

                    string name = showExt ? fi.Name : System.IO.Path.GetFileNameWithoutExtension(fi.Name);
                    _browserItems.Add(new BrowserEntry
                    {
                        IsFolder  = false,
                        Name      = name,
                        Path      = fi.FullName,
                        Extension = fi.Extension,
                        SizeBytes = fi.Length,
                        Modified  = fi.LastWriteTime,
                        Created   = fi.CreationTime,
                        Owner     = TryGetOwner(fi.FullName)
                    });
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                StatusTotal.Text = "Error: " + ex.Message;
            }
        }

        // ─── Disk Space Panel ─────────────────────────────────────────────
        private void LoadDiskSpacePanel()
        {
            DiskSpacePanel.Children.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                double usedPct = drive.TotalSize > 0
                    ? 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize
                    : 0;
                var brush = usedPct < 70 ? Brushes.SteelBlue
                          : usedPct < 85 ? Brushes.Orange
                          : Brushes.OrangeRed;

                string label = !string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')}) — {drive.DriveType}"
                    : $"Local Disk ({drive.Name.TrimEnd('\\')}) — {drive.DriveType}";

                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

                // Drive name + type
                panel.Children.Add(new TextBlock
                {
                    Text       = $"💽  {label}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = 14,
                    Margin     = new Thickness(0, 0, 0, 4)
                });

                // Usage bar
                var barBg = new Border
                {
                    Height          = 18,
                    Background      = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    CornerRadius    = new CornerRadius(4),
                    Margin          = new Thickness(0, 0, 0, 4)
                };
                var barFill = new Border
                {
                    Background   = brush,
                    CornerRadius = new CornerRadius(4),
                    Width        = 0,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                barBg.Child = barFill;
                panel.Children.Add(barBg);

                // Animate bar width after layout
                barBg.Loaded += (_, _) =>
                {
                    barFill.Width = Math.Max(4, barBg.ActualWidth * usedPct / 100.0);
                };

                // Stats row
                long used = drive.TotalSize - drive.AvailableFreeSpace;
                panel.Children.Add(new TextBlock
                {
                    Text       = $"Used: {FolderItem.FormatBytes(used)}   Free: {FolderItem.FormatBytes(drive.AvailableFreeSpace)}   Total: {FolderItem.FormatBytes(drive.TotalSize)}   ({usedPct:F1}% used)",
                    Foreground = (Brush)FindResource("Win11SubTextBrush"),
                    FontSize   = 12
                });

                // Format + filesystem
                panel.Children.Add(new TextBlock
                {
                    Text       = $"File System: {drive.DriveFormat}",
                    Foreground = (Brush)FindResource("Win11SubTextBrush"),
                    FontSize   = 11,
                    Margin     = new Thickness(0, 2, 0, 0)
                });

                var border = new Border
                {
                    Child           = panel,
                    BorderBrush     = (Brush)FindResource("Win11BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(6),
                    Padding         = new Thickness(14),
                    Margin          = new Thickness(0, 0, 0, 10)
                };
                DiskSpacePanel.Children.Add(border);
            }
        }

        // ─── Scanning ─────────────────────────────────────────────────────
        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath)) return;

            _scanCts = new CancellationTokenSource();
            BtnScan.IsEnabled  = false;
            BtnStop.IsEnabled  = true;
            ScanProgress.Visibility = Visibility.Visible;
            ScanProgress.IsIndeterminate = true;
            StatusTotal.Text   = $"Scanning {_currentPath}…";

            try
            {
                await Task.Run(() => PerformScan(_currentPath, _scanCts.Token));
                StatusTotal.Text = "Scan complete.";
            }
            catch (OperationCanceledException)
            {
                StatusTotal.Text = "Scan stopped.";
            }
            catch (Exception ex)
            {
                StatusTotal.Text = $"Scan error: {ex.Message}";
            }
            finally
            {
                BtnScan.IsEnabled  = true;
                BtnStop.IsEnabled  = false;
                ScanProgress.Visibility = Visibility.Collapsed;
                _scanCts?.Dispose();
                _scanCts = null;

                Dispatcher.Invoke(() =>
                {
                    LoadFolderSizes(_currentPath);
                    LoadFileBrowser(_currentPath);
                    LoadDiskSpacePanel();
                });
            }
        }

        private void PerformScan(string rootPath, CancellationToken ct)
        {
            using var db = new FolderDiagDbContext(_dbOpts);

            // Remove previous scan data for this root
            var old = db.FolderItems.Where(f => f.Path.StartsWith(rootPath)).ToList();
            db.FolderItems.RemoveRange(old);
            var oldFiles = db.FileItems.Where(f => f.FolderPath.StartsWith(rootPath)).ToList();
            db.FileItems.RemoveRange(oldFiles);
            db.SaveChanges();

            ScanDirectory(db, rootPath, null, 0, ct);
            db.SaveChanges();
        }

        private (long size, long alloc, int files, int folders) ScanDirectory(
            FolderDiagDbContext db, string path, string? parentPath, int depth, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            long totalSize = 0, totalAlloc = 0;
            int  totalFiles = 0, totalFolders = 0;

            // Scan files in this directory
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    long alloc = AllocatedSize(fi.Length);

                    totalSize  += fi.Length;
                    totalAlloc += alloc;
                    totalFiles++;

                    bool isTemp = fi.Extension.ToLower() is ".tmp" or ".temp" or ".bak" or ".log" or ".dmp";

                    db.FileItems.Add(new FileItem
                    {
                        Name        = fi.Name,
                        Path        = fi.FullName,
                        FolderPath  = path,
                        Extension   = fi.Extension.ToLower(),
                        SizeBytes   = fi.Length,
                        AllocatedBytes = alloc,
                        Modified    = fi.LastWriteTime,
                        Created     = fi.CreationTime,
                        Accessed    = fi.LastAccessTime,
                        Owner       = TryGetOwner(fi.FullName),
                        IsReadOnly  = fi.IsReadOnly,
                        IsHidden    = (fi.Attributes & FileAttributes.Hidden) != 0,
                        IsSystem    = (fi.Attributes & FileAttributes.System) != 0,
                        IsTemp      = isTemp,
                        ScannedAt   = DateTime.Now
                    });
                }
            }
            catch { }

            // Recurse subdirectories
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    totalFolders++;
                    var (childSize, childAlloc, childFiles, childFolders) =
                        ScanDirectory(db, dir, path, depth + 1, ct);
                    totalSize    += childSize;
                    totalAlloc   += childAlloc;
                    totalFiles   += childFiles;
                    totalFolders += childFolders;
                }
            }
            catch { }

            // Store this folder
            db.FolderItems.Add(new FolderItem
            {
                Path        = path,
                Name        = depth == 0 ? path : System.IO.Path.GetFileName(path),
                ParentPath  = parentPath,
                SizeBytes   = totalSize,
                AllocatedBytes = totalAlloc,
                FileCount   = totalFiles,
                FolderCount = totalFolders,
                LastModified = Directory.Exists(path) ? new DirectoryInfo(path).LastWriteTime : null,
                Created     = Directory.Exists(path) ? new DirectoryInfo(path).CreationTime   : null,
                Owner       = TryGetOwner(path),
                Depth       = depth,
                ScannedAt   = DateTime.Now
            });

            // Update status on UI thread
            Dispatcher.InvokeAsync(() => StatusTotal.Text = $"Scanning: {System.IO.Path.GetFileName(path)}…  ({totalFiles:N0} files)");

            return (totalSize, totalAlloc, totalFiles, totalFolders);
        }

        private static long AllocatedSize(long bytes, long clusterSize = 4096)
            => ((bytes + clusterSize - 1) / clusterSize) * clusterSize;

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
            StatusTotal.Text = "Stopping scan…";
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => RefreshCurrentTab();

        // ─── Charts ───────────────────────────────────────────────────────
        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            var items = _folderItems.Take(20).ToList();
            if (!items.Any()) return;

            switch (_chartType)
            {
                case ChartType.Bar:     DrawBarChart(items);  break;
                case ChartType.Pie:     DrawPieChart(items);  break;
                case ChartType.Treemap: DrawTreemap(items);   break;
            }
        }

        private static readonly Brush[] ChartPalette =
        {
            new SolidColorBrush(Color.FromRgb(0x00,0x78,0xD4)),
            new SolidColorBrush(Color.FromRgb(0x10,0x7C,0x10)),
            new SolidColorBrush(Color.FromRgb(0xD8,0x3B,0x01)),
            new SolidColorBrush(Color.FromRgb(0x8E,0x8C,0xD8)),
            new SolidColorBrush(Color.FromRgb(0x00,0x8B,0x8B)),
            new SolidColorBrush(Color.FromRgb(0xB1,0x46,0xC2)),
            new SolidColorBrush(Color.FromRgb(0xE8,0x74,0x23)),
            new SolidColorBrush(Color.FromRgb(0x00,0x7A,0xCC)),
            new SolidColorBrush(Color.FromRgb(0x49,0x7E,0x2A)),
            new SolidColorBrush(Color.FromRgb(0x7A,0x29,0x78)),
        };

        private void DrawBarChart(List<FolderItem> items)
        {
            ChartTitle.Text = "Bar Chart — Folder Sizes (Top 20)";
            if (!items.Any()) return;

            double canvasW = Math.Max(ChartCanvas.ActualWidth,  600);
            double canvasH = Math.Max(ChartCanvas.ActualHeight, 200);
            ChartCanvas.Width  = canvasW;
            ChartCanvas.Height = canvasH;

            double maxVal     = items.Max(i => (double)i.SizeBytes);
            double barWidth   = Math.Max(18, (canvasW - 60) / items.Count - 6);
            double maxBarH    = canvasH - 60;
            double x          = 40;

            for (int i = 0; i < items.Count; i++)
            {
                double barH = maxVal > 0 ? maxBarH * items[i].SizeBytes / maxVal : 0;
                Brush  fill = ChartPalette[i % ChartPalette.Length];

                var rect = new Rectangle
                {
                    Width  = barWidth,
                    Height = Math.Max(2, barH),
                    Fill   = fill,
                    Tag    = items[i],
                    Cursor = Cursors.Hand,
                    RadiusX = 3, RadiusY = 3
                };
                rect.MouseLeftButtonDown += ChartBar_Click;

                // Tooltip
                rect.ToolTip = $"{items[i].Name}\n{items[i].SizeFormatted}  ({items[i].PercentFormatted})";

                Canvas.SetLeft(rect, x);
                Canvas.SetTop (rect, canvasH - 30 - barH);
                ChartCanvas.Children.Add(rect);

                // Label
                var lbl = new TextBlock
                {
                    Text      = items[i].Name.Length > 10 ? items[i].Name[..10] + "…" : items[i].Name,
                    FontSize  = 9,
                    Width     = barWidth,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping  = TextWrapping.NoWrap,
                    Foreground    = Brushes.Black
                };
                Canvas.SetLeft(lbl, x);
                Canvas.SetTop (lbl, canvasH - 28);
                ChartCanvas.Children.Add(lbl);

                // Size above bar
                var sizeLbl = new TextBlock
                {
                    Text      = items[i].SizeFormatted,
                    FontSize  = 9,
                    Width     = barWidth + 10,
                    TextAlignment = TextAlignment.Center,
                    Foreground    = Brushes.DimGray
                };
                Canvas.SetLeft(sizeLbl, x - 5);
                Canvas.SetTop (sizeLbl, canvasH - 30 - barH - 16);
                ChartCanvas.Children.Add(sizeLbl);

                x += barWidth + 6;
            }
        }

        private void DrawPieChart(List<FolderItem> items)
        {
            ChartTitle.Text = "Pie Chart — Folder Sizes (Top 20)";
            double cx = 150, cy = 120, r = 110;
            long total = items.Sum(i => i.SizeBytes);
            if (total == 0) return;

            double startAngle = -Math.PI / 2;

            for (int i = 0; i < items.Count; i++)
            {
                double sweep = 2 * Math.PI * items[i].SizeBytes / total;
                if (sweep < 0.001) continue;

                Brush fill = ChartPalette[i % ChartPalette.Length];
                var path = BuildPieSlice(cx, cy, r, startAngle, startAngle + sweep, fill);
                path.ToolTip = $"{items[i].Name}\n{items[i].SizeFormatted} ({items[i].PercentFormatted})";
                path.Tag = items[i];
                path.MouseLeftButtonDown += ChartBar_Click;
                ChartCanvas.Children.Add(path);

                // Legend
                var legendRect = new Rectangle
                {
                    Width  = 12, Height = 12,
                    Fill   = fill,
                    RadiusX = 2, RadiusY = 2
                };
                Canvas.SetLeft(legendRect, 290);
                Canvas.SetTop (legendRect, 20 + i * 18);
                ChartCanvas.Children.Add(legendRect);

                string legText = (items[i].Name.Length > 22 ? items[i].Name[..22] + "…" : items[i].Name)
                               + $"  {items[i].SizeFormatted}";
                var legendLbl = new TextBlock { Text = legText, FontSize = 10, Foreground = Brushes.Black };
                Canvas.SetLeft(legendLbl, 308);
                Canvas.SetTop (legendLbl, 19 + i * 18);
                ChartCanvas.Children.Add(legendLbl);

                startAngle += sweep;
            }

            ChartCanvas.Width  = 550;
            ChartCanvas.Height = 280;
        }

        private static System.Windows.Shapes.Path BuildPieSlice(
            double cx, double cy, double r,
            double startAngle, double endAngle, Brush fill)
        {
            double x1 = cx + r * Math.Cos(startAngle);
            double y1 = cy + r * Math.Sin(startAngle);
            double x2 = cx + r * Math.Cos(endAngle);
            double y2 = cy + r * Math.Sin(endAngle);
            bool large = (endAngle - startAngle) > Math.PI;

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true, IsFilled = true };
            fig.Segments.Add(new LineSegment(new Point(x1, y1), true));
            fig.Segments.Add(new ArcSegment(new Point(x2, y2), new Size(r, r), 0, large, SweepDirection.Clockwise, true));
            geo.Figures.Add(fig);

            return new System.Windows.Shapes.Path
            {
                Data   = geo,
                Fill   = fill,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.Hand
            };
        }

        private void DrawTreemap(List<FolderItem> items)
        {
            ChartTitle.Text = "Treemap — Folder Sizes";
            double w = Math.Max(ChartCanvas.ActualWidth,  500);
            double h = Math.Max(ChartCanvas.ActualHeight, 200);
            ChartCanvas.Width  = w;
            ChartCanvas.Height = h;

            long total = items.Sum(i => i.SizeBytes);
            if (total == 0) return;

            double x = 0, remaining = w;
            foreach (var (item, idx) in items.Select((it, i) => (it, i)))
            {
                double wPct = (double)item.SizeBytes / total;
                double bw   = remaining * wPct;

                var rect = new Rectangle
                {
                    Width  = Math.Max(2, bw - 2),
                    Height = h,
                    Fill   = ChartPalette[idx % ChartPalette.Length],
                    Tag    = item,
                    Cursor = Cursors.Hand,
                    Opacity = 0.88
                };
                rect.ToolTip = $"{item.Name}\n{item.SizeFormatted} ({item.PercentFormatted})";
                rect.MouseLeftButtonDown += ChartBar_Click;
                Canvas.SetLeft(rect, x);
                Canvas.SetTop (rect, 0);
                ChartCanvas.Children.Add(rect);

                if (bw > 40)
                {
                    var lbl = new TextBlock
                    {
                        Text         = item.Name,
                        FontSize     = Math.Min(12, bw / 8),
                        Foreground   = Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                        Width        = bw - 8,
                        MaxHeight    = h - 4
                    };
                    Canvas.SetLeft(lbl, x + 4);
                    Canvas.SetTop (lbl, 4);
                    ChartCanvas.Children.Add(lbl);
                }

                x += bw;
                remaining -= bw;
            }
        }

        private void DrawClassificationPie(List<ClassifyRow> rows)
        {
            ClassifyPieCanvas.Children.Clear();
            if (!rows.Any()) return;
            long total = rows.Sum(r => r.TotalSize);
            if (total == 0) return;

            double cx = 120, cy = 120, r = 100;
            double angle = -Math.PI / 2;

            for (int i = 0; i < rows.Count; i++)
            {
                double sweep = 2 * Math.PI * rows[i].TotalSize / total;
                if (sweep < 0.001) continue;

                var path = BuildPieSlice(cx, cy, r, angle, angle + sweep, ChartPalette[i % ChartPalette.Length]);
                path.ToolTip = $"{rows[i].Category}: {rows[i].Percent}";
                ClassifyPieCanvas.Children.Add(path);

                // Legend
                var lb = new Rectangle { Width = 10, Height = 10, Fill = ChartPalette[i % ChartPalette.Length] };
                Canvas.SetLeft(lb, 250); Canvas.SetTop(lb, 20 + i * 16);
                ClassifyPieCanvas.Children.Add(lb);
                var lt = new TextBlock { Text = $"{rows[i].Category} {rows[i].Percent}", FontSize = 10 };
                Canvas.SetLeft(lt, 264); Canvas.SetTop(lt, 19 + i * 16);
                ClassifyPieCanvas.Children.Add(lt);

                angle += sweep;
            }
        }

        // ─── Chart click-through (drill down) ─────────────────────────────
        private void ChartBar_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is FolderItem fi)
            {
                NavigateTo(fi.Path);
                MainTabCtrl.SelectedIndex = 0;
            }
        }

        private void ChartCanvas_Click(object sender, MouseButtonEventArgs e) { }

        // ─── Status update ────────────────────────────────────────────────
        private void UpdateStatus()
        {
            long totalSize  = _folderItems.Sum(f => f.SizeBytes);
            int  totalFiles = _folderItems.Sum(f => f.FileCount);
            int  totalFolders = _folderItems.Sum(f => f.FolderCount);

            StatusTotal.Text     = $"{_folderItems.Count} folders in view";
            StatusFileCount.Text = $"   {totalFiles:N0} files  |  {totalFolders:N0} sub-folders";
            StatusSize.Text      = $"Total: {FolderItem.FormatBytes(totalSize)}";
        }

        // ─── Details panel update ─────────────────────────────────────────
        private void UpdateDetails(FolderItem item)
        {
            DetName.Text     = item.Name;
            DetSize.Text     = item.SizeFormatted;
            DetAlloc.Text    = item.AllocatedFormatted;
            DetFiles.Text    = item.FileCountFormatted;
            DetFolders.Text  = item.FolderCountFormatted;
            DetPct.Text      = item.PercentFormatted;
            DetModified.Text = item.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
            DetCreated.Text  = item.Created?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
            DetOwner.Text    = item.Owner ?? "—";
            DetPath.Text     = item.Path;

            // Animate size bar
            DetSizeBar.Width = 0;
            DetailsPanel.UpdateLayout();
            double maxW = ((Border)((Grid)((ScrollViewer)((Border)DetSizeBar.Parent).Parent).Content).Children[^1]).ActualWidth;
            DetSizeBar.Width = Math.Max(2, (DetailsPanel.ActualWidth - 24) * item.PercentOfParent / 100.0);
        }

        // ─── Reports ──────────────────────────────────────────────────────
        private void LoadReport(string type, int topN)
        {
            _reportItems.Clear();
            using var db = new FolderDiagDbContext(_dbOpts);

            IQueryable<FileItem> q = db.FileItems;
            if (!string.IsNullOrEmpty(_currentPath))
                q = q.Where(f => f.FolderPath.StartsWith(_currentPath));

            IEnumerable<FileItem> result = type switch
            {
                "Largest Files"              => q.OrderByDescending(f => f.SizeBytes),
                "Oldest Files"              => q.OrderBy(f => f.Modified),
                "Newest Files"              => q.OrderByDescending(f => f.Modified),
                "Temporary Files"           => q.Where(f => f.IsTemp).OrderByDescending(f => f.SizeBytes),
                "Duplicate Files (by size)" => q.GroupBy(f => f.SizeBytes)
                                                 .Where(g => g.Count() > 1)
                                                 .SelectMany(g => g)
                                                 .OrderByDescending(f => f.SizeBytes),
                "Files by Category"         => q.OrderByDescending(f => f.SizeBytes),
                "Files by Owner"            => q.OrderBy(f => f.Owner),
                "Files by Age Bucket"       => q.OrderBy(f => f.Modified),
                _                           => q.OrderByDescending(f => f.SizeBytes)
            };

            var list = topN > 0 ? result.Take(topN).ToList() : result.ToList();
            foreach (var f in list) _reportItems.Add(f);

            StatusTotal.Text = $"Report: {type} — {list.Count} items";
        }

        // ─── Classification ───────────────────────────────────────────────
        private void LoadClassification()
        {
            _classifyItems.Clear();
            using var db = new FolderDiagDbContext(_dbOpts);

            var q = db.FileItems.AsQueryable();
            if (!string.IsNullOrEmpty(_currentPath))
                q = q.Where(f => f.FolderPath.StartsWith(_currentPath));

            var groups = q.AsEnumerable()
                .GroupBy(f => FileItem.GetCategory(f.Extension))
                .Select(g => new
                {
                    Category  = g.Key,
                    TotalSize = g.Sum(f => f.SizeBytes),
                    Count     = g.Count()
                })
                .OrderByDescending(g => g.TotalSize)
                .ToList();

            long grand = groups.Sum(g => g.TotalSize);

            foreach (var g in groups)
            {
                _classifyItems.Add(new ClassifyRow
                {
                    Category  = g.Category,
                    Extension = "—",
                    TotalSize = g.TotalSize,
                    Count     = g.Count,
                    Percent   = grand > 0 ? $"{100.0 * g.TotalSize / grand:F1}%" : "0%"
                });
            }

            DrawClassificationPie(_classifyItems.ToList());
        }

        // ─── Search ───────────────────────────────────────────────────────
        private void ExecuteSearch()
        {
            _searchItems.Clear();
            using var db = new FolderDiagDbContext(_dbOpts);

            var q = db.FileItems.AsQueryable();
            if (!string.IsNullOrEmpty(_currentPath))
                q = q.Where(f => f.FolderPath.StartsWith(_currentPath));

            string name = SrchName.Text.Trim();
            string ext  = SrchExt.Text.Trim();
            string owner = SrchOwner.Text.Trim();

            if (!string.IsNullOrEmpty(name))
                q = q.Where(f => f.Name.Contains(name));
            if (!string.IsNullOrEmpty(ext))
                q = q.Where(f => f.Extension.Contains(ext));
            if (!string.IsNullOrEmpty(owner))
                q = q.Where(f => f.Owner != null && f.Owner.Contains(owner));

            string cat = (SrchCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            if (cat != "All")
                q = q.Where(f => FileItem.GetCategory(f.Extension) == cat);

            // Min size
            long minBytes = (SrchMinSize.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "> 1 MB"   => 1_048_576,
                "> 10 MB"  => 10_485_760,
                "> 100 MB" => 104_857_600,
                "> 1 GB"   => 1_073_741_824,
                _          => 0
            };
            if (minBytes > 0) q = q.Where(f => f.SizeBytes >= minBytes);

            // Age filter
            string age = (SrchAge.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Any time";
            DateTime? cutoff = age switch
            {
                "Today"         => DateTime.Today,
                "Last 7 days"   => DateTime.Now.AddDays(-7),
                "Last 30 days"  => DateTime.Now.AddDays(-30),
                "Last 90 days"  => DateTime.Now.AddDays(-90),
                "Last year"     => DateTime.Now.AddYears(-1),
                "Over 1 year ago" => null,
                _ => null
            };

            if (age == "Over 1 year ago")
                q = q.Where(f => f.Modified < DateTime.Now.AddYears(-1));
            else if (cutoff.HasValue)
                q = q.Where(f => f.Modified >= cutoff.Value);

            foreach (var f in q.OrderByDescending(f => f.SizeBytes).Take(500).ToList())
                _searchItems.Add(f);

            MainTabCtrl.SelectedIndex = 4;
            StatusTotal.Text = $"Search returned {_searchItems.Count} results";
        }

        // ─── Snapshots ────────────────────────────────────────────────────
        private void LoadSnapshots()
        {
            _snapshots.Clear();
            using var db = new FolderDiagDbContext(_dbOpts);
            foreach (var s in db.ScanSnapshots.OrderByDescending(s => s.CreatedAt).ToList())
                _snapshots.Add(s);

            CmbSnap1.ItemsSource = CmbSnap2.ItemsSource = _snapshots.Select(s => s.Name).ToList();
        }

        private void SaveSnapshot(string name)
        {
            using var db = new FolderDiagDbContext(_dbOpts);
            long total  = db.FolderItems.Where(f => f.Path.StartsWith(_currentPath)).Sum(f => f.SizeBytes);
            int  files  = db.FileItems.Where(f => f.FolderPath.StartsWith(_currentPath)).Count();
            int  folders = db.FolderItems.Where(f => f.Path.StartsWith(_currentPath)).Count();

            db.ScanSnapshots.Add(new ScanSnapshot
            {
                Name           = name,
                RootPath       = _currentPath,
                CreatedAt      = DateTime.Now,
                TotalSizeBytes = total,
                TotalFiles     = files,
                TotalFolders   = folders
            });
            db.SaveChanges();
            LoadSnapshots();
        }

        // ─── Export ───────────────────────────────────────────────────────
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "folderdiag_export.csv" };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Name,Path,Size (Bytes),Size (Human),Files,Folders,% of Parent,Last Modified,Owner");
            foreach (var f in _folderItems)
                sb.AppendLine($"\"{f.Name}\",\"{f.Path}\",{f.SizeBytes},\"{f.SizeFormatted}\",{f.FileCount},{f.FolderCount},{f.PercentOfParent:F2},\"{f.LastModified:yyyy-MM-dd HH:mm:ss}\",\"{f.Owner}\"");

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            StatusTotal.Text = $"Exported CSV → {dlg.FileName}";
        }

        private void BtnExportXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "XML|*.xml", FileName = "folderdiag_export.xml" };
            if (dlg.ShowDialog() != true) return;

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement("FolderReport",
                    new XAttribute("GeneratedAt", DateTime.Now),
                    new XAttribute("Root", _currentPath),
                    _folderItems.Select(f =>
                        new XElement("Folder",
                            new XAttribute("Name",         f.Name),
                            new XAttribute("Path",         f.Path),
                            new XAttribute("SizeBytes",    f.SizeBytes),
                            new XAttribute("SizeHuman",    f.SizeFormatted),
                            new XAttribute("Files",        f.FileCount),
                            new XAttribute("SubFolders",   f.FolderCount),
                            new XAttribute("PctOfParent",  f.PercentOfParent.ToString("F2")),
                            new XAttribute("LastModified", f.LastModified?.ToString("o") ?? ""),
                            new XAttribute("Owner",        f.Owner ?? "")))));
            doc.Save(dlg.FileName);
            StatusTotal.Text = $"Exported XML → {dlg.FileName}";
        }

        private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "HTML|*.html", FileName = "folderdiag_report.html" };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
<title>FolderDiag Report</title>
<style>
body{font-family:Segoe UI,sans-serif;background:#f3f3f3;margin:0;padding:20px}
h1{color:#0078d4}table{width:100%;border-collapse:collapse;background:#fff;border-radius:8px;overflow:hidden}
th{background:#0078d4;color:#fff;padding:10px 12px;text-align:left}
td{padding:8px 12px;border-bottom:1px solid #e0e0e0}
tr:hover td{background:#cce4f7}.bar{height:12px;background:#0078d4;border-radius:3px}
</style></head><body>");
            sb.Append($"<h1>FolderDiag Report</h1><p>Root: <strong>{_currentPath}</strong> — Generated: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            sb.Append("<table><thead><tr><th>Name</th><th>Size</th><th>Bar</th><th>%</th><th>Files</th><th>Folders</th><th>Last Modified</th><th>Owner</th></tr></thead><tbody>");

            foreach (var f in _folderItems)
            {
                int barW = (int)Math.Min(200, f.PercentOfParent * 2);
                sb.Append($"<tr><td>{f.Name}</td><td>{f.SizeFormatted}</td><td><div class='bar' style='width:{barW}px'></div></td><td>{f.PercentFormatted}</td><td>{f.FileCountFormatted}</td><td>{f.FolderCountFormatted}</td><td>{f.LastModified:yyyy-MM-dd HH:mm}</td><td>{f.Owner}</td></tr>");
            }

            sb.Append("</tbody></table></body></html>");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            StatusTotal.Text = $"Exported HTML → {dlg.FileName}";
        }

        // ─── File operations ──────────────────────────────────────────────
        private string? GetSelectedFolderPath()
        {
            if (FoldersGrid.SelectedItem is FolderItem fi) return fi.Path;
            if (FileBrowserGrid.SelectedItem is BrowserEntry be) return be.Path;
            return null;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopyOrMove(false);
        private void BtnMove_Click(object sender, RoutedEventArgs e) => CopyOrMove(true);

        private void CopyOrMove(bool move)
        {
            string? src = GetSelectedFolderPath();
            if (src == null) { MessageBox.Show("Select a folder first."); return; }

            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = move ? "Move to…" : "Copy to…"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string dest = System.IO.Path.Combine(dlg.SelectedPath, System.IO.Path.GetFileName(src));
            try
            {
                if (move) Directory.Move(src, dest);
                else      CopyDirectory(src, dest);
                StatusTotal.Text = $"{(move ? "Moved" : "Copied")} → {dest}";
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(dir)));
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path == null) return;
            if (MessageBox.Show($"Delete '{path}'?\nThis sends it to the Recycle Bin.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                StatusTotal.Text = $"Deleted: {path}";
                RefreshCurrentTab();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnZip_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path == null) return;
            var dlg = new SaveFileDialog { Filter = "ZIP|*.zip", FileName = System.IO.Path.GetFileName(path) + ".zip" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                ZipFile.CreateFromDirectory(path, dlg.FileName, CompressionLevel.Optimal, true);
                StatusTotal.Text = $"Zipped → {dlg.FileName}";
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        // ─── Quick Access pinning ─────────────────────────────────────────
        private void PinCurrentPath()
        {
            if (string.IsNullOrEmpty(_currentPath)) return;
            if (!_pinnedPaths.Contains(_currentPath))
            {
                _pinnedPaths.Add(_currentPath);
                SavePinnedPaths();
                LoadQuickAccess();
                StatusTotal.Text = $"Pinned: {_currentPath}";
            }
        }

        // ─── Helper: get file/folder owner ───────────────────────────────
        private static string? TryGetOwner(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var sec = new DirectoryInfo(path).GetAccessControl();
                    return sec.GetOwner(typeof(NTAccount))?.ToString();
                }
                var fs = new FileInfo(path).GetAccessControl();
                return fs.GetOwner(typeof(NTAccount))?.ToString();
            }
            catch { return null; }
        }

        // ─── Event Handlers ───────────────────────────────────────────────

        // Navigation pane tree selection
        private void NavTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is string path && Directory.Exists(path))
                NavigateTo(path);
        }

        // Special folder clicks (Desktop, Downloads, etc.)
        private void NavSpecialFolder_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem tvi)
            {
                string path = tvi.Tag?.ToString() switch
                {
                    "Desktop"   => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Downloads" => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Pictures"  => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Music"     => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "Videos"    => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    _           => ""
                };
                if (!string.IsNullOrEmpty(path)) NavigateTo(path);
            }
        }

        // Section collapse toggles
        private void BtnToggleSectionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var (section, ico) = btn.Tag?.ToString() switch
            {
                "QuickAccess" => (QuickAccessSection, IcoQuickAccess),
                "ThisPC"      => (ThisPCSection, IcoThisPC),
                "Network"     => (NetworkSection, IcoNetwork),
                "Cloud"       => (CloudSection, IcoCloud),
                _             => ((StackPanel?)null, (TextBlock?)null)
            };
            if (section == null || ico == null) return;
            bool collapsed = section.Visibility == Visibility.Collapsed;
            section.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            ico.Text = collapsed ? "▾" : "▸";
        }

        // Main tab changed — refresh content
        private void MainTabCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            switch (MainTabCtrl.SelectedIndex)
            {
                case 0: LoadFolderSizes(_currentPath); break;
                case 1: LoadFileBrowser(_currentPath); break;
                case 2: LoadDiskSpacePanel();          break;
                case 4: LoadClassification();          break;
                case 6: LoadSnapshots();               break;
            }
        }

        // Address bar Enter key
        private void TxtAddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string path = TxtAddressBar.Text.Trim();
                if (Directory.Exists(path)) NavigateTo(path);
                else StatusTotal.Text = $"Path not found: {path}";
            }
        }

        // Live search filter
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = TxtSearch.Text.Trim().ToLowerInvariant();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrEmpty(term)) { LoadFolderSizes(_currentPath); return; }

            var filtered = _folderItems.Where(f =>
                f.Name.ToLowerInvariant().Contains(term) ||
                f.Path.ToLowerInvariant().Contains(term)).ToList();

            _folderItems.Clear();
            foreach (var f in filtered) _folderItems.Add(f);
        }

        // Nav arrows
        private void BtnNavBack_Click(object sender, RoutedEventArgs e)
        {
            if (_navIndex > 0)
            {
                _navIndex--;
                _currentPath = _navHistory[_navIndex];
                TxtAddressBar.Text = _currentPath;
                BtnNavBack.IsEnabled    = _navIndex > 0;
                BtnNavForward.IsEnabled = true;
                RefreshCurrentTab();
            }
        }
        private void BtnNavForward_Click(object sender, RoutedEventArgs e)
        {
            if (_navIndex < _navHistory.Count - 1)
            {
                _navIndex++;
                _currentPath = _navHistory[_navIndex];
                TxtAddressBar.Text = _currentPath;
                BtnNavBack.IsEnabled    = true;
                BtnNavForward.IsEnabled = _navIndex < _navHistory.Count - 1;
                RefreshCurrentTab();
            }
        }
        private void BtnNavUp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath)) return;
            string? parent = Directory.GetParent(_currentPath)?.FullName;
            if (!string.IsNullOrEmpty(parent)) NavigateTo(parent);
        }

        // Grid selections
        private void FoldersGrid_SelectionChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FoldersGrid.SelectedItem is FolderItem f)
                UpdateDetails(f);
        }
        private void FileBrowserGrid_SelectionChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FileBrowserGrid.SelectedItem is BrowserEntry be && !be.IsFolder)
                StatusTotal.Text = $"{be.Name}  —  {FolderItem.FormatBytes(be.SizeBytes)}";
            else if (FileBrowserGrid.SelectedItem is BrowserEntry beDir && beDir.IsFolder)
                NavigateTo(beDir.Path);
        }

        // Ribbon sort/filter
        private void CmbSort_Changed(object sender, SelectionChangedEventArgs e)          => LoadFolderSizes(_currentPath);
        private void CmbMinSize_Changed(object sender, SelectionChangedEventArgs e)        => LoadFolderSizes(_currentPath);
        private void CmbDepth_Changed(object sender, SelectionChangedEventArgs e)          => LoadFolderSizes(_currentPath);

        // View options
        private void ChkShowHidden_Changed(object sender, RoutedEventArgs e)   => LoadFileBrowser(_currentPath);
        private void ChkShowSystem_Changed(object sender, RoutedEventArgs e)   => LoadFileBrowser(_currentPath);
        private void ChkShowExtensions_Changed(object sender, RoutedEventArgs e) => LoadFileBrowser(_currentPath);

        // View layout buttons (placeholder — tiles view would use WrapPanel)
        private void BtnViewDetails_Click(object sender, RoutedEventArgs e) { /* already default */ }
        private void BtnViewList_Click(object sender, RoutedEventArgs e)    { /* TODO: switch to list */ }
        private void BtnViewTiles_Click(object sender, RoutedEventArgs e)   { /* TODO: switch to tiles */ }

        // Chart type buttons
        private void BtnChartBar_Click(object sender, RoutedEventArgs e)     { _chartType = ChartType.Bar;     DrawChart(); ChartTitle.Text = "Bar Chart — Folder Sizes"; }
        private void BtnChartPie_Click(object sender, RoutedEventArgs e)     { _chartType = ChartType.Pie;     DrawChart(); }
        private void BtnChartTreemap_Click(object sender, RoutedEventArgs e) { _chartType = ChartType.Treemap; DrawChart(); }

        // Report tab
        private void CmbReportType_Changed(object sender, SelectionChangedEventArgs e) { }
        private void CmbTopN_Changed(object sender, SelectionChangedEventArgs e)       { }
        private void BtnRunReport_Click(object sender, RoutedEventArgs e)
        {
            string type = (CmbReportType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Largest Files";
            int top = (CmbTopN.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "100" => 100, "250" => 250, "500" => 500, "All" => 0, _ => 50
            };
            LoadReport(type, top);
            MainTabCtrl.SelectedIndex = 3;
        }

        // Report ribbon shortcuts
        private void BtnReportLargest_Click(object sender, RoutedEventArgs e)    { CmbReportType.SelectedIndex = 0; BtnRunReport_Click(sender, e); }
        private void BtnReportOldest_Click(object sender, RoutedEventArgs e)      { CmbReportType.SelectedIndex = 1; BtnRunReport_Click(sender, e); }
        private void BtnReportTemp_Click(object sender, RoutedEventArgs e)        { CmbReportType.SelectedIndex = 3; BtnRunReport_Click(sender, e); }
        private void BtnReportDuplicates_Click(object sender, RoutedEventArgs e)  { CmbReportType.SelectedIndex = 4; BtnRunReport_Click(sender, e); }
        private void BtnClassifyType_Click(object sender, RoutedEventArgs e)      { LoadClassification(); MainTabCtrl.SelectedIndex = 4; }
        private void BtnClassifyAge_Click(object sender, RoutedEventArgs e)       { LoadClassification(); MainTabCtrl.SelectedIndex = 4; }
        private void BtnClassifyOwner_Click(object sender, RoutedEventArgs e)     { LoadClassification(); MainTabCtrl.SelectedIndex = 4; }

        // Search
        private void BtnSearch_Click(object sender, RoutedEventArgs e) => ExecuteSearch();

        // Snapshot
        private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for this snapshot:", "Save Snapshot",
                $"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrWhiteSpace(name))
            {
                SaveSnapshot(name);
                StatusTotal.Text = $"Snapshot saved: {name}";
            }
        }
        private void BtnDeleteSnapshot_Click(object sender, RoutedEventArgs e)
        {
            if (SnapshotsGrid.SelectedItem is not ScanSnapshot snap) return;
            using var db = new FolderDiagDbContext(_dbOpts);
            var entity = db.ScanSnapshots.Find(snap.Id);
            if (entity != null) { db.ScanSnapshots.Remove(entity); db.SaveChanges(); }
            LoadSnapshots();
        }
        private void SnapshotsGrid_SelectionChanged(object sender, SelectedCellsChangedEventArgs e) { }
        private void BtnCompare_Click(object sender, RoutedEventArgs e)   { MainTabCtrl.SelectedIndex = 6; }
        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Scheduling: Use Task Scheduler to run FolderDiag with --scan <path> argument.\n\nExample:\n  FolderDiag.exe --scan C:\\", "Schedule Scan");

        private void BtnRunCompare_Click(object sender, RoutedEventArgs e)
        {
            _compareItems.Clear();
            string? n1 = CmbSnap1.SelectedItem?.ToString();
            string? n2 = CmbSnap2.SelectedItem?.ToString();
            if (n1 == null || n2 == null) return;

            using var db = new FolderDiagDbContext(_dbOpts);
            var snap1 = db.ScanSnapshots.FirstOrDefault(s => s.Name == n1);
            var snap2 = db.ScanSnapshots.FirstOrDefault(s => s.Name == n2);
            if (snap1 == null || snap2 == null) return;

            // Compare top-level folder sizes at the time of each snapshot
            // (simplified — compares current folder data between two root paths)
            var folders1 = db.FolderItems.Where(f => f.Path.StartsWith(snap1.RootPath)).ToList();
            var folders2 = db.FolderItems.Where(f => f.Path.StartsWith(snap2.RootPath)).ToList();

            var allPaths = folders1.Select(f => f.Path)
                .Union(folders2.Select(f => f.Path))
                .Distinct();

            foreach (var path in allPaths)
            {
                long before = folders1.FirstOrDefault(f => f.Path == path)?.SizeBytes ?? 0;
                long after  = folders2.FirstOrDefault(f => f.Path == path)?.SizeBytes ?? 0;
                if (before == after) continue;

                _compareItems.Add(new CompareRow
                {
                    FolderPath = path,
                    SizeBefore = before,
                    SizeAfter  = after
                });
            }
        }

        // Context menu — folder grid
        private void CtxOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path != null)
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
        }
        private void CtxScanFolder_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path != null) { NavigateTo(path); BtnScan_Click(sender, e); }
        }
        private void CtxPinQuickAccess_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath() ?? _currentPath;
            if (string.IsNullOrEmpty(path)) return;
            if (!_pinnedPaths.Contains(path))
            {
                _pinnedPaths.Add(path);
                SavePinnedPaths();
                LoadQuickAccess();
                StatusTotal.Text = $"Pinned to Quick Access: {System.IO.Path.GetFileName(path)}";
            }
            else StatusTotal.Text = "Already pinned.";
        }
        private void CtxCopyPath_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path != null) Clipboard.SetText(path);
        }
        private void CtxExportCsv_Click(object sender, RoutedEventArgs e)  => BtnExportCsv_Click(sender, e);
        private void CtxMove_Click(object sender, RoutedEventArgs e)        => CopyOrMove(true);
        private void CtxCopy_Click(object sender, RoutedEventArgs e)        => CopyOrMove(false);
        private void CtxZip_Click(object sender, RoutedEventArgs e)         => BtnZip_Click(sender, e);
        private void CtxDelete_Click(object sender, RoutedEventArgs e)      => BtnDelete_Click(sender, e);
        private void CtxProperties_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedFolderPath();
            if (path == null) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                    { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        // Context menu — file browser
        private void CtxBrowserOpen_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowserGrid.SelectedItem is BrowserEntry be)
            {
                if (be.IsFolder) NavigateTo(be.Path);
                else
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(be.Path) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show("Cannot open: " + ex.Message); }
                }
            }
        }

        // Context menu — file reports / search
        private void CtxOpenContaining_Click(object sender, RoutedEventArgs e)
        {
            FileItem? fi = ReportsGrid.SelectedItem as FileItem ?? SearchGrid.SelectedItem as FileItem;
            if (fi != null)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fi.Path}\"");
        }
        private void CtxDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            FileItem? fi = ReportsGrid.SelectedItem as FileItem ?? SearchGrid.SelectedItem as FileItem;
            if (fi == null) return;
            if (MessageBox.Show($"Delete '{fi.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;
            try
            {
                File.Delete(fi.Path);
                StatusTotal.Text = $"Deleted: {fi.Path}";
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }
    }

    // ── Extension helper ──────────────────────────────────────────────────
    internal static class Ext
    {
        public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}
