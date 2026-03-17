using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderDiag.Core.Models;
using FolderDiag.Wpf.ViewModels;

namespace FolderDiag.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Wire up control events
        BarChart.EntryClicked += (_, e) => _vm.NavigateToCommand.Execute(e);
        Treemap.EntryClicked += (_, e) => _vm.NavigateToCommand.Execute(e);
        Treemap.EntryDoubleClicked += (_, e) => _vm.NavigateToCommand.Execute(e);
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await _vm.InitializeAsync();
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileSystemEntry entry && entry.IsDirectory)
        {
            _vm.NavigateToCommand.Execute(entry);
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.SearchCommand.Execute(null);
        else if (e.Key == Key.Escape)
            _vm.ClearSearchCommand.Execute(null);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_vm.SettingsVm) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _vm.Settings = _vm.SettingsVm.ToSettings();
            _vm.SettingsVm.SaveCommand.Execute(null);
        }
    }
}
