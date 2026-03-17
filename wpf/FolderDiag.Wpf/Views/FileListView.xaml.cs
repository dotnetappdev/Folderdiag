using System.Windows.Controls;
using System.Windows.Input;
using FolderDiag.Core.Models;
using FolderDiag.Wpf.ViewModels;

namespace FolderDiag.Wpf.Views;

public partial class FileListView : UserControl
{
    public FileListView()
    {
        InitializeComponent();
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ListView lv)
        {
            vm.SelectedCount = lv.SelectedItems.Count;
            if (lv.SelectedItems.Count == 1 && lv.SelectedItem is FileSystemEntry entry)
                vm.SelectedEntry = entry;
        }
    }

    private void FileListView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm &&
            sender is ListView lv &&
            lv.SelectedItem is FileSystemEntry entry)
        {
            if (entry.IsDirectory)
                vm.NavigateToCommand.Execute(entry);
            else
                vm.OpenInExplorerCommand.Execute(entry);
        }
    }
}
