using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace FolderDiag.Wpf.Theming
{
    /// <summary>Reads the user's Windows 11 personalisation and accessibility settings.</summary>
    public static class WindowsTheme
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AccentKey      = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
        private const string DwmKey         = @"Software\Microsoft\Windows\DWM";
        private const string AccessKey      = @"Software\Microsoft\Accessibility";

        /// <summary>Settings › Personalisation › Colours › "Choose your default app mode".</summary>
        public static bool AppsUseLightTheme
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                    return k?.GetValue("AppsUseLightTheme") is not int v || v != 0;
                }
                catch { return true; }
            }
        }

        /// <summary>Settings › Accessibility › Contrast themes.</summary>
        public static bool HighContrast => SystemParameters.HighContrast;

        /// <summary>Settings › Accessibility › Text size (1.0 – 2.25).</summary>
        public static double TextScaleFactor
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(AccessKey);
                    if (k?.GetValue("TextScaleFactor") is int v && v >= 100 && v <= 225) return v / 100.0;
                }
                catch { }
                return 1.0;
            }
        }

        /// <summary>
        /// Settings › Personalisation › Colours › Accent colour.
        /// Returns the 8-shade palette Windows generates (Light3..Dark3) when available.
        /// </summary>
        public static (Color accent, Color light1, Color light2, Color dark1) Accent
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(AccentKey);
                    if (k?.GetValue("AccentPalette") is byte[] p && p.Length >= 32)
                    {
                        Color At(int i) => Color.FromRgb(p[i * 4], p[i * 4 + 1], p[i * 4 + 2]);
                        // 0 Light3, 1 Light2, 2 Light1, 3 Accent, 4 Dark1, 5 Dark2, 6 Dark3
                        return (At(3), At(2), At(1), At(4));
                    }
                }
                catch { }

                Color c = Color.FromRgb(0x00, 0x78, 0xD4);
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(DwmKey);
                    if (k?.GetValue("AccentColor") is int abgr)
                        c = Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
                }
                catch { }
                return (c, ColorUtil.Mix(c, Colors.White, 0.25), ColorUtil.Mix(c, Colors.White, 0.45), ColorUtil.Mix(c, Colors.Black, 0.2));
            }
        }

        // ── Title bar ─────────────────────────────────────────────────
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809–1909
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE     = 20; // Windows 10 2004+ / Windows 11

        /// <summary>Makes the native title bar dark or light to match the app theme.</summary>
        public static void SetTitleBarDark(Window window, bool dark)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref v, sizeof(int));
            }
            catch { /* older Windows: ignore */ }
        }
    }

    public static class ColorUtil
    {
        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
        public static double Luminance(Color c)
        {
            static double Ch(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        /// <summary>Black or white, whichever reads better on <paramref name="bg"/>.</summary>
        public static Color ReadableOn(Color bg) => Luminance(bg) > 0.4 ? Colors.Black : Colors.White;

        public static bool TryParse(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try { color = (Color)ColorConverter.ConvertFromString(text.Trim()); return true; }
            catch { return false; }
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
