using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
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
using FolderDiag.Core.Scanning;
using FolderDiag.Wpf.Scanning;
using FolderDiag.Wpf.Settings;
using FolderDiag.Wpf.Theming;
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
        /// <summary>The ".." row that goes up to the parent folder.</summary>
        public bool   IsParentLink { get; set; }
        /// <summary>Like Explorer: folders show no size.</summary>
        public string SizeText   => IsFolder || IsParentLink ? "" : FolderItem.FormatBytes(SizeBytes);
        public string Icon       => IsFolder ? "📁" : GetFileIcon(Extension);
        /// <summary>The Windows shell icon (same as File Explorer).</summary>
        public ImageSource? IconImage => IsParentLink ? FolderDiag.Wpf.Theming.ShellIcons.Folder
                                                      : FolderDiag.Wpf.Theming.ShellIcons.Get(Path, IsFolder);

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
        private Task? _scanTask;
        // Folders we've already auto-scanned this session (prevents re-scan loops on empty/denied folders)
        private readonly HashSet<string> _autoScanAttempted = new(StringComparer.OrdinalIgnoreCase);
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
            ThemeManager.Register(this);
            // Start with File Explorer's own View › Show settings
            ChkShowHidden.IsChecked     = ExplorerSettings.ShowHidden;
            ChkShowExtensions.IsChecked = ExplorerSettings.ShowExtensions;
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (_, _) =>
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _currentWatcher?.Dispose();
                foreach (var w in _navWatchers.Values) w.Dispose();
                _navWatchers.Clear();
            };

            // Store the database in %LocalAppData% so it is writable when installed
            // under Program Files (Inno Setup) or inside an MSIX package.
            System.IO.Directory.CreateDirectory(AppDataDir);
            var dbPath = System.IO.Path.Combine(AppDataDir, "folderdiag.db");

            _dbOpts = new DbContextOptionsBuilder<FolderDiagDbContext>()
                .UseSqlite($"Data Source={dbPath}")
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
            ScanStore.Initialize(db); // creates tables + indexes used by folder lookups
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
        // ─── Navigation item header: real Windows icon + label ───────────
        private object NavHeader(string text, ImageSource? icon)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (icon != null)
            {
                var img = new Image { Source = icon, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                img.SetResourceReference(WidthProperty, "Fs16");   // icons grow with the font size setting
                img.SetResourceReference(HeightProperty, "Fs16");
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                sp.Children.Add(img);
            }
            else
            {
                var glyph = new TextBlock { Text = "\uE8B7", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                glyph.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
                glyph.SetResourceReference(TextBlock.ForegroundProperty, "IconGold");
                sp.Children.Add(glyph);
            }
            sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            System.Windows.Automation.AutomationProperties.SetName(sp, text); // screen readers read the label
            return sp;
        }

        private static string ResolveSpecialFolder(string? tag) => tag switch
        {
            "Desktop"   => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Downloads" => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Pictures"  => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Music"     => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "Videos"    => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            _           => ""
        };

        private void LoadNavigationPane()
        {
            // "This PC" special folders are declared in XAML; give them their Windows icons
            foreach (var tvi in new[] { NavDesktop, NavDownloads, NavDocuments, NavPictures, NavMusic, NavVideos })
            {
                string tag = tvi.Tag?.ToString() ?? "";
                tvi.Header = NavHeader(tag, ShellIcons.Get(ResolveSpecialFolder(tag), true));
            }

            LoadDrives();
            LoadCloudStorage();
            LoadNetworkDrives();
        }

        private void LoadDrives()
        {
            DrivesTree.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {

                string label = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})"
                    : $"{DefaultDriveName(drive.DriveType)} ({drive.Name.TrimEnd('\\')})";

                var tvi = new TreeViewItem
                {
                    Header = NavHeader(label, ShellIcons.Get(drive.RootDirectory.FullName, true)),
                    Tag    = drive.RootDirectory.FullName
                };

                if (drive.IsReady)
                {
                    tvi.Items.Add(new TreeViewItem { Header = LoadingHeader });
                    tvi.Expanded += NavTreeItem_Expanded;
                    tvi.Collapsed += NavTreeItem_Collapsed;
                }

                DrivesTree.Items.Add(tvi);
            }
        }

        // Windows 11 Explorer names for unlabelled drives
        private static string DefaultDriveName(DriveType t) => t switch
        {
            DriveType.CDRom     => "DVD Drive",
            DriveType.Removable => "USB Drive",
            DriveType.Network   => "Network Drive",
            DriveType.Ram       => "RAM Disk",
            _                   => "Local Disk",
        };

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
                    AddCloudNode("OneDrive", p);
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
                if (Directory.Exists(p)) { AddCloudNode("Google Drive", p); break; }
            }

            // Dropbox
            var dropboxPaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox (Personal)"),
            };
            foreach (var p in dropboxPaths)
            {
                if (Directory.Exists(p)) { AddCloudNode("Dropbox", p); break; }
            }

            // iCloud Drive
            var icloudPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "iCloudDrive");
            if (Directory.Exists(icloudPath)) AddCloudNode("iCloud Drive", icloudPath);

            // Box
            var boxPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Box");
            if (Directory.Exists(boxPath)) AddCloudNode("Box", boxPath);

            if (CloudTree.Items.Count == 0)
            {
                var none = new TreeViewItem { Header = "  No cloud storage detected", IsEnabled = false };
                CloudTree.Items.Add(none);
            }
        }

        private void AddCloudNode(string label, string path)
        {
            // Use the name Windows shows (e.g. "OneDrive - Personal"), falling back to the provider name
            string shown = ShellIcons.DisplayName(path) is { Length: > 0 } n ? n : label;
            var tvi = new TreeViewItem { Header = NavHeader(shown, ShellIcons.Get(path, true)), Tag = path, ToolTip = path };
            tvi.Items.Add(new TreeViewItem { Header = LoadingHeader });
            tvi.Expanded += NavTreeItem_Expanded;
            tvi.Collapsed += NavTreeItem_Collapsed;
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
                    Header = NavHeader(d.Name.TrimEnd('\\'), ShellIcons.Get(d.RootDirectory.FullName, true)),
                    Tag    = d.RootDirectory.FullName
                };
                tvi.Items.Add(new TreeViewItem { Header = LoadingHeader });
                tvi.Expanded += NavTreeItem_Expanded;
                tvi.Collapsed += NavTreeItem_Collapsed;
                NetworkTree.Items.Add(tvi);
            }

            if (!NetworkTree.Items.Cast<TreeViewItem>().Any())
            {
                NetworkTree.Items.Add(new TreeViewItem { Header = "  No network drives", IsEnabled = false });
            }
        }

        // ─── Navigation tree: lazy children, Explorer names/order, live sync ───
        private const string LoadingHeader = "Loading…";
        private readonly Dictionary<TreeViewItem, FileSystemWatcher> _navWatchers = new();

        private void NavTreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            // Expanded bubbles up the tree: only handle the node that actually expanded
            if (sender is not TreeViewItem tvi || !ReferenceEquals(e.OriginalSource, tvi)) return;
            if (tvi.Tag is not string path) return;

            if (tvi.Items.Count == 1 && tvi.Items[0] is TreeViewItem { Header: string h } && h == LoadingHeader)
                tvi.Items.Clear();

            SyncNavChildren(tvi, path);   // also picks up changes made while it was collapsed
            WatchNavNode(tvi, path);
        }

        private void NavTreeItem_Collapsed(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem tvi || !ReferenceEquals(e.OriginalSource, tvi)) return;
            UnwatchNavNode(tvi);
        }

        /// <summary>
        /// Brings a node's children in line with the disk: removes deleted folders, adds new ones,
        /// refreshes display names, and keeps Explorer's order. Expanded sub-trees are preserved.
        /// </summary>
        private void SyncNavChildren(TreeViewItem tvi, string path)
        {
            bool showHidden = ChkShowHidden?.IsChecked == true;
            List<DirectoryInfo> dirs;
            try
            {
                dirs = new DirectoryInfo(path)
                    .EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })
                    // Like Explorer's navigation pane: hide hidden folders unless "Hidden items" is on
                    .Where(d => showHidden || (d.Attributes & FileAttributes.Hidden) == 0)
                    .ToList();
            }
            catch { return; }

            var existing = tvi.Items.OfType<TreeViewItem>()
                .Where(c => c.Tag is string)
                .ToDictionary(c => (string)c.Tag, StringComparer.OrdinalIgnoreCase);
            var current = new HashSet<string>(dirs.Select(d => d.FullName), StringComparer.OrdinalIgnoreCase);

            // Removed (or renamed away)
            foreach (var (p, child) in existing)
                if (!current.Contains(p)) { UnwatchSubtree(child); tvi.Items.Remove(child); }
            // Placeholder rows ("Loading…") no longer needed
            foreach (var ph in tvi.Items.OfType<TreeViewItem>().Where(c => c.Tag is null).ToList()) tvi.Items.Remove(ph);

            // Added / renamed to, plus refresh names of the rest
            var named = dirs.Select(d => (Dir: d, Name: ShellIcons.FolderDisplayName(d)))
                            .OrderBy(x => x.Name, ShellIcons.LogicalComparer).ToList();
            for (int i = 0; i < named.Count; i++)
            {
                var (d, name) = named[i];
                if (!existing.TryGetValue(d.FullName, out var node) || !tvi.Items.Contains(node))
                    node = CreateFolderNode(d, name);
                else
                    node.Header = NavHeader(name, ShellIcons.Get(d.FullName, true));

                // Keep Explorer order
                int at = tvi.Items.IndexOf(node);
                if (at != i)
                {
                    if (at >= 0) tvi.Items.RemoveAt(at);
                    tvi.Items.Insert(Math.Min(i, tvi.Items.Count), node);
                }
            }
        }

        private TreeViewItem CreateFolderNode(DirectoryInfo dir, string displayName)
        {
            var node = new TreeViewItem
            {
                Header  = NavHeader(displayName, ShellIcons.Get(dir.FullName, true)),
                Tag     = dir.FullName,
                ToolTip = displayName != dir.Name ? $"{dir.FullName}\n(shown as \"{displayName}\", like File Explorer)" : null,
            };
            try
            {
                // Cheap "has sub-folders?" check: stops at the first one instead of listing them all
                if (dir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true }).Any())
                    node.Items.Add(new TreeViewItem { Header = LoadingHeader });
            }
            catch { }
            node.Expanded  += NavTreeItem_Expanded;
            node.Collapsed += NavTreeItem_Collapsed;
            return node;
        }

        /// <summary>
        /// Watches an expanded folder so renames, new and deleted folders (including ones
        /// synced down by OneDrive from another device) show up without a manual refresh.
        /// </summary>
        private void WatchNavNode(TreeViewItem tvi, string path)
        {
            if (_navWatchers.ContainsKey(tvi)) return;
            try
            {
                var w = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.Attributes,
                    IncludeSubdirectories = false,
                };
                var debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                debounce.Tick += (_, _) =>
                {
                    debounce.Stop();
                    if (!_navWatchers.ContainsKey(tvi)) return;
                    ShellIcons.Invalidate(path);
                    SyncNavChildren(tvi, path);
                };
                void Changed(object? _, FileSystemEventArgs __) => Dispatcher.BeginInvoke(() => { debounce.Stop(); debounce.Start(); });
                w.Created += Changed; w.Deleted += Changed; w.Renamed += Changed; w.Changed += Changed;
                w.Error += (_, _) => Dispatcher.BeginInvoke(() => { debounce.Stop(); debounce.Start(); }); // buffer overflow: resync
                w.EnableRaisingEvents = true;
                _navWatchers[tvi] = w;
            }
            catch { /* e.g. some network shares don't support change notifications */ }
        }

        private void UnwatchNavNode(TreeViewItem tvi)
        {
            if (_navWatchers.Remove(tvi, out var w)) w.Dispose();
        }

        private void UnwatchSubtree(TreeViewItem tvi)
        {
            UnwatchNavNode(tvi);
            foreach (var child in tvi.Items.OfType<TreeViewItem>()) UnwatchSubtree(child);
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

            foreach (var (_, label, folder) in special)
            {
                string path = folder == Environment.SpecialFolder.UserProfile
                    ? System.IO.Path.Combine(Environment.GetFolderPath(folder), "Downloads")
                    : Environment.GetFolderPath(folder);

                var tvi = new TreeViewItem
                {
                    Header = NavHeader(label, ShellIcons.Get(path, true)),
                    Tag    = path
                };
                QuickAccessTree.Items.Add(tvi);
            }

            // User-pinned folders
            var standard = new HashSet<string>(QuickAccessTree.Items.OfType<TreeViewItem>()
                .Select(t => t.Tag as string).OfType<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var p in _pinnedPaths.Where(Directory.Exists).Where(p => !standard.Contains(p.TrimEnd('\\'))))
            {
                var tvi = new TreeViewItem
                {
                    Header = NavHeader(ShellIcons.FolderDisplayName(new DirectoryInfo(p)), ShellIcons.Get(p, true)),
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

        // ─── Per-user app data (writable; never the install directory) ────
        private static readonly string AppDataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderDiag");

        // ─── Pinned path persistence ──────────────────────────────────────
        private static string PinnedFile => System.IO.Path.Combine(AppDataDir, "pinned.txt");

        private void LoadPinnedPaths()
        {
            _pinnedPaths.Clear();
            if (File.Exists(PinnedFile))
                foreach (var line in File.ReadAllLines(PinnedFile))
                    if (!string.IsNullOrWhiteSpace(line)
                        && System.IO.Path.IsPathFullyQualified(line)
                        && !_pinnedPaths.Contains(line))
                        _pinnedPaths.Add(line);
        }

        private void SavePinnedPaths()
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllLines(PinnedFile, _pinnedPaths);
        }

        // ─── Navigation ───────────────────────────────────────────────────
        private void NavigateTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!Directory.Exists(path))
            {
                // A file: show its folder with the file selected (like Explorer's "Open file location")
                if (File.Exists(path) && System.IO.Path.GetDirectoryName(path) is string folder)
                {
                    NavigateTo(folder);
                    SelectPath(path);
                }
                return;
            }
            path = NormalizePath(path);
            if (string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase) && _navHistory.Count > 0)
            {
                RefreshCurrentTab();   // same folder: just refresh, don't add a duplicate history entry
                return;
            }

            // History
            if (_navIndex < _navHistory.Count - 1)
                _navHistory.RemoveRange(_navIndex + 1, _navHistory.Count - _navIndex - 1);
            _navHistory.Add(path);
            _navIndex = _navHistory.Count - 1;
            ShowFolder(path);
        }

        /// <summary>Displays a folder (no history change). Used by NavigateTo and Back/Forward.</summary>
        private void ShowFolder(string path)
        {
            BtnNavBack.IsEnabled    = _navIndex > 0;
            BtnNavForward.IsEnabled = _navIndex < _navHistory.Count - 1;

            _currentPath = path;
            TxtAddressBar.Text = path;
            BreadcrumbPath.Text = path;

            WatchCurrentFolder(path);

            // Refresh active tab
            RefreshCurrentTab();
        }

        // ─── Live updates for the folder being viewed ────────────────────
        private FileSystemWatcher? _currentWatcher;
        private System.Windows.Threading.DispatcherTimer? _currentDebounce;
        private bool _foldersChangedSinceScan;

        /// <summary>
        /// Watches the folder on screen. The file browser refreshes itself; the Folder Sizes view
        /// (which shows the last scan) gets a "changed since last scan" note so sizes are never
        /// silently stale.
        /// </summary>
        private void WatchCurrentFolder(string path)
        {
            _currentWatcher?.Dispose();
            _currentWatcher = null;
            _foldersChangedSinceScan = false;
            if (!Directory.Exists(path)) return;
            try
            {
                _currentDebounce ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _currentDebounce.Tick -= CurrentFolderChanged;
                _currentDebounce.Tick += CurrentFolderChanged;

                var w = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size
                                 | NotifyFilters.LastWrite | NotifyFilters.Attributes,
                    IncludeSubdirectories = false,
                };
                void Changed(object? _, FileSystemEventArgs e) => Dispatcher.BeginInvoke(() =>
                {
                    if (Directory.Exists(e.FullPath) || e.ChangeType is WatcherChangeTypes.Deleted or WatcherChangeTypes.Renamed)
                        _foldersChangedSinceScan = true;
                    _currentDebounce.Stop(); _currentDebounce.Start();
                });
                w.Created += Changed; w.Deleted += Changed; w.Renamed += Changed; w.Changed += Changed;
                w.EnableRaisingEvents = true;
                _currentWatcher = w;
            }
            catch { }
        }

        private void CurrentFolderChanged(object? sender, EventArgs e)
        {
            _currentDebounce?.Stop();
            ShellIcons.Invalidate(_currentPath);
            switch (MainTabCtrl.SelectedIndex)
            {
                case 1: LoadFileBrowser(_currentPath); break;   // live view of the disk
                case 0:
                    if (_foldersChangedSinceScan && _scanTask == null)
                        StatusTotal.Text = "Folders here changed since the last scan (renamed, added or removed) — press Scan to update sizes.";
                    break;
            }
        }

        // ─── Theme / settings / zoom ──────────────────────────────────────
        private Brush Res(string key) => (Brush)FindResource(key);
        private double Fs(int n) => (double)FindResource($"Fs{n}");

        // Code-drawn visuals don't use DynamicResource, so redraw them on theme/font change
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (!IsLoaded) return;
            LoadDiskSpacePanel();
            DrawChart();
            FoldersGrid.Items.Refresh(); // re-evaluate % bar colours
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void OpenSettings(string page = "Appearance")
        {
            var dlg = new SettingsWindow { Owner = this };
            dlg.ShowPage(page);
            dlg.ShowDialog();
        }

        // Zooming with the keyboard/wheel switches off "Use Windows scaling";
        // Ctrl+0 switches it back on.
        private void SetUiScale(double scale, bool useWindows = false)
        {
            var s = SettingsService.Current.Clone();
            s.UseWindowsScaling = useWindows;
            s.UiScale = useWindows ? 1.0 : Math.Round(Math.Clamp(scale, 0.8, 2.0), 1);
            SettingsService.Save(s);
            ThemeManager.Apply(SettingsService.Current);
            StatusTotal.Text = useWindows ? "UI scale: using Windows display scaling" : $"UI scale {s.UiScale * 100:0}%";
        }

        private double CurrentUiScale => ThemeManager.EffectiveUiScale(SettingsService.Current);

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Alt+Left / Alt+Right / Alt+Up (as shown in the toolbar tooltips). With Alt held,
            // WPF reports the key in SystemKey.
            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                switch (e.SystemKey)
                {
                    case Key.Left:  if (BtnNavBack.IsEnabled)    BtnNavBack_Click(sender, e);    e.Handled = true; return;
                    case Key.Right: if (BtnNavForward.IsEnabled) BtnNavForward_Click(sender, e); e.Handled = true; return;
                    case Key.Up:    BtnNavUp_Click(sender, e); e.Handled = true; return;
                }
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            double cur = CurrentUiScale;
            switch (e.Key)
            {
                case Key.OemPlus: case Key.Add:        SetUiScale(cur + 0.1); e.Handled = true; break;
                case Key.OemMinus: case Key.Subtract:  SetUiScale(cur - 0.1); e.Handled = true; break;
                case Key.D0: case Key.NumPad0:         SetUiScale(1.0, useWindows: true); e.Handled = true; break;
                case Key.OemComma:                     OpenSettings();         e.Handled = true; break;
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            SetUiScale(CurrentUiScale + (e.Delta > 0 ? 0.1 : -0.1));
            e.Handled = true;
        }

        // Full path without a trailing separator — except drive roots, which keep it.
        // ("C:" on its own means "current directory on drive C", not the drive root.)
        private static string NormalizePath(string path)
        {
            try { path = System.IO.Path.GetFullPath(path); } catch { }
            return System.IO.Path.TrimEndingDirectorySeparator(path);
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
        /// <summary>
        /// Lists everything in a folder, like File Explorer: every sub-folder (with its size from
        /// the last scan, or "Not scanned") and every file (sizes read straight from disk).
        /// The disk decides what is listed, so deleted folders disappear and new ones appear even
        /// before a rescan; the scan database only supplies folder sizes.
        /// </summary>
        /// <summary>Folder Sizes rows without the ".." row (for charts, totals and exports).</summary>
        private IEnumerable<FolderItem> RealFolderItems => _folderItems.Where(f => !f.IsParentLink);

        /// <summary>The folder ".." should go to, or null at the top of a drive/share.</summary>
        private static string? ParentLinkFor(string path)
        {
            try { return Directory.GetParent(path)?.FullName; } catch { return null; }
        }

        private void LoadFolderSizes(string path)
        {
            _folderItems.Clear();
            if (string.IsNullOrEmpty(path)) return;
            path = NormalizePath(path);

            // Scanned sizes for this folder and its direct sub-folders
            FolderItem? self;
            Dictionary<string, FolderItem> scanned;
            using (var db = new FolderDiagDbContext(_dbOpts))
            {
                self = db.FolderItems.AsNoTracking()
                    .Where(f => EF.Functions.Collate(f.Path, "NOCASE") == path)
                    .OrderByDescending(f => f.ScannedAt)
                    .FirstOrDefault();
                scanned = db.FolderItems.AsNoTracking()
                    .Where(f => EF.Functions.Collate(f.ParentPath, "NOCASE") == path)
                    .AsEnumerable()
                    .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.ScannedAt).First(), StringComparer.OrdinalIgnoreCase);
            }

            var items = new List<FolderItem>();
            int unscanned = 0;
            try
            {
                var opts = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
                var dir = new DirectoryInfo(path);

                foreach (var d in dir.EnumerateDirectories("*", opts))
                {
                    if (scanned.TryGetValue(d.FullName, out var row))
                    {
                        row.Name = ShellIcons.FolderDisplayName(d);   // Explorer's name
                        items.Add(row);
                    }
                    else
                    {
                        unscanned++;
                        items.Add(new FolderItem
                        {
                            Path = d.FullName, Name = ShellIcons.FolderDisplayName(d), ParentPath = path,
                            IsScanned = false, LastModified = d.LastWriteTime, Created = d.CreationTime,
                        });
                    }
                }

                foreach (var f in dir.EnumerateFiles("*", opts))
                {
                    items.Add(new FolderItem
                    {
                        IsFile = true, Path = f.FullName, Name = f.Name, ParentPath = path,
                        SizeBytes = f.Length, AllocatedBytes = (f.Length + 4095) / 4096 * 4096,
                        LastModified = f.LastWriteTime, Created = f.CreationTime,
                    });
                }
            }
            catch (Exception ex)
            {
                StatusTotal.Text = $"Can't read {path}: {ex.Message}";
            }

            // Percentages of everything listed, so they always add up to 100%
            long total = items.Where(i => i.IsScanned).Sum(i => i.SizeBytes);
            foreach (var i in items)
                i.PercentOfParent = total > 0 && i.IsScanned ? 100.0 * i.SizeBytes / total : 0;

            // Biggest first; not-yet-scanned folders after scanned ones, then by name
            // ".." first (except at the top of a drive), then biggest first; not-yet-scanned
            // folders after scanned ones, then by name
            if (ParentLinkFor(path) is string parent)
                _folderItems.Add(new FolderItem { Path = parent, Name = "..", IsParentLink = true, IsScanned = false });
            foreach (var i in items.OrderByDescending(i => i.IsScanned)
                                   .ThenByDescending(i => i.SizeBytes)
                                   .ThenBy(i => i.Name, ShellIcons.LogicalComparer))
                _folderItems.Add(i);

            // Folder never scanned as a whole: scan it once automatically (if enabled)
            if (self == null && unscanned > 0)
            {
                if (SettingsService.Current.AutoScanOnNavigate && _autoScanAttempted.Add(path))
                    _ = StartScanAsync(path);
                else if (_scanTask == null)
                    StatusTotal.Text = $"{unscanned:N0} folder(s) here haven't been scanned yet — click Scan to calculate their sizes.";
            }
            else if (unscanned > 0 && _scanTask == null)
            {
                StatusTotal.Text = $"{unscanned:N0} new folder(s) since the last scan — click Scan to update.";
            }
            else
            {
                UpdateStatus();
            }

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

            if (ParentLinkFor(path) is string parentDir)
                _browserItems.Add(new BrowserEntry { Name = "..", Path = parentDir, IsFolder = true, IsParentLink = true });

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
                        Name      = ShellIcons.FolderDisplayName(di),   // e.g. "Belgeler" shows as "Documents"
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
                var brush = ThemeManager.DiskBarBrush(usedPct); // colours chosen in Settings › Bar colours

                string label = !string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')}) — {drive.DriveType}"
                    : $"Local Disk ({drive.Name.TrimEnd('\\')}) — {drive.DriveType}";

                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

                // Drive name + type
                panel.Children.Add(new TextBlock
                {
                    Text       = $"💽  {label}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = Fs(14),
                    Foreground = Res("Win11TextBrush"),
                    Margin     = new Thickness(0, 0, 0, 4)
                });

                // Usage bar
                var barBg = new Border
                {
                    Height          = 18,
                    Background      = Res("Win11BorderBrush"),
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
                    Foreground = Res("Win11SubTextBrush"),
                    FontSize   = Fs(12)
                });

                // Format + filesystem
                panel.Children.Add(new TextBlock
                {
                    Text       = $"File System: {drive.DriveFormat}",
                    Foreground = Res("Win11SubTextBrush"),
                    FontSize   = Fs(11),
                    Margin     = new Thickness(0, 2, 0, 0)
                });

                var border = new Border
                {
                    Child           = panel,
                    BorderBrush     = Res("Win11BorderBrush"),
                    Background      = Res("Win11PanelBrush"),
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
            _autoScanAttempted.Add(NormalizePath(_currentPath));
            await StartScanAsync(_currentPath);
        }

        // Runs one scan at a time. Starting a new scan (e.g. by navigating to another
        // unscanned folder) cancels the running one and waits for it to finish first.
        private async Task StartScanAsync(string scanPath)
        {
            scanPath = NormalizePath(scanPath);

            if (_scanTask != null)
            {
                _scanCts?.Cancel();
                try { await _scanTask; } catch { }
            }

            var cts = new CancellationTokenSource();
            _scanCts = cts;
            BtnScan.IsEnabled = false;
            BtnStop.IsEnabled = true;

            long expectedBytes = EstimateScanBytes(scanPath);
            ShowScanBanner(scanPath, expectedBytes);
            ScanProgress lastProgress = default;

            // Progress<T> marshals back to the UI thread; the scanner throttles to ~5 updates/sec
            var progress = new Progress<ScanProgress>(p =>
            {
                if (!ReferenceEquals(_scanCts, cts)) return;
                lastProgress = p;
                UpdateScanBanner(p, expectedBytes);
            });

            var task = Task.Run(() => PerformScan(scanPath, progress, cts.Token));
            _scanTask = task;
            try
            {
                var totals = await task;
                FinishScanBanner(ScanOutcome.Completed,
                    $"Scan complete — {scanPath}",
                    $"{totals.Files:N0} files · {totals.Folders:N0} folders · {FolderItem.FormatBytes(totals.Size)} in {FormatDuration(_scanClock.Elapsed)}");
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(_scanTask, task))
                    FinishScanBanner(ScanOutcome.Stopped, "Scan stopped — previous results kept",
                        $"Stopped after {lastProgress.Files:N0} files · {FolderItem.FormatBytes(lastProgress.Bytes)} · {FormatDuration(_scanClock.Elapsed)}");
            }
            catch (Exception ex)
            {
                FinishScanBanner(ScanOutcome.Failed, "Scan failed", ex.Message);
            }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_scanTask, task))
                {
                    // Only the most recent scan updates the UI
                    _scanTask = null;
                    _scanCts  = null;
                    BtnScan.IsEnabled = true;
                    BtnStop.IsEnabled = false;
                    RefreshCurrentTab();
                }
            }
        }

        // Fast scanner (FolderDiag.Core.Scanning) + streaming bulk writer (FolderDiag.Data.ScanStore).
        // Runs in one transaction: stopping a scan keeps the previous results.
        private ScanTotals PerformScan(string rootPath, IProgress<ScanProgress> progress, CancellationToken ct)
        {
            var settings = SettingsService.Current;
            var options = new ScanOptions
            {
                MaxParallelism = settings.ParallelScanning ? Math.Clamp(Environment.ProcessorCount, 2, 8) : 1,
                OwnerResolver  = settings.ReadOwners ? OwnerResolver.Shared.Resolve : null,
                FileOwners     = settings.ReadOwners,
            };
            return ScanStore.ScanAndStore(_dbOpts, rootPath, options, progress, ct);
        }

        // ─── Scan feedback (banner, status bar, taskbar) ─────────────────
        private enum ScanOutcome { Completed, Stopped, Failed }
        // Taskbar button progress (like File Explorer copies) — visible even when minimised
        private System.Windows.Shell.TaskbarItemInfo Taskbar => TaskbarItemInfo ??= new System.Windows.Shell.TaskbarItemInfo();
        private readonly System.Diagnostics.Stopwatch _scanClock = new();
        private System.Windows.Threading.DispatcherTimer? _bannerHideTimer;

        /// <summary>
        /// Bytes we expect to read, for a real percentage: a drive's used space, or the folder's
        /// size from its last scan. 0 = unknown (progress bar shows the sliding "busy" animation).
        /// </summary>
        private long EstimateScanBytes(string path)
        {
            try
            {
                string? root = System.IO.Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root) && string.Equals(NormalizePath(root), path, StringComparison.OrdinalIgnoreCase))
                {
                    var d = new DriveInfo(root);
                    if (d.IsReady) return d.TotalSize - d.TotalFreeSpace;
                }
                using var db = new FolderDiagDbContext(_dbOpts);
                return db.FolderItems.AsNoTracking()
                    .Where(f => EF.Functions.Collate(f.Path, "NOCASE") == path)
                    .Select(f => f.SizeBytes).FirstOrDefault();
            }
            catch { return 0; }
        }

        private void ShowScanBanner(string path, long expectedBytes)
        {
            _bannerHideTimer?.Stop();
            _scanClock.Restart();

            ScanBanner.BeginAnimation(OpacityProperty, null);
            ScanBanner.Opacity = 1;
            ScanBanner.Visibility = Visibility.Visible;
            BannerStop.Visibility = Visibility.Visible;
            BannerDismiss.Visibility = Visibility.Collapsed;

            BannerIcon.Text = "\uE895"; // Sync
            BannerIcon.SetResourceReference(TextBlock.ForegroundProperty, "Win11AccentBrush");
            BannerIconBg.SetResourceReference(Shape.FillProperty, "Win11SelectBrush");
            SetSpinner(true);

            BannerTitle.Text   = $"Scanning {path}";
            BannerDetail.Text  = expectedBytes > 0 ? $"Estimated {FolderItem.FormatBytes(expectedBytes)} to read" : "Counting files…";
            BannerCurrent.Text = "";
            BannerPercent.Text = "";
            bool known = expectedBytes > 0;
            BannerProgress.IsIndeterminate = !known;
            BannerProgress.Value = 0;
            BannerProgress.Visibility = Visibility.Visible;

            ScanProgress.Visibility = Visibility.Visible;
            ScanProgress.IsIndeterminate = !known;
            ScanProgress.Value = 0;
            StatusTotal.Text = $"Scanning {path}…";

            Taskbar.ProgressState = known ? System.Windows.Shell.TaskbarItemProgressState.Normal
                                          : System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
            Taskbar.ProgressValue = 0;
        }

        private void UpdateScanBanner(ScanProgress p, long expectedBytes)
        {
            var elapsed = p.Elapsed;
            double secs = Math.Max(0.1, elapsed.TotalSeconds);
            string rate = $"{p.Files / secs:N0} files/s";
            string detail = $"{p.Files:N0} files · {p.Folders:N0} folders · {FolderItem.FormatBytes(p.Bytes)} · {rate} · {FormatDuration(elapsed)}";

            if (expectedBytes > 0)
            {
                // Bytes-based estimate; capped at 99% because some space (page file, system files) can't be read
                double pct = Math.Min(99, 100.0 * p.Bytes / expectedBytes);
                BannerProgress.Value = pct;
                ScanProgress.Value   = pct;
                Taskbar.ProgressValue = pct / 100;
                BannerPercent.Text = $"{pct:0}%";
                if (pct >= 3 && secs >= 3 && p.Bytes > 0)
                {
                    var left = TimeSpan.FromSeconds(secs * (expectedBytes - p.Bytes) / p.Bytes);
                    if (left > TimeSpan.Zero) detail += $" · about {FormatDuration(left)} left";
                }
            }

            BannerDetail.Text  = detail;
            BannerCurrent.Text = p.CurrentPath;
            StatusTotal.Text   = $"Scanning… {p.Files:N0} files · {FolderItem.FormatBytes(p.Bytes)} · {rate}";
        }

        private void FinishScanBanner(ScanOutcome outcome, string title, string detail)
        {
            _scanClock.Stop();
            SetSpinner(false);
            BannerStop.Visibility = Visibility.Collapsed;
            BannerDismiss.Visibility = Visibility.Visible;
            BannerCurrent.Text = "";
            ScanProgress.Visibility = Visibility.Collapsed;

            (string glyph, Color color) = outcome switch
            {
                ScanOutcome.Completed => ("\uE73E", Color.FromRgb(0x10, 0x7C, 0x10)), // check
                ScanOutcome.Stopped   => ("\uE71A", Color.FromRgb(0x9D, 0x5D, 0x00)), // stop
                _                     => ("\uE7BA", Color.FromRgb(0xC4, 0x2B, 0x1C)), // warning
            };
            BannerIcon.Text = glyph;
            BannerIcon.Foreground = Brushes.White;
            BannerIconBg.Fill = new SolidColorBrush(color);

            BannerTitle.Text  = title;
            BannerDetail.Text = detail;
            if (outcome == ScanOutcome.Completed)
            {
                BannerProgress.IsIndeterminate = false;
                BannerProgress.Value = 100;
                BannerPercent.Text = "100%";
            }
            else
            {
                BannerProgress.Visibility = Visibility.Collapsed;
                BannerPercent.Text = "";
            }

            StatusTotal.Text = $"{title}. {detail}";
            Taskbar.ProgressState = outcome == ScanOutcome.Failed
                ? System.Windows.Shell.TaskbarItemProgressState.Error
                : System.Windows.Shell.TaskbarItemProgressState.None;

            // Results stay visible for a few seconds (errors stay until dismissed), then fade out
            if (outcome == ScanOutcome.Failed) return;
            _bannerHideTimer?.Stop();
            _bannerHideTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _bannerHideTimer.Tick += (_, _) => { _bannerHideTimer.Stop(); HideScanBanner(); };
            _bannerHideTimer.Start();
        }

        private void HideScanBanner()
        {
            if (_scanTask != null) return; // a new scan started meanwhile
            if (!SystemParameters.ClientAreaAnimation) { ScanBanner.Visibility = Visibility.Collapsed; return; }
            var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fade.Completed += (_, _) => { if (_scanTask == null) ScanBanner.Visibility = Visibility.Collapsed; };
            ScanBanner.BeginAnimation(OpacityProperty, fade);
        }

        private void BannerDismiss_Click(object sender, RoutedEventArgs e)
        {
            _bannerHideTimer?.Stop();
            ScanBanner.Visibility = Visibility.Collapsed;
            Taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        }

        /// <summary>Spins the banner icon — unless Windows "Animation effects" is turned off.</summary>
        private void SetSpinner(bool on)
        {
            if (BannerIcon.RenderTransform is not RotateTransform rot) return;
            if (on && SystemParameters.ClientAreaAnimation)
                rot.BeginAnimation(RotateTransform.AngleProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
                    { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
            else
            {
                rot.BeginAnimation(RotateTransform.AngleProperty, null);
                rot.Angle = 0;
            }
        }

        private static string FormatDuration(TimeSpan t)
            => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m"
             : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds:00}s"
             : $"{Math.Max(0, t.TotalSeconds):0}s";

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
            StatusTotal.Text = "Stopping scan…";
            if (ScanBanner.Visibility == Visibility.Visible) BannerTitle.Text = "Stopping scan…";
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => RefreshCurrentTab();

        // ─── Charts ───────────────────────────────────────────────────────
        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            var items = RealFolderItems.Take(20).ToList();
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
                    Foreground    = Res("Win11TextBrush")
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
                    Foreground    = Res("Win11SubTextBrush")
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
                var legendLbl = new TextBlock { Text = legText, FontSize = 10, Foreground = Res("Win11TextBrush") };
                Canvas.SetLeft(legendLbl, 308);
                Canvas.SetTop (legendLbl, 19 + i * 18);
                ChartCanvas.Children.Add(legendLbl);

                startAngle += sweep;
            }

            ChartCanvas.Width  = 550;
            ChartCanvas.Height = 280;
        }

        private System.Windows.Shapes.Path BuildPieSlice(
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
                Stroke = Res("Win11ChartBgBrush"),
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
                double bw   = w * wPct;   // share of the full width (was 'remaining', which shrank later tiles)

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
            if (sender is FrameworkElement { Tag: FolderItem fi })
            {
                MainTabCtrl.SelectedIndex = 0;
                OpenFolderItem(fi);   // folders drill in, files open in their app
            }
        }

        private void ChartCanvas_Click(object sender, MouseButtonEventArgs e) { }

        // ─── Status update ────────────────────────────────────────────────
        private void UpdateStatus()
        {
            var real = RealFolderItems.ToList();
            long totalSize  = real.Sum(f => f.SizeBytes);
            int  directFiles = real.Count(f => f.IsFile);
            int  totalFiles = real.Sum(f => f.FileCount) + directFiles;
            int  totalFolders = real.Count(f => !f.IsFile) + real.Sum(f => f.FolderCount);

            StatusTotal.Text     = $"{real.Count(f => !f.IsFile):N0} folders, {directFiles:N0} files in view";
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
            DetIcon.Source   = ShellIcons.Get(item.Path, !item.IsFile);
            DetPath.Text     = item.Path;

            // Size bar: share of the bar's track (its parent Grid). The old code cast the parent to
            // Border — it's a Grid — and crashed with InvalidCastException on every selection.
            double track = (DetSizeBar.Parent as FrameworkElement)?.ActualWidth ?? 0;
            if (track <= 0) track = Math.Max(0, DetailsPanel.ActualWidth - 24);
            DetSizeBar.Width = item.IsScanned ? Math.Max(2, track * Math.Clamp(item.PercentOfParent, 0, 100) / 100.0) : 0;
        }

        // ─── Reports ──────────────────────────────────────────────────────
        private void LoadReport(string type, int topN)
        {
            _reportItems.Clear();
            using var db = new FolderDiagDbContext(_dbOpts);

            IQueryable<FileItem> q = db.FileItems.AsNoTracking();
            if (!string.IsNullOrEmpty(_currentPath))
                q = ScanStore.FilesUnder(q, _currentPath);

            IEnumerable<FileItem> result = type switch
            {
                "Largest Files"              => q.OrderByDescending(f => f.SizeBytes),
                "Oldest Files"              => q.OrderBy(f => f.Modified),
                "Newest Files"              => q.OrderByDescending(f => f.Modified),
                "Temporary Files"           => q.Where(f => f.IsTemp).OrderByDescending(f => f.SizeBytes),
                // Sizes that occur more than once, then the files with those sizes (SQL-translatable;
                // GroupBy(...).SelectMany(...) isn't, and threw at runtime). Empty files are ignored.
                "Duplicate Files (by size)" => q.Where(f => f.SizeBytes > 0 &&
                                                   q.Where(o => o.SizeBytes == f.SizeBytes).Skip(1).Any())
                                                 .OrderByDescending(f => f.SizeBytes).ThenBy(f => f.Name),
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

            var q = db.FileItems.AsNoTracking();
            if (!string.IsNullOrEmpty(_currentPath))
                q = ScanStore.FilesUnder(q, _currentPath);

            // Group by extension in SQL, then map extensions to categories here (much less data
            // than pulling every file row into memory)
            var groups = q.GroupBy(f => f.Extension)
                .Select(g => new { Extension = g.Key, Size = g.Sum(f => f.SizeBytes), Count = g.Count() })
                .AsEnumerable()
                .GroupBy(x => FileItem.GetCategory(x.Extension))
                .Select(g => new
                {
                    Category  = g.Key,
                    TotalSize = g.Sum(x => x.Size),
                    Count     = g.Sum(x => x.Count)
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

            var q = db.FileItems.AsNoTracking();
            if (!string.IsNullOrEmpty(_currentPath))
                q = ScanStore.FilesUnder(q, _currentPath);

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
            // Category filter in SQL via the category's extensions (calling GetCategory inside the
            // query couldn't be translated and threw at runtime)
            if (cat == "Other")
                q = q.Where(f => !FileItem.AllCategorisedExtensions.Contains(f.Extension));
            else if (cat != "All" && FileItem.CategoryExtensions.TryGetValue(cat, out var exts))
                q = q.Where(f => exts.Contains(f.Extension));

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

            MainTabCtrl.SelectedIndex = 5;   // Search tab (4 is Classification)
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
            // The folder's own scanned row already holds the recursive total; summing every row
            // below it (as before) counted nested folders many times over.
            var self = db.FolderItems.AsNoTracking()
                .Where(f => EF.Functions.Collate(f.Path, "NOCASE") == _currentPath)
                .OrderByDescending(f => f.ScannedAt).FirstOrDefault();
            if (self == null)
            {
                MessageBox.Show(this, "Scan this folder first, then take a snapshot.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            long total   = self.SizeBytes;
            int  files   = self.FileCount;
            int  folders = self.FolderCount;

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
            foreach (var f in RealFolderItems)
                sb.AppendLine(string.Join(",",
                    Csv(f.Name), Csv(f.Path), f.SizeBytes.ToString(CultureInfo.InvariantCulture), Csv(f.SizeFormatted),
                    f.FileCount.ToString(CultureInfo.InvariantCulture), f.FolderCount.ToString(CultureInfo.InvariantCulture),
                    f.PercentOfParent.ToString("F2", CultureInfo.InvariantCulture),
                    Csv(f.LastModified?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), Csv(f.Owner)));

            try
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusTotal.Text = $"Exported CSV → {dlg.FileName}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Couldn't save {dlg.FileName}:\n{ex.Message}\n\nIf it's open in another program (e.g. Excel), close it and try again.",
                                "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnExportXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "XML|*.xml", FileName = "folderdiag_export.xml" };
            if (dlg.ShowDialog() != true) return;

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement("FolderReport",
                    new XAttribute("GeneratedAt", DateTime.Now),
                    new XAttribute("Root", _currentPath),
                    RealFolderItems.Select(f =>
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
            try
            {
                doc.Save(dlg.FileName);
                StatusTotal.Text = $"Exported XML → {dlg.FileName}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Couldn't save {dlg.FileName}:\n{ex.Message}\n\nIf it's open in another program (e.g. Excel), close it and try again.",
                                "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            sb.Append($"<h1>FolderDiag Report</h1><p>Root: <strong>{Html(_currentPath)}</strong> — Generated: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            sb.Append("<table><thead><tr><th>Name</th><th>Size</th><th>Bar</th><th>%</th><th>Files</th><th>Folders</th><th>Last Modified</th><th>Owner</th></tr></thead><tbody>");

            foreach (var f in RealFolderItems)
            {
                int barW = (int)Math.Min(200, f.PercentOfParent * 2);
                sb.Append($"<tr><td>{Html(f.Name)}</td><td>{f.SizeFormatted}</td><td><div class='bar' style='width:{barW}px'></div></td><td>{f.PercentFormatted}</td><td>{f.FileCountFormatted}</td><td>{f.FolderCountFormatted}</td><td>{f.LastModified:yyyy-MM-dd HH:mm}</td><td>{Html(f.Owner)}</td></tr>");
            }

            sb.Append("</tbody></table></body></html>");
            try
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusTotal.Text = $"Exported HTML → {dlg.FileName}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Couldn't save {dlg.FileName}:\n{ex.Message}\n\nIf it's open in another program (e.g. Excel), close it and try again.",
                                "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ─── File operations ──────────────────────────────────────────────
        private string? GetSelectedFolderPath()
        {
            if (FoldersGrid.SelectedItem is FolderItem fi) return fi.IsFile || fi.IsParentLink ? null : fi.Path;
            if (FileBrowserGrid.SelectedItem is BrowserEntry be) return be.IsParentLink ? null : be.Path;
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

            // Refuse to copy/move a folder into itself (would recurse forever / corrupt data)
            string srcFull  = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(src));
            string destFull = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(dest));
            if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase) ||
                destFull.StartsWith(srcFull + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The destination is inside the source folder.");
                return;
            }
            if (Directory.Exists(dest) &&
                MessageBox.Show($"'{dest}' already exists. Merge and overwrite existing files?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

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
            {
                // Don't follow junctions/symlinks: they can loop or point outside the source tree
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                CopyDirectory(dir, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(dir)));
            }
        }

        // ─── Export helpers ───────────────────────────────────────────────
        // Quotes a CSV field and neutralises spreadsheet formula injection (=, +, -, @, tab, CR).
        private static string Csv(string? value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Same as the Delete key: Windows' own Recycle Bin confirmation and progress
            if (EditableEntryOf(ActiveGrid?.SelectedItem) is Entry selected)
            {
                DeleteEntry(selected, permanently: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                return;
            }

            // A file row in Folder Sizes: send the file to the Recycle Bin
            if (FoldersGrid.SelectedItem is FolderItem { IsFile: true } file)
            {
                if (MessageBox.Show($"Delete '{file.Path}'?\nThis sends it to the Recycle Bin.",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    StatusTotal.Text = $"Deleted: {file.Path}";
                    RefreshCurrentTab();
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
                return;
            }

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
            => OwnerResolver.Shared.Resolve(path, Directory.Exists(path));

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
                string path = ResolveSpecialFolder(tvi.Tag?.ToString());
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
            // SelectionChanged bubbles: selecting a row in a DataGrid (or an item in a ComboBox)
            // inside a tab arrives here too. Only react to the tab itself changing — otherwise
            // every click on a row reloaded the list, wiping the selection mid double-click.
            if (!ReferenceEquals(e.OriginalSource, MainTabCtrl)) return;
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
            if (!IsLoaded) return;
            string term = TxtSearch.Text.Trim().ToLowerInvariant();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrEmpty(term)) { LoadFolderSizes(_currentPath); return; }

            var filtered = _folderItems.Where(f => f.IsParentLink ||
                f.Name.ToLowerInvariant().Contains(term) ||
                f.Path.ToLowerInvariant().Contains(term)).ToList();

            _folderItems.Clear();
            foreach (var f in filtered) _folderItems.Add(f);
        }

        // Nav arrows
        // Back/Forward skip folders that no longer exist and update everything NavigateTo does
        // (address bar, breadcrumb, folder watcher) — previously the watcher stayed on the old folder.
        private void BtnNavBack_Click(object sender, RoutedEventArgs e) => StepHistory(-1);
        private void BtnNavForward_Click(object sender, RoutedEventArgs e) => StepHistory(+1);

        private void StepHistory(int direction)
        {
            for (int i = _navIndex + direction; i >= 0 && i < _navHistory.Count; i += direction)
            {
                if (!Directory.Exists(_navHistory[i])) continue;
                _navIndex = i;
                ShowFolder(_navHistory[i]);
                return;
            }
            StatusTotal.Text = direction < 0 ? "No earlier folder to go back to." : "No later folder to go forward to.";
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
            if (FoldersGrid.SelectedItem is FolderItem { IsParentLink: false } f)
                UpdateDetails(f);
        }
        // Single click only selects (like File Explorer) so folders can be selected and right-clicked;
        // double-click or Enter opens. (Opening on selection made that impossible.)
        private void FileBrowserGrid_SelectionChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FileBrowserGrid.SelectedItem is BrowserEntry be)
                StatusTotal.Text = be.IsFolder
                    ? $"{be.Name}  —  folder (double-click or Enter to open)"
                    : $"{be.Name}  —  {FolderItem.FormatBytes(be.SizeBytes)}";
        }

        // ─── Open items: double-click / Enter, like File Explorer ─────────
        /// <summary>The data item of the row containing <paramref name="start"/>; null for headers, scrollbars, empty space.</summary>
        private static object? RowItemFrom(DataGrid grid, DependencyObject? start)
        {
            for (DependencyObject? d = start; d != null && d != grid; d = ParentOf(d))
            {
                if (d is DataGridRow row) return row.Item;
                if (d is System.Windows.Controls.Primitives.DataGridColumnHeader or System.Windows.Controls.Primitives.ScrollBar) return null;
            }
            return null;
        }

        private static DependencyObject? ParentOf(DependencyObject d)
            => d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

        private void CtxFolderOpen_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersGrid.SelectedItem is FolderItem f) OpenFolderItem(f);
        }

        /// <summary>Right-click selects the row under the mouse first, so the context menu acts on it.</summary>
        private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject src) return;
            for (DependencyObject? d = src; d != null && d != grid; d = ParentOf(d))
                if (d is DataGridRow row) { grid.SelectedItem = row.Item; row.Focus(); return; }
        }

        private void OpenFolderItem(FolderItem f)
        {
            StatusTotal.Text = $"Opening {f.Name}…";
            if (f.IsFile)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(f.Path) { UseShellExecute = true }); }
                catch (Exception ex) { StatusTotal.Text = $"Cannot open '{f.Name}': {ex.Message}"; }
                return;
            }
            if (Directory.Exists(f.Path)) NavigateTo(f.Path);
            else StatusTotal.Text = $"'{f.Name}' no longer exists — it may have been moved, renamed or deleted since the last scan.";
        }

        private void OpenBrowserEntry(BrowserEntry be)
        {
            StatusTotal.Text = $"Opening {be.Name}…";
            if (be.IsFolder) { NavigateTo(be.Path); return; }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(be.Path) { UseShellExecute = true }); }
            catch (Exception ex) { StatusTotal.Text = $"Cannot open '{be.Name}': {ex.Message}"; }
        }

        // Ribbon sort/filter
        // NOTE: these handlers also fire during InitializeComponent() when XAML sets
        // IsChecked="True" / IsSelected="True" — before later controls (e.g. BreadcrumbPath)
        // or _dbOpts exist. Ignore them until the window has loaded.
        private void CmbSort_Changed(object sender, SelectionChangedEventArgs e)    { if (IsLoaded) LoadFolderSizes(_currentPath); }
        private void CmbMinSize_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadFolderSizes(_currentPath); }
        private void CmbDepth_Changed(object sender, SelectionChangedEventArgs e)   { if (IsLoaded) LoadFolderSizes(_currentPath); }

        // View options
        private void ChkShowHidden_Changed(object sender, RoutedEventArgs e)     { if (IsLoaded) LoadFileBrowser(_currentPath); }
        private void ChkShowSystem_Changed(object sender, RoutedEventArgs e)     { if (IsLoaded) LoadFileBrowser(_currentPath); }
        private void ChkShowExtensions_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) LoadFileBrowser(_currentPath); }

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
            // Dictionaries by path (case-insensitive): the old per-path FirstOrDefault scan was
            // O(n²) and froze the UI on large drives
            static Dictionary<string, long> SizesUnder(FolderDiagDbContext db, string root) =>
                ScanStore.FoldersBelow(db.FolderItems.AsNoTracking(), root)
                    .Select(f => new { f.Path, f.SizeBytes }).AsEnumerable()
                    .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().SizeBytes, StringComparer.OrdinalIgnoreCase);

            var folders1 = SizesUnder(db, snap1.RootPath);
            var folders2 = SizesUnder(db, snap2.RootPath);

            foreach (var path in folders1.Keys.Union(folders2.Keys, StringComparer.OrdinalIgnoreCase))
            {
                long before = folders1.GetValueOrDefault(path);
                long after  = folders2.GetValueOrDefault(path);
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
            if (FoldersGrid.SelectedItem is FolderItem { IsFile: true } file)
            {
                System.Diagnostics.Process.Start(App.ExplorerExe, $"/select,\"{file.Path}\"");
                return;
            }
            string? path = GetSelectedFolderPath();
            if (path != null)
                System.Diagnostics.Process.Start(App.ExplorerExe, $"\"{path}\"");
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
            if (EditableEntryOf(FoldersGrid.SelectedItem) is Entry f && MainTabCtrl.SelectedIndex == 0) ShowProperties(f.Path);
            else if (EditableEntryOf(FileBrowserGrid.SelectedItem) is Entry b) ShowProperties(b.Path);
            else if (GetSelectedFolderPath() is string p) ShowProperties(p);
        }

        // Context menu — file browser
        private void CtxBrowserOpen_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowserGrid.SelectedItem is BrowserEntry be) OpenBrowserEntry(be);
        }

        // Context menu — file reports / search
        private void CtxOpenContaining_Click(object sender, RoutedEventArgs e)
        {
            FileItem? fi = ReportsGrid.SelectedItem as FileItem ?? SearchGrid.SelectedItem as FileItem;
            if (fi != null)
                System.Diagnostics.Process.Start(App.ExplorerExe, $"/select,\"{fi.Path}\"");
        }
        private void CtxDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            FileItem? fi = ReportsGrid.SelectedItem as FileItem ?? SearchGrid.SelectedItem as FileItem;
            if (fi == null) return;
            // Recycle Bin (recoverable) with Windows' own dialogs — this used to delete permanently
            DeleteEntry(new Entry(fi.Path, fi.Name, IsFolder: false), permanently: false);
            if (!File.Exists(fi.Path)) { _reportItems.Remove(fi); _searchItems.Remove(fi); }
        }
    }

    // ── Extension helper ──────────────────────────────────────────────────
    internal static class Ext
    {
        public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}
