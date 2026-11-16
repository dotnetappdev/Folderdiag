using System.Windows;
using FolderDiag.Wpf.Settings;
using FolderDiag.Wpf.Theming;

namespace FolderDiag.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Apply theme/fonts before the first window is created so it never flashes the wrong theme
            ThemeManager.Initialize(SettingsService.Load());
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ThemeManager.Shutdown();
            base.OnExit(e);
        }
    }
}
