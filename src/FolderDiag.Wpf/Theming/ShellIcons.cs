using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderDiag.Wpf.Theming
{
    /// <summary>
    /// The real Windows icons for drives, folders and files (the same ones File Explorer shows),
    /// taken from the shell with SHGetFileInfo and cached.
    /// <list type="bullet">
    /// <item>Drives, special folders (Desktop, Downloads…), cloud roots (OneDrive…) and
    ///       .exe/.lnk/.ico files are looked up by their real path — they have their own icons.</item>
    /// <item>Ordinary folders share one folder icon, and other files share one icon per
    ///       extension; these are looked up without touching the disk, so big lists stay fast.</item>
    /// </list>
    /// Icons are fetched at 32 px and shown at 16 DIP, so they stay crisp at 150–200% scaling.
    /// Must be called on the UI thread (the shell needs an STA thread).
    /// </summary>
    public static class ShellIcons
    {
        private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PerFileIconExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".ico", ".url", ".appref-ms", ".cur", ".ani" };
        private static HashSet<string>? _specialFolders;

        /// <summary>Icon for a folder, drive or file. Returns null if Windows can't supply one.</summary>
        public static ImageSource? Get(string? path, bool isDirectory)
        {
            if (string.IsNullOrEmpty(path)) return isDirectory ? Folder : null;

            if (isDirectory)
            {
                string norm = path.Length > 3 ? path.TrimEnd('\\', '/') : path;
                bool isRoot = norm.Length <= 3 && norm.Length >= 2 && norm[1] == ':';
                if (isRoot || IsSpecialFolder(norm))
                    return Cache.GetOrAdd("path:" + norm, _ => Load(isRoot && !norm.EndsWith('\\') ? norm + "\\" : norm, 0, useAttributes: false));
                return Folder;
            }

            string ext = Path.GetExtension(path);
            if (PerFileIconExtensions.Contains(ext))
                return Cache.GetOrAdd("file:" + path, _ => Load(path, FILE_ATTRIBUTE_NORMAL, useAttributes: false)
                                                            ?? ForExtension(ext));
            return ForExtension(ext);
        }

        /// <summary>The standard closed folder icon.</summary>
        public static ImageSource? Folder
            => Cache.GetOrAdd("folder", _ => Load("folder", FILE_ATTRIBUTE_DIRECTORY, useAttributes: true));

        /// <summary>Icon for a file type, e.g. ".pdf" (no disk access).</summary>
        public static ImageSource? ForExtension(string? ext)
        {
            ext = string.IsNullOrEmpty(ext) ? "" : ext.StartsWith('.') ? ext : "." + ext;
            return Cache.GetOrAdd("ext:" + ext, _ => Load("file" + ext, FILE_ATTRIBUTE_NORMAL, useAttributes: true));
        }

        /// <summary>A representative file-type icon for FolderDiag's file categories.</summary>
        public static ImageSource? ForCategory(string? category) => category switch
        {
            "Images"      => ForExtension(".jpg"),
            "Video"       => ForExtension(".mp4"),
            "Audio"       => ForExtension(".mp3"),
            "Documents"   => ForExtension(".docx"),
            "Archives"    => ForExtension(".zip"),
            "Executables" => ForExtension(".dll"),
            "Source Code" => ForExtension(".cs"),
            "Temporary"   => ForExtension(".tmp"),
            "Config"      => ForExtension(".ini"),
            "Text"        => ForExtension(".txt"),
            _             => ForExtension(""),
        };

        private static bool IsSpecialFolder(string path)
        {
            _specialFolders ??= BuildSpecialFolders();
            return _specialFolders.Contains(path);
        }

        private static HashSet<string> BuildSpecialFolders()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string? p) { if (!string.IsNullOrEmpty(p)) set.Add(p.TrimEnd('\\')); }

            foreach (Environment.SpecialFolder f in Enum.GetValues<Environment.SpecialFolder>())
                try { Add(Environment.GetFolderPath(f)); } catch { }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var name in new[] { "Downloads", "Saved Games", "Contacts", "Links", "Searches", "3D Objects",
                                         "OneDrive", "Dropbox", "Google Drive", "iCloudDrive", "Box" })
                Add(Path.Combine(profile, name));
            Add(Environment.GetEnvironmentVariable("OneDrive"));
            Add(Environment.GetEnvironmentVariable("OneDriveConsumer"));
            Add(Environment.GetEnvironmentVariable("OneDriveCommercial"));
            return set;
        }

        // ── Win32 ─────────────────────────────────────────────────────
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static ImageSource? Load(string path, uint attributes, bool useAttributes)
        {
            if (!OperatingSystem.IsWindows()) return null;
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON | (useAttributes ? SHGFI_USEFILEATTRIBUTES : 0);
            try
            {
                if (SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero
                    || info.hIcon == IntPtr.Zero)
                    return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze(); // shareable across threads/elements
                return src;
            }
            catch { return null; }
            finally
            {
                if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon);
            }
        }
    }

    /// <summary>
    /// Binds a path to its Windows icon. ConverterParameter: "folder" (path is a folder),
    /// "file" (path is a file), or "category" (value is a FolderDiag file category).
    /// </summary>
    public sealed class ShellIconConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? s = value?.ToString();
            return (parameter as string) switch
            {
                "category" => ShellIcons.ForCategory(s),
                "file"     => ShellIcons.Get(s, isDirectory: false),
                _          => ShellIcons.Get(s, isDirectory: true),
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
