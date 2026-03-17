using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using FolderDiag.Core.Models;
using FolderDiag.Data;

namespace FolderDiag.Wpf
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FolderItem> _items = new();
        private DbContextOptions<FolderDiagDbContext> _dbOptions;

        public MainWindow()
        {
            InitializeComponent();
            FoldersGrid.ItemsSource = _items;

            _dbOptions = new DbContextOptionsBuilder<FolderDiagDbContext>()
                .UseSqlite("Data Source=folderdiag.db")
                .Options;

            // Ensure DB exists and seed sample data if empty
            using (var db = new FolderDiagDbContext(_dbOptions))
            {
                db.Database.EnsureCreated();
                if (!db.FolderItems.Any())
                {
                    db.FolderItems.Add(new FolderItem { Path = "C:\\", SizeBytes = 123456789, ScannedAt = DateTime.Now });
                    db.FolderItems.Add(new FolderItem { Path = "C:\\Windows", SizeBytes = 98765432, ScannedAt = DateTime.Now });
                    db.SaveChanges();
                }
            }

            LoadDrivesIntoTree();
            LoadItemsForPath("C:\\");
        }

        private void LoadDrivesIntoTree()
        {
            TreeFolders.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var ti = new TreeViewItem { Header = drive.Name.TrimEnd('\\'), Tag = drive.RootDirectory.FullName };
                ti.Items.Add(new object()); // dummy to show expand glyph
                ti.Expanded += Drive_Expanded;
                TreeFolders.Items.Add(ti);
            }
        }

        private void Drive_Expanded(object? sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem tvi && tvi.Items.Count == 1 && tvi.Items[0] is object)
            {
                tvi.Items.Clear();
                try
                {
                    var path = (string?)tvi.Tag;
                    if (path is null) return;
                    var dirs = Directory.GetDirectories(path);
                    foreach (var d in dirs)
                    {
                        var sub = new TreeViewItem { Header = System.IO.Path.GetFileName(d), Tag = d };
                        // add dummy
                        sub.Items.Add(new object());
                        sub.Expanded += Drive_Expanded;
                        tvi.Items.Add(sub);
                    }
                }
                catch { /* ignore inaccessible folders */ }
            }
        }

        private void TreeFolders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (TreeFolders.SelectedItem is TreeViewItem tvi && tvi.Tag is string path)
            {
                LoadItemsForPath(path);
            }
        }

        private void LoadItemsForPath(string path)
        {
            _items.Clear();
            using (var db = new FolderDiagDbContext(_dbOptions))
            {
                var rows = db.FolderItems.Where(f => EF.Functions.Like(f.Path, path + "%")).ToList();
                if (rows.Count == 0)
                {
                    // Fallback: if no data, show the selected folder itself
                    var fi = new FolderItem { Path = path, SizeBytes = 0, ScannedAt = DateTime.Now };
                    _items.Add(fi);
                }
                else
                {
                    foreach (var r in rows) _items.Add(r);
                }
            }

            StatusTotal.Text = $"Showing {_items.Count} items for {path}";
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false;
            StatusTotal.Text = "Scanning...";
            await Task.Run(() => PerformScan());
            StatusTotal.Text = "Scan complete";
            BtnScan.IsEnabled = true;
            BtnRefresh_Click(null, null);
        }

        private void PerformScan()
        {
            // Simple synchronous scan example that adds folder sizes for top-level folders in each drive
            using var db = new FolderDiagDbContext(_dbOptions);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                try
                {
                    var root = drive.RootDirectory.FullName;
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        long size = 0;
                        try
                        {
                            size = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => (new FileInfo(f).Length));
                        }
                        catch { }

                        var item = new FolderItem { Path = dir, SizeBytes = size, ScannedAt = DateTime.Now };
                        db.FolderItems.Add(item);
                    }
                }
                catch { }
            }
            db.SaveChanges();
        }

        private void BtnRefresh_Click(object? sender, RoutedEventArgs? e)
        {
            if (TreeFolders.SelectedItem is TreeViewItem tvi && tvi.Tag is string p)
                LoadItemsForPath(p);
            else
                LoadItemsForPath("C:\\");
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            // Not implemented: long-running scan cancellation
            StatusTotal.Text = "Stop requested (not implemented)";
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // Simple CSV export to folderdiag_export.csv
            try
            {
                var lines = _items.Select(i => $"\"{i.Path}\",{i.SizeBytes},\"{i.ScannedAt:O}\"");
                File.WriteAllLines("folderdiag_export.csv", lines);
                StatusTotal.Text = "Exported to folderdiag_export.csv";
            }
            catch (Exception ex)
            {
                StatusTotal.Text = "Export failed: " + ex.Message;
            }
        }
    }
}
