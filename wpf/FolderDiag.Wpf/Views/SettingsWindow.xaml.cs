using System.Windows;
using FolderDiag.Wpf.ViewModels;

namespace FolderDiag.Wpf.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
