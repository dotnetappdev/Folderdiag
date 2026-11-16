using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FolderDiag.Core.Models;
using FolderDiag.Wpf.Theming;
using VbFs = Microsoft.VisualBasic.FileIO.FileSystem;
using VbUi = Microsoft.VisualBasic.FileIO.UIOption;
using VbRecycle = Microsoft.VisualBasic.FileIO.RecycleOption;

namespace FolderDiag.Wpf
{
    /// <summary>
    /// File Explorer behaviour for the right-hand grids (Folder Sizes and the file browser):
    /// click/double-click as configured in Windows, Explorer keyboard shortcuts, clipboard,
    /// rename, new folder, delete via the Windows shell, Properties, type-to-select and the
    /// mouse back/forward buttons.
    /// </summary>
    public partial class MainWindow
    {
        // ── The item a grid row represents ────────────────────────────
        private readonly record struct Entry(string Path, string Name, bool IsFolder);

        private static Entry? EntryOf(object? item) => item switch
        {
            FolderItem f   => new Entry(f.Path, f.Name, !f.IsFile),
            BrowserEntry b => new Entry(b.Path, b.Name, b.IsFolder),
            _              => null,
        };

        private DataGrid? ActiveGrid => MainTabCtrl.SelectedIndex switch
        {
            0 => FoldersGrid,
            1 => FileBrowserGrid,
            _ => null,
        };

