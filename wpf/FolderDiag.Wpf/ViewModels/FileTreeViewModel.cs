using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.ViewModels;

public sealed partial class FileTreeViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<FileSystemEntry> _rootItems = [];
    [ObservableProperty] private FileSystemEntry? _selectedEntry;

    public event Action<FileSystemEntry>? EntrySelected;

    public void SetRoot(FileSystemEntry root)
    {
        RootItems.Clear();
        RootItems.Add(root);
        root.IsExpanded = true;
    }

    public void SelectEntry(FileSystemEntry entry)
    {
        if (SelectedEntry != null) SelectedEntry.IsSelected = false;
        SelectedEntry = entry;
        entry.IsSelected = true;
    }

    [RelayCommand]
    private void Select(FileSystemEntry entry)
    {
        SelectEntry(entry);
        EntrySelected?.Invoke(entry);
    }
}
