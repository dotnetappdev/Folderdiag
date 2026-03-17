using System.IO;
using System.Windows;
using FolderDiag.Core.Services;
using FolderDiag.Data;
using FolderDiag.Data.Repositories;
using FolderDiag.Wpf.ViewModels;
using FolderDiag.Wpf.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FolderDiag.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .ConfigureLogging(logging =>
            {
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await _host.StartAsync();

        // Initialize database
        var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FolderDiag",
            "folderdiag.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<DatabaseInitializer>();

        // Repositories
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IScanHistoryRepository, ScanHistoryRepository>();

        // Core services
        services.AddSingleton<IFileSystemScanner, FileSystemScanner>();
        services.AddSingleton<ISearchService, SearchService>();

        // ViewModels
        services.AddTransient<FileTreeViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
