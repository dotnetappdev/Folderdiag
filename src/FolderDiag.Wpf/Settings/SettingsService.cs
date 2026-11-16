using System.IO;
using System.Text.Json;

namespace FolderDiag.Wpf.Settings
{
    /// <summary>Loads/saves settings as JSON in %LocalAppData%\FolderDiag\settings.json.</summary>
    public static class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderDiag", "settings.json");

        public static AppSettings Current { get; private set; } = new();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new AppSettings();
            }
            catch
            {
                Current = new AppSettings(); // corrupt file: fall back to defaults
            }
            Current.Normalize();
            return Current;
        }

        public static void Save(AppSettings settings)
        {
            settings.Normalize();
            Current = settings;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                // Write to a temp file first so a crash can't leave a half-written settings file
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOpts));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch { /* settings are best-effort; never crash the app over them */ }
        }
    }
}
