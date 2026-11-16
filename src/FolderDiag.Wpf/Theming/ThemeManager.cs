using System.Windows;
using System.Windows.Media;
using FolderDiag.Wpf.Settings;
using Microsoft.Win32;

namespace FolderDiag.Wpf.Theming
{
    /// <summary>
    /// Applies <see cref="AppSettings"/> to the running app: Light/Dark/High-contrast palette,
    /// accent colour, fonts, UI scale and title bars. Every themed key in Themes/ModernTheme.xaml
    /// is overwritten in Application.Resources, so all DynamicResource references update live.
    /// Also tracks Windows: changing app mode, accent, text size or contrast theme in
    /// Windows Settings re-applies automatically when the app is set to follow Windows.
    /// </summary>
    public static class ThemeManager
    {
        private static AppSettings _settings = new();
        private static bool _hooked;

        /// <summary>Raised after colours/fonts change (code-drawn visuals such as charts should redraw).</summary>
        public static event EventHandler? ThemeChanged;

        public static bool IsDark { get; private set; }
        public static bool IsHighContrast { get; private set; }
        public static Color Accent { get; private set; } = Color.FromRgb(0x00, 0x78, 0xD4);
        public static AppSettings Settings => _settings;

        public static void Initialize(AppSettings settings)
        {
            if (!_hooked)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
                _hooked = true;
            }
            Apply(settings);
        }

        public static void Shutdown()
        {
            if (!_hooked) return;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
            _hooked = false;
        }

        /// <summary>Applies settings to every open window (used for live preview too).</summary>
        public static void Apply(AppSettings settings)
        {
            _settings = settings;
            var res = Application.Current.Resources;

            IsHighContrast = settings.FollowHighContrast && WindowsTheme.HighContrast;
            IsDark = settings.Theme switch
            {
                ThemeChoice.Light => false,
                ThemeChoice.Dark  => true,
                _                 => !WindowsTheme.AppsUseLightTheme,
            };

            var palette = IsHighContrast ? HighContrastPalette() : IsDark ? DarkPalette(settings) : LightPalette(settings);
            foreach (var (key, color) in palette)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                res[key] = brush;
            }

            ApplyFonts(res, settings);

            foreach (Window w in Application.Current.Windows)
                ApplyToWindow(w);

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Call from a window's constructor so it picks up scale + title bar.</summary>
        public static void Register(Window window)
        {
            window.SourceInitialized += (_, _) => WindowsTheme.SetTitleBarDark(window, IsDark && !IsHighContrast);
            window.Loaded += (_, _) => ApplyToWindow(window);
        }

        private static void ApplyToWindow(Window w)
        {
            WindowsTheme.SetTitleBarDark(w, IsDark && !IsHighContrast);
            if (w.Content is FrameworkElement root)
            {
                // Windows display scaling is applied by WPF itself (per-monitor DPI aware),
                // so "Use Windows scaling" means no extra zoom on top.
                double s = EffectiveUiScale(_settings);
                root.LayoutTransform = Math.Abs(s - 1.0) < 0.001 ? Transform.Identity : new ScaleTransform(s, s);
            }
        }

        // ── Fonts ─────────────────────────────────────────────────────
        public static double EffectiveUiScale(AppSettings s) => s.UseWindowsScaling ? 1.0 : s.UiScale;

        /// <summary>13 px is the app's 100% size; Windows Text size scales it up to 225%.</summary>
        public static double EffectiveFontScale(AppSettings s)
            => s.UseWindowsFontSize ? WindowsTheme.TextScaleFactor : s.FontSize / 13.0;

        public static FontFamily EffectiveFontFamily(AppSettings s)
        {
            if (s.UseWindowsFont) return WindowsTheme.UiFontFamily;
            try { return new FontFamily(s.FontFamily); } catch { return new FontFamily("Segoe UI"); }
        }

        private static void ApplyFonts(ResourceDictionary res, AppSettings s)
        {
            double f = EffectiveFontScale(s);
            foreach (int n in new[] { 10, 11, 12, 13, 14, 15, 16, 20 })
                res[$"Fs{n}"] = Math.Round(n * f, 1);

            res["RowHeight"]    = Math.Max(28, Math.Round(28 * f));
            res["HeaderHeight"] = Math.Max(30, Math.Round(30 * f));
            res["InputHeight"]  = Math.Max(28, Math.Round(28 * f));

            res["AppFontFamily"] = EffectiveFontFamily(s);
        }