        private void OpenEntry(Entry e)
        {
            StatusTotal.Text = $"Opening {e.Name}…";
            if (e.IsFolder)
            {
                if (Directory.Exists(e.Path)) NavigateTo(e.Path);
                else StatusTotal.Text = $"'{e.Name}' no longer exists — it may have been moved, renamed or deleted.";
                return;
            }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Path) { UseShellExecute = true }); }
            catch (Exception ex) { StatusTotal.Text = $"Cannot open '{e.Name}': {ex.Message}"; }
        }

        // ── Mouse: single- or double-click to open, exactly as set in Windows ──
        private object? _pressedItem;

        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            object? item = RowItemFrom(grid, e.OriginalSource as DependencyObject)
                        ?? RowItemFrom(grid, grid.InputHitTest(e.GetPosition(grid)) as DependencyObject);
            _pressedItem = item;

            if (!ExplorerSettings.SingleClickToOpen && e.ClickCount == 2 && EntryOf(item) is Entry entry)
            {
                e.Handled = true;
                OpenEntry(entry);
            }
        }

        private void Grid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // "Single-click to open an item (point to select)": open on release over the same row,
            // unless a modifier key is held (Ctrl/Shift click only selects, as in Explorer)
            if (!ExplorerSettings.SingleClickToOpen || sender is not DataGrid grid) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            object? item = RowItemFrom(grid, e.OriginalSource as DependencyObject);
            if (item != null && ReferenceEquals(item, _pressedItem) && EntryOf(item) is Entry entry)
            {
                e.Handled = true;
                OpenEntry(entry);
            }
            _pressedItem = null;
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            // Point to select (single-click mode)
            if (!ExplorerSettings.SingleClickToOpen || sender is not DataGrid grid || e.LeftButton == MouseButtonState.Pressed) return;
            var item = RowItemFrom(grid, e.OriginalSource as DependencyObject);
            if (item != null && !ReferenceEquals(grid.SelectedItem, item)) grid.SelectedItem = item;
        }

        /// <summary>Mouse back/forward side buttons, anywhere in the window (like Explorer).</summary>
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1 && BtnNavBack.IsEnabled)    { BtnNavBack_Click(sender, e);    e.Handled = true; }
            if (e.ChangedButton == MouseButton.XButton2 && BtnNavForward.IsEnabled) { BtnNavForward_Click(sender, e); e.Handled = true; }
        }

        // ── Keyboard: Explorer shortcuts ──────────────────────────────
        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            var mods = Keyboard.Modifiers;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            Entry? sel = EntryOf(grid.SelectedItem);

            bool handled = true;
            switch (key)
            {
                case Key.Enter when mods == ModifierKeys.None && sel is Entry s:           OpenEntry(s); break;
                case Key.Enter when mods == ModifierKeys.Alt && sel is Entry s:            ShowProperties(s.Path); break;
                case Key.Back when mods == ModifierKeys.None:                              BtnNavUp_Click(sender, e); break;
                case Key.F5:                                                               RefreshView(); break;
                case Key.F2 when sel is Entry s:                                           RenameEntry(s); break;
                case Key.Delete when sel is Entry s:                                       DeleteEntry(s, permanently: mods.HasFlag(ModifierKeys.Shift)); break;
                case Key.C when mods == ModifierKeys.Control && sel is Entry s:            CopyToClipboard(s, cut: false); break;
                case Key.X when mods == ModifierKeys.Control && sel is Entry s:            CopyToClipboard(s, cut: true); break;
                case Key.V when mods == ModifierKeys.Control:                              PasteFromClipboard(); break;
                case Key.N when mods == (ModifierKeys.Control | ModifierKeys.Shift):       CreateNewFolder(); break;
                default: handled = false; break;
            }
            e.Handled = handled;
        }

        // Type the start of a name to jump to it (Explorer's type-ahead)
        private string _typeAhead = "";
        private DateTime _typeAheadAt;

        private void Grid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not DataGrid grid || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
            if ((DateTime.Now - _typeAheadAt).TotalMilliseconds > 1000) _typeAhead = "";
            _typeAheadAt = DateTime.Now;
            _typeAhead += e.Text;

            var match = grid.Items.Cast<object>()
                .FirstOrDefault(i => EntryOf(i) is Entry en && en.Name.StartsWith(_typeAhead, StringComparison.CurrentCultureIgnoreCase));
            if (match != null)
            {
                grid.SelectedItem = match;
                grid.ScrollIntoView(match);
                (grid.ItemContainerGenerator.ContainerFromItem(match) as DataGridRow)?.Focus();
            }
            e.Handled = true;
        }

        private void RefreshView()
        {
            ShellIcons.Invalidate(_currentPath);
            RefreshCurrentTab();
            StatusTotal.Text = "Refreshed.";
        }

        // ── Delete (Recycle Bin / permanent) via the Windows shell ────
        private void DeleteEntry(Entry e, bool permanently)
        {
            try
            {
                // AllDialogs = Windows' own confirmation, progress and error dialogs, as in Explorer
                var recycle = permanently ? VbRecycle.DeletePermanently : VbRecycle.SendToRecycleBin;
                if (e.IsFolder) VbFs.DeleteDirectory(e.Path, VbUi.AllDialogs, recycle);
                else            VbFs.DeleteFile(e.Path, VbUi.AllDialogs, recycle);
                StatusTotal.Text = permanently ? $"Deleted {e.Name}" : $"Moved {e.Name} to the Recycle Bin";
            }
            catch (OperationCanceledException) { StatusTotal.Text = "Delete cancelled."; }
            catch (Exception ex) { StatusTotal.Text = $"Couldn't delete '{e.Name}': {ex.Message}"; }
            RefreshCurrentTab();
        }

        // ── Clipboard (interoperates with File Explorer) ──────────────
        private const int DROPEFFECT_COPY = 1, DROPEFFECT_MOVE = 2;

        private void CopyToClipboard(Entry e, bool cut)
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { e.Path });
            // Explorer reads this to know whether Paste should move (cut) or copy
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(cut ? DROPEFFECT_MOVE : DROPEFFECT_COPY)));
            Clipboard.SetDataObject(data, copy: true);
            StatusTotal.Text = cut ? $"Cut {e.Name} — paste (Ctrl+V) in another folder to move it" : $"Copied {e.Name}";
        }

        private void PasteFromClipboard()
        {
            if (string.IsNullOrEmpty(_currentPath) || !Clipboard.ContainsFileDropList()) return;
            bool move = false;
            try
            {
                if (Clipboard.GetData("Preferred DropEffect") is MemoryStream ms && ms.Length >= 4)
                {
                    var buf = new byte[4];
                    ms.Read(buf, 0, 4);
                    move = (BitConverter.ToInt32(buf, 0) & DROPEFFECT_MOVE) != 0;
                }
            }
            catch { }

            int done = 0;
            foreach (string? src in Clipboard.GetFileDropList())
            {
                if (string.IsNullOrEmpty(src)) continue;
                string dest = Path.Combine(_currentPath, Path.GetFileName(src.TrimEnd('\\')));
                try
                {
                    if (!move && string.Equals(Path.GetDirectoryName(src), _currentPath, StringComparison.OrdinalIgnoreCase))
                        dest = UniqueName(_currentPath, Path.GetFileNameWithoutExtension(src) + " - Copy", Path.GetExtension(src));

                    // Windows' own progress + "replace or skip" dialogs
                    if (Directory.Exists(src))
                    {
                        if (move) VbFs.MoveDirectory(src, dest, VbUi.AllDialogs);
                        else      VbFs.CopyDirectory(src, dest, VbUi.AllDialogs);
                    }
                    else if (File.Exists(src))
                    {
                        if (move) VbFs.MoveFile(src, dest, VbUi.AllDialogs);
                        else      VbFs.CopyFile(src, dest, VbUi.AllDialogs);
                    }
                    done++;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { StatusTotal.Text = $"Couldn't paste '{Path.GetFileName(src)}': {ex.Message}"; }
            }
            if (move && done > 0) Clipboard.Clear(); // a cut can only be pasted once, as in Explorer
            if (done > 0) StatusTotal.Text = $"{(move ? "Moved" : "Pasted")} {done} item(s)";
            RefreshCurrentTab();
        }

        private static string UniqueName(string folder, string baseName, string ext)
        {
            string candidate = Path.Combine(folder, baseName + ext);
            for (int n = 2; File.Exists(candidate) || Directory.Exists(candidate); n++)
                candidate = Path.Combine(folder, $"{baseName} ({n}){ext}");
            return candidate;
        }

        // ── New folder (Ctrl+Shift+N) and rename (F2) ─────────────────
        private void CreateNewFolder()
        {
            if (!Directory.Exists(_currentPath)) return;
            try
            {
                string path = UniqueName(_currentPath, "New folder", "");
                Directory.CreateDirectory(path);
                RefreshCurrentTab();
                RenameEntry(new Entry(path, Path.GetFileName(path), true)); // name it straight away, like Explorer
            }
            catch (Exception ex) { StatusTotal.Text = $"Couldn't create a folder here: {ex.Message}"; }
        }

        private void RenameEntry(Entry e)
        {
            // Rename the real name on disk (not the display name: a localised folder shown as
            // "Documents" may really be "Belgeler", and hidden extensions must be kept)
            string realName = Path.GetFileName(e.Path.TrimEnd('\\'));
            e = e with { Name = realName };
            string? newName = PromptRename(e);
            if (string.IsNullOrWhiteSpace(newName) || newName == realName) return;
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "A file name can't contain any of these characters:\n\\ / : * ? \" < > |", "Rename",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string dest = Path.Combine(Path.GetDirectoryName(e.Path)!, newName);
            try
            {
                if (e.IsFolder) VbFs.RenameDirectory(e.Path, newName);
                else            VbFs.RenameFile(e.Path, newName);
                StatusTotal.Text = $"Renamed to {newName}";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Rename", MessageBoxButton.OK, MessageBoxImage.Warning); }
            RefreshCurrentTab();
            SelectPath(dest);
        }

        /// <summary>Small themed rename box. Selects the name without its extension, like Explorer.</summary>
        private string? PromptRename(Entry e)
        {
            var box = new TextBox { Text = e.Name, MinWidth = 340, Margin = new Thickness(0, 6, 0, 14) };
            var ok = new Button { Content = "Rename", IsDefault = true, MinWidth = 90, Style = (Style)FindResource("Win11AccentButtonStyle") };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
            var label = new TextBlock { Text = e.IsFolder ? "New folder name:" : "New file name:" };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(label);
            panel.Children.Add(box);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok); buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            var dlg = new Window
            {
                Title = "Rename", Owner = this, Content = panel, SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            dlg.SetResourceReference(BackgroundProperty, "Win11BgBrush");
            dlg.SetResourceReference(ForegroundProperty, "Win11TextBrush");
            dlg.SetResourceReference(FontFamilyProperty, "AppFontFamily");
            dlg.SetResourceReference(FontSizeProperty, "Fs13");
            ThemeManager.Register(dlg);

            ok.Click += (_, _) => dlg.DialogResult = true;
            dlg.Loaded += (_, _) =>
            {
                box.Focus();
                int dot = e.IsFolder ? -1 : e.Name.LastIndexOf('.');
                box.Select(0, dot > 0 ? dot : e.Name.Length);
            };
            return dlg.ShowDialog() == true ? box.Text.Trim() : null;
        }

        private void SelectPath(string path)
        {
            if (ActiveGrid is not DataGrid grid) return;
            var item = grid.Items.Cast<object>().FirstOrDefault(i =>
                EntryOf(i) is Entry en && string.Equals(en.Path, path, StringComparison.OrdinalIgnoreCase));
            if (item == null) return;
            grid.SelectedItem = item;
            grid.ScrollIntoView(item);
        }

        // ── Properties (the real Windows dialog) ──────────────────────
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHObjectProperties(IntPtr hwnd, uint shopObjectType, string pszObjectName, string? pszPropertyPage);
        private const uint SHOP_FILEPATH = 0x2;

        private void ShowProperties(string path)
        {
            try
            {
                if (!SHObjectProperties(new WindowInteropHelper(this).Handle, SHOP_FILEPATH, path, null))
                    StatusTotal.Text = $"Couldn't show properties for {path}";
            }
            catch (Exception ex) { StatusTotal.Text = ex.Message; }
        }
    }

    /// <summary>The user's File Explorer options (Folder Options › General / View).</summary>
    public static class ExplorerSettings
    {
        [DllImport("shell32.dll")]
        private static extern void SHGetSettings(out uint lpsfs, uint dwMask);

        // SHELLFLAGSTATE bit positions and SSF_* masks
        private const uint SSF_SHOWALLOBJECTS = 0x0001, SSF_SHOWEXTENSIONS = 0x0002, SSF_DOUBLECLICKINWEBVIEW = 0x0080;
        private const uint BIT_SHOWALLOBJECTS = 1 << 0, BIT_SHOWEXTENSIONS = 1 << 1, BIT_DOUBLECLICKINWEBVIEW = 1 << 5;

        private static uint Read(uint mask)
        {
            if (!OperatingSystem.IsWindows()) return BIT_DOUBLECLICKINWEBVIEW | BIT_SHOWEXTENSIONS;
            try { SHGetSettings(out uint flags, mask); return flags; }
            catch { return BIT_DOUBLECLICKINWEBVIEW; }
        }

        /// <summary>"Single-click to open an item (point to select)" — read live, so changes apply at once.</summary>
        public static bool SingleClickToOpen => (Read(SSF_DOUBLECLICKINWEBVIEW) & BIT_DOUBLECLICKINWEBVIEW) == 0;

        /// <summary>Explorer › View › Show › Hidden items.</summary>
        public static bool ShowHidden => (Read(SSF_SHOWALLOBJECTS) & BIT_SHOWALLOBJECTS) != 0;

        /// <summary>Explorer › View › Show › File name extensions.</summary>
        public static bool ShowExtensions => (Read(SSF_SHOWEXTENSIONS) & BIT_SHOWEXTENSIONS) != 0;
    }
}
