using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using FolderDiag.Wpf.Settings;

namespace FolderDiag.Wpf
{
    /// <summary>
    /// View tab › Layout (Details / List / Tiles / Icons) and Panes (chart, details pane),
    /// like File Explorer. Hiding a pane gives its space back to the list: the chart sits
    /// under the list (hiding it adds height) and the details pane is a full-height column
    /// on the right (hiding it adds width). Choices are remembered between runs.
    /// </summary>
    public partial class MainWindow
    {
        private enum ItemsView { Details, List, Tiles, Icons }
        private ItemsView _itemsView = ItemsView.Details;

        // Sizes to restore when a hidden pane is shown again (the splitters change them)
        private GridLength _chartHeight  = new(240);
        private GridLength _detailsWidth = new(260);

        private void InitLayoutViews()
        {
            // The List/Tiles/Icons lists show the same items (and sort order) as the grids and
            // share their selection, so everything that reads FoldersGrid/FileBrowserGrid
            // .SelectedItem (context menus, ribbon commands, details pane) keeps working.
            SetupViewList(FoldersList, FoldersGrid, _folderItems);
            SetupViewList(FileBrowserList, FileBrowserGrid, _browserItems);

            var s = SettingsService.Current;
            ChkShowChart.IsChecked       = s.ShowChartPane;
            ChkShowDetailsPane.IsChecked = s.ShowDetailsPane;
            ApplyPanes();
            SetItemsView(Enum.TryParse<ItemsView>(s.ItemsView, true, out var v) ? v : ItemsView.Details, save: false);

            // Alt+Shift+P toggles the details pane, as in File Explorer
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.System && e.SystemKey == Key.P && Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
                {
                    ChkShowDetailsPane.IsChecked = ChkShowDetailsPane.IsChecked != true;
                    e.Handled = true;
                }
            };
        }

        private static void SetupViewList(ListBox list, DataGrid grid, System.Collections.IEnumerable items)
        {
            list.ItemsSource = items;
            list.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                new Binding(nameof(DataGrid.SelectedItem)) { Source = grid, Mode = BindingMode.TwoWay });
            list.ContextMenu = grid.ContextMenu;
        }

        // ── Layout: Details / List / Tiles / Icons ────────────────────
        private void BtnViewDetails_Click(object sender, RoutedEventArgs e) => SetItemsView(ItemsView.Details);
        private void BtnViewList_Click(object sender, RoutedEventArgs e)    => SetItemsView(ItemsView.List);
        private void BtnViewTiles_Click(object sender, RoutedEventArgs e)   => SetItemsView(ItemsView.Tiles);
        private void BtnViewIcons_Click(object sender, RoutedEventArgs e)   => SetItemsView(ItemsView.Icons);

        private void SetItemsView(ItemsView view, bool save = true)
        {
            _itemsView = view;
            bool details = view == ItemsView.Details;
            var gridVis = details ? Visibility.Visible : Visibility.Collapsed;
            var listVis = details ? Visibility.Collapsed : Visibility.Visible;
            FoldersGrid.Visibility = FileBrowserGrid.Visibility = gridVis;
            FoldersList.Visibility = FileBrowserList.Visibility = listVis;

            if (!details)
            {
                ConfigureViewList(FoldersList, view, "Folder");
                ConfigureViewList(FileBrowserList, view, "Browser");
            }

            // Highlight the current layout button
            foreach (var (btn, v) in new[] { (BtnViewDetails, ItemsView.Details), (BtnViewList, ItemsView.List),
                                             (BtnViewTiles, ItemsView.Tiles), (BtnViewIcons, ItemsView.Icons) })
            {
                if (v == view) btn.SetResourceReference(BackgroundProperty, "Win11SelectBrush");
                else btn.ClearValue(BackgroundProperty);
            }

            // Keep the selected item in view in the new layout
            if (ActiveGrid is { SelectedItem: not null } active)
                Dispatcher.InvokeAsync(() =>
                {
                    if (active.SelectedItem is object item) ScrollItemIntoView(active, item);
                }, DispatcherPriority.Loaded);

            if (save)
            {
                SettingsService.Current.ItemsView = view.ToString();
                SettingsService.Save(SettingsService.Current);
            }
        }

        private void ConfigureViewList(ListBox list, ItemsView view, string prefix)
        {
            list.ItemTemplate = (DataTemplate)FindResource($"{prefix}{view}Template");
            // List: columns that scroll sideways (like Explorer). Tiles/Icons: rows that scroll down.
            bool columns = view == ItemsView.List;
            list.ItemsPanel = (ItemsPanelTemplate)FindResource(columns ? "ViewColumnsPanel" : "ViewRowsPanel");
            ScrollViewer.SetHorizontalScrollBarVisibility(list, columns ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(list, columns ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        }

        private void ViewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Don't let the list's SelectionChanged bubble up to the tab control
            e.Handled = true;
            if (ReferenceEquals(sender, FoldersList)) OnFolderSelected();
            else if (ReferenceEquals(sender, FileBrowserList)) OnBrowserSelected();
        }

        // ── Panes: chart and details pane ─────────────────────────────
        private void ChkPanes_Changed(object sender, RoutedEventArgs e)
        {
            // Also fires during InitializeComponent (IsChecked="True") before the grid exists
            if (!IsLoaded) return;
            ApplyPanes();
            SettingsService.Current.ShowChartPane   = ChkShowChart.IsChecked == true;
            SettingsService.Current.ShowDetailsPane = ChkShowDetailsPane.IsChecked == true;
            SettingsService.Save(SettingsService.Current);
        }

        private void ApplyPanes()
        {
            bool chart   = ChkShowChart.IsChecked == true;
            bool details = ChkShowDetailsPane.IsChecked == true;

            // Chart row under the list: hidden → the list gets the full height
            if (chart)
            {
                if (ChartRow.Height.Value == 0) ChartRow.Height = _chartHeight;
                ChartRow.MinHeight = 120;
                ChartSplitterRow.Height = new GridLength(4);
            }
            else
            {
                if (ChartRow.Height.Value > 0) _chartHeight = ChartRow.Height;
                ChartRow.MinHeight = 0;
                ChartRow.Height = new GridLength(0);
                ChartSplitterRow.Height = new GridLength(0);
            }
            ChartPane.Visibility = ChartSplitter.Visibility = chart ? Visibility.Visible : Visibility.Collapsed;

            // Details column on the right: hidden → the list (and chart) get the full width
            if (details)
            {
                if (DetailsCol.Width.Value == 0) DetailsCol.Width = _detailsWidth;
                DetailsCol.MinWidth = 160;
                DetailsSplitterCol.Width = new GridLength(4);
            }
            else
            {
                if (DetailsCol.Width.Value > 0) _detailsWidth = DetailsCol.Width;
                DetailsCol.MinWidth = 0;
                DetailsCol.Width = new GridLength(0);
                DetailsSplitterCol.Width = new GridLength(0);
            }
            DetailsPane.Visibility = DetailsSplitter.Visibility = details ? Visibility.Visible : Visibility.Collapsed;

            // The chart is laid out from its size, so redraw once the new layout has settled
            if (chart && IsLoaded) Dispatcher.InvokeAsync(DrawChart, DispatcherPriority.Loaded);
        }
    }
}