        // ── Accent ────────────────────────────────────────────────────
        private static (Color accent, Color hover, Color press) ResolveAccent(AppSettings s, bool dark)
        {
            if (s.UseWindowsAccent)
            {
                var w = WindowsTheme.Accent;
                // Windows 11 uses lighter shades on dark backgrounds and darker ones on light
                return dark ? (w.light2, w.light1, w.accent) : (w.dark1, w.accent, ColorUtil.Mix(w.dark1, Colors.Black, 0.15));
            }

            if (!ColorUtil.TryParse(s.AccentColor, out var c)) c = Color.FromRgb(0x00, 0x78, 0xD4);
            return dark
                ? (c, ColorUtil.Mix(c, Colors.White, 0.12), ColorUtil.Mix(c, Colors.Black, 0.15))
                : (c, ColorUtil.Mix(c, Colors.White, 0.10), ColorUtil.Mix(c, Colors.Black, 0.15));
        }

        private static IEnumerable<(string, Color)> AccentKeys(Color accent, Color hover, Color press, Color panel, Color text, bool dark)
        {
            Accent = accent;
            var select = ColorUtil.Mix(panel, accent, dark ? 0.38 : 0.22);
            yield return ("Win11AccentBrush", accent);
            yield return ("Win11AccentHoverBrush", hover);
            yield return ("Win11AccentPressBrush", press);
            yield return ("Win11AccentTextBrush", ColorUtil.ReadableOn(accent));
            yield return ("Win11SelectBrush", select);
            yield return ("Win11SelectTextBrush", text);
        }

        private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        // ── Palettes (Windows 11 Fluent values) ───────────────────────
        private static List<(string, Color)> LightPalette(AppSettings s)
        {
            var panel = C(0xFFFFFF); var text = C(0x1A1A1A);
            var (a, h, p) = ResolveAccent(s, false);
            var list = new List<(string, Color)>
            {
                ("Win11BgBrush",               C(0xF3F3F3)),
                ("Win11PanelBrush",            panel),
                ("Win11SidebarBrush",          C(0xF0F0F0)),
                ("Win11RibbonBrush",           C(0xFAFAFA)),
                ("Win11BorderBrush",           C(0xE0E0E0)),
                ("Win11TextBrush",             text),
                ("Win11SubTextBrush",          C(0x5F5F5F)),
                ("Win11HoverBrush",            C(0xE9E9E9)),
                ("Win11PressedBrush",          C(0xDADADA)),
                ("Win11HeaderBgBrush",         C(0xF0F0F0)),
                ("Win11StatusBarBrush",        C(0xF0F0F0)),
                ("Win11GridLineBrush",         C(0xE8E8E8)),
                ("Win11AltRowBrush",           C(0xF9F9F9)),
                ("Win11InputHoverBorderBrush", C(0x8A8A8A)),
                ("Win11ScrollThumbBrush",      C(0xB5B5B5)),
                ("Win11PlaceholderBrush",      C(0x767676)),
                ("Win11ChartBgBrush",          panel),
            };
            list.AddRange(AccentKeys(a, h, p, panel, text, false));
            return list;
        }

        private static List<(string, Color)> DarkPalette(AppSettings s)
        {
            var panel = C(0x2B2B2B); var text = C(0xF3F3F3);
            var (a, h, p) = ResolveAccent(s, true);
            var list = new List<(string, Color)>
            {
                ("Win11BgBrush",               C(0x202020)),
                ("Win11PanelBrush",            panel),
                ("Win11SidebarBrush",          C(0x1C1C1C)),
                ("Win11RibbonBrush",           C(0x272727)),
                ("Win11BorderBrush",           C(0x3D3D3D)),
                ("Win11TextBrush",             text),
                ("Win11SubTextBrush",          C(0xC5C5C5)),
                ("Win11HoverBrush",            C(0x383838)),
                ("Win11PressedBrush",          C(0x454545)),
                ("Win11HeaderBgBrush",         C(0x323232)),
                ("Win11StatusBarBrush",        C(0x1C1C1C)),
                ("Win11GridLineBrush",         C(0x363636)),
                ("Win11AltRowBrush",           C(0x303030)),
                ("Win11InputHoverBorderBrush", C(0x7A7A7A)),
                ("Win11ScrollThumbBrush",      C(0x5E5E5E)),
                ("Win11PlaceholderBrush",      C(0x9E9E9E)),
                ("Win11ChartBgBrush",          panel),
            };
            list.AddRange(AccentKeys(a, h, p, panel, text, true));
            return list;
        }

