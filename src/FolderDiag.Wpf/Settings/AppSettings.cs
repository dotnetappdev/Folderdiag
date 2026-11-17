using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderDiag.Wpf.Settings
{
    public enum ThemeChoice { System, Light, Dark, HighContrastDark, HighContrastLight }

    /// <summary>
    /// User preferences. Defaults follow Windows 11 (theme, accent, text size,
    /// high contrast), so a fresh install matches whatever the user picked in Windows.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Special colour value meaning "use the current accent colour".</summary>
        public const string AccentToken = "accent";

        // ── Appearance ────────────────────────────────────────────────
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThemeChoice Theme { get; set; } = ThemeChoice.System;
        public bool   UseWindowsAccent   { get; set; } = true;
        public string AccentColor        { get; set; } = "#0078D4";
        public bool   FollowHighContrast { get; set; } = true;

        // ── Fonts & accessibility ─────────────────────────────────────
        // Each "UseWindows…" switch makes the app follow Windows; when off, the
        // value next to it is used instead.
        public bool   UseWindowsScaling  { get; set; } = true;   // Settings › Display › Scale
        public double UiScale            { get; set; } = 1.0;    // 0.8 – 2.0 (when not using Windows scaling)
        public bool   UseWindowsFont     { get; set; } = true;   // Windows UI font
        public string FontFamily         { get; set; } = "Segoe UI";
        public bool   UseWindowsFontSize { get; set; } = true;   // Settings › Accessibility › Text size
        public double FontSize           { get; set; } = 13;     // base size 9 – 24 (when not using Windows size)

        // ── Bar colours ───────────────────────────────────────────────
        public string DiskNormalColor     { get; set; } = AccentToken;
        public string DiskWarningColor    { get; set; } = "#F7A400";
        public string DiskCriticalColor   { get; set; } = "#E81123";
        public int    DiskWarningPercent  { get; set; } = 75;
        public int    DiskCriticalPercent { get; set; } = 90;

        public string FolderLowColor      { get; set; } = "#3DBE6C";
        public string FolderMediumColor   { get; set; } = "#F5A623";
        public string FolderHighColor     { get; set; } = "#E84E4E";
        public int    FolderMediumPercent { get; set; } = 50;
        public int    FolderHighPercent   { get; set; } = 80;

        // ── General ───────────────────────────────────────────────────
        public bool AutoScanOnNavigate { get; set; } = true;
        public bool ParallelScanning   { get; set; } = true;   // read several folders at once (SSD/NVMe)
        public bool ReadOwners         { get; set; } = true;   // needed for "Files by Owner" / owner search

        // ── Layout (View tab) ─────────────────────────────────────────
        public bool   ShowChartPane   { get; set; } = true;
        public bool   ShowDetailsPane { get; set; } = true;
        public string ItemsView       { get; set; } = "Details";   // Details | List | Tiles | Icons

        public AppSettings Clone()
            => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new AppSettings();

        /// <summary>Clamp anything out of range (e.g. hand-edited settings.json).</summary>
        public void Normalize()
        {
            UiScale  = Math.Round(Math.Clamp(UiScale, 0.8, 2.0), 2);
            FontSize = Math.Clamp(FontSize, 9, 24);
            if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";

            DiskWarningPercent  = Math.Clamp(DiskWarningPercent, 10, 98);
            DiskCriticalPercent = Math.Clamp(DiskCriticalPercent, DiskWarningPercent + 1, 99);
            FolderMediumPercent = Math.Clamp(FolderMediumPercent, 5, 98);
            FolderHighPercent   = Math.Clamp(FolderHighPercent, FolderMediumPercent + 1, 99);
        }
    }
}
