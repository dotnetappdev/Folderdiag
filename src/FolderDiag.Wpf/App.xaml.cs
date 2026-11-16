using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using FolderDiag.Wpf.Settings;
using FolderDiag.Wpf.Theming;

// Security: load native DLLs used via P/Invoke (shell32, user32, dwmapi, shlwapi) only from
// System32, never from the app folder or current directory. Prevents DLL planting — e.g. a
// malicious dwmapi.dll dropped next to the portable .exe in a Downloads folder.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace FolderDiag.Wpf
{
    public partial class App : Application
    {
        /// <summary>
        /// Full path to Windows' own explorer.exe. Starting "explorer.exe" by name searches the
        /// app's folder first, so a planted explorer.exe next to the portable app would run.
        /// </summary>
        public static string ExplorerExe { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows) is { Length: > 0 } win ? win : @"C:\Windows", "explorer.exe");

        public static string LogFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderDiag", "logs");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Never lose the app to an unexpected error: log it and tell the user instead
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception, "AppDomain");
            TaskScheduler.UnobservedTaskException += (_, args) => { Log(args.Exception, "Task"); args.SetObserved(); };

            base.OnStartup(e);
            // Apply theme/fonts before the first window is created so it never flashes the wrong theme
            ThemeManager.Initialize(SettingsService.Load());
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ThemeManager.Shutdown();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string? logFile = Log(e.Exception, "UI");
            e.Handled = true; // keep running
            try
            {
                MessageBox.Show(
                    $"Something went wrong, but FolderDiag is still running.\n\n{e.Exception.GetType().Name}: {e.Exception.Message}" +
                    (logFile != null ? $"\n\nDetails were saved to:\n{logFile}" : ""),
                    "FolderDiag", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { /* never throw from the handler */ }
        }

        /// <summary>Appends the error to a daily log in %LocalAppData%\FolderDiag\logs. Returns the file path.</summary>
        public static string? Log(Exception? ex, string source)
        {
            if (ex == null) return null;
            try
            {
                Directory.CreateDirectory(LogFolder);
                string file = Path.Combine(LogFolder, $"error-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, $"[{DateTime.Now:O}] ({source}) {ex}\n\n");

                // Keep the folder small: delete logs older than 14 days
                foreach (var old in new DirectoryInfo(LogFolder).EnumerateFiles("error-*.log")
                                                               .Where(f => f.LastWriteTime < DateTime.Now.AddDays(-14)))
                    try { old.Delete(); } catch { }
                return file;
            }
            catch { return null; }
        }
    }
}