        /// <summary>Maps to the user's chosen Windows contrast theme (Aquatic, Desert, Dusk, Night sky…).</summary>
        private static List<(string, Color)> HighContrastPalette()
        {
            var win = SystemColors.WindowColor; var text = SystemColors.WindowTextColor;
            var hi = SystemColors.HighlightColor; var hiText = SystemColors.HighlightTextColor;
            Accent = hi;
            IsDark = ColorUtil.Luminance(win) < 0.3;
            return new List<(string, Color)>
            {
                ("Win11BgBrush", win), ("Win11PanelBrush", win), ("Win11SidebarBrush", win),
                ("Win11RibbonBrush", win), ("Win11HeaderBgBrush", win), ("Win11StatusBarBrush", win),
                ("Win11ChartBgBrush", win), ("Win11AltRowBrush", win),
                ("Win11BorderBrush", text), ("Win11GridLineBrush", SystemColors.GrayTextColor),
                ("Win11InputHoverBorderBrush", hi), ("Win11ScrollThumbBrush", text),
                ("Win11TextBrush", text), ("Win11SubTextBrush", text), ("Win11PlaceholderBrush", SystemColors.GrayTextColor),
                ("Win11HoverBrush", SystemColors.ControlColor), ("Win11PressedBrush", SystemColors.ControlDarkColor),
                ("Win11AccentBrush", hi), ("Win11AccentHoverBrush", hi), ("Win11AccentPressBrush", hi),
                ("Win11AccentTextBrush", hiText),
                ("Win11SelectBrush", hi), ("Win11SelectTextBrush", hiText),
            };
        }

        // ── Bar colours (disk space + folder %) ───────────────────────
        public static Color ResolveColor(string value)
            => value == AppSettings.AccentToken ? Accent
             : ColorUtil.TryParse(value, out var c) ? c : Accent;

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        public static Brush DiskBarBrush(double usedPct)
        {
            if (IsHighContrast) return (Brush)Application.Current.Resources["Win11AccentBrush"];
            var s = _settings;
            return Frozen(ResolveColor(usedPct >= s.DiskCriticalPercent ? s.DiskCriticalColor
                                     : usedPct >= s.DiskWarningPercent  ? s.DiskWarningColor
                                     : s.DiskNormalColor));
        }

        public static Brush FolderBarBrush(double pct)
        {
            if (IsHighContrast) return (Brush)Application.Current.Resources["Win11AccentBrush"];
            var s = _settings;
            return Frozen(ResolveColor(pct >= s.FolderHighPercent   ? s.FolderHighColor
                                     : pct >= s.FolderMediumPercent ? s.FolderMediumColor
                                     : s.FolderLowColor));
        }

        // ── Follow Windows live ───────────────────────────────────────
        private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            // Theme/accent changes arrive as General/Color/VisualStyle; text size as Accessibility/Window
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
                or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Accessibility
                or UserPreferenceCategory.Window)
                Reapply();
        }

        private static void OnSystemParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SystemParameters.HighContrast) or nameof(SystemParameters.WindowGlassColor))
                Reapply();
        }

        private static System.Windows.Threading.DispatcherTimer? _debounce;

        // Windows sends bursts of notifications; coalesce them into one re-apply.
        private static void Reapply()
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(() =>
            {
                _debounce ??= new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(250), System.Windows.Threading.DispatcherPriority.Background,
                    (_, _) => { _debounce!.Stop(); Apply(_settings); }, app.Dispatcher);
                _debounce.Stop();
                _debounce.Start();
            });
        }
    }
}
