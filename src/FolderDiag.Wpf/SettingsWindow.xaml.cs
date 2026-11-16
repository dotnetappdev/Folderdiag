using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderDiag.Wpf.Settings;
using FolderDiag.Wpf.Theming;

namespace FolderDiag.Wpf
{
    /// <summary>
    /// Options dialog. Every change previews live across the app; Cancel (or closing the
    /// window) restores what was there before, Apply/OK saves to settings.json.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private AppSettings _original;   // last saved state (restored on Cancel)
        private AppSettings _working;    // what the user is editing
        private bool _loading = true;    // suppress change events while filling controls
        private bool _saved;

        private static readonly double[] FontSizes = { 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24 };

        public SettingsWindow()
        {
            _original = SettingsService.Current.Clone();
            _working  = _original.Clone();

            InitializeComponent();
            ThemeManager.Register(this);

            CmbFont.ItemsSource = Fonts.SystemFontFamilies
                .Select(f => f.Source).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            CmbFontSize.ItemsSource = FontSizes.Select(s => $"{s:0} pt").ToList();
            TxtSettingsPath.Text = SettingsService.FilePath;

            LoadFromSettings();
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
            Closing += (_, _) => { if (!_saved) ThemeManager.Apply(_original); };
        }

        /// <summary>Open on a given page: "Appearance", "Fonts", "Colours" or "General".</summary>
        public void ShowPage(string tag)
        {
            foreach (ListBoxItem item in NavList.Items)
                if ((string)item.Tag == tag) { item.IsSelected = true; break; }
        }

        // ── Settings → controls ───────────────────────────────────────
        private void LoadFromSettings()
        {
            _loading = true;
            var s = _working;

            RbThemeSystem.IsChecked = s.Theme == ThemeChoice.System;
            RbThemeLight.IsChecked  = s.Theme == ThemeChoice.Light;
            RbThemeDark.IsChecked   = s.Theme == ThemeChoice.Dark;
            ChkWindowsAccent.IsChecked = s.UseWindowsAccent;
            AccentPicker.SelectedColor = s.AccentColor;
            ChkHighContrast.IsChecked  = s.FollowHighContrast;

            ChkWindowsScaling.IsChecked  = s.UseWindowsScaling;
            ChkWindowsFont.IsChecked     = s.UseWindowsFont;
            ChkWindowsFontSize.IsChecked = s.UseWindowsFontSize;
            SldScale.Value = Math.Round(s.UiScale * 100);
            CmbFont.SelectedItem = ((List<string>)CmbFont.ItemsSource)
                .FirstOrDefault(n => n.Equals(s.FontFamily, StringComparison.OrdinalIgnoreCase)) ?? "Segoe UI";
            int sizeIdx = Array.IndexOf(FontSizes, Math.Round(s.FontSize));
            CmbFontSize.SelectedIndex = sizeIdx >= 0 ? sizeIdx : Array.IndexOf(FontSizes, 13.0);

            DiskNormalPicker.SelectedColor = s.DiskNormalColor;
            DiskWarnPicker.SelectedColor   = s.DiskWarningColor;
            DiskCritPicker.SelectedColor   = s.DiskCriticalColor;
            SldDiskWarn.Value = s.DiskWarningPercent;
            SldDiskCrit.Value = s.DiskCriticalPercent;

            FolderLowPicker.SelectedColor  = s.FolderLowColor;
            FolderMedPicker.SelectedColor  = s.FolderMediumColor;
            FolderHighPicker.SelectedColor = s.FolderHighColor;
            SldFolderMed.Value  = s.FolderMediumPercent;
            SldFolderHigh.Value = s.FolderHighPercent;

            ChkAutoScan.IsChecked = s.AutoScanOnNavigate;

            _loading = false;
            UpdateDerivedUi();
        }

        // ── Controls → settings ───────────────────────────────────────
        private void ReadIntoSettings()
        {
            var s = _working;
            s.Theme = RbThemeLight.IsChecked == true ? ThemeChoice.Light
                    : RbThemeDark.IsChecked == true  ? ThemeChoice.Dark
                    : ThemeChoice.System;
            s.UseWindowsAccent   = ChkWindowsAccent.IsChecked == true;
            s.AccentColor        = AccentPicker.SelectedColor;
            s.FollowHighContrast = ChkHighContrast.IsChecked == true;

            s.UseWindowsScaling  = ChkWindowsScaling.IsChecked == true;
            s.UseWindowsFont     = ChkWindowsFont.IsChecked == true;
            s.UseWindowsFontSize = ChkWindowsFontSize.IsChecked == true;
            s.UiScale = SldScale.Value / 100.0;
            if (CmbFont.SelectedItem is string font) s.FontFamily = font;
            if (CmbFontSize.SelectedIndex >= 0) s.FontSize = FontSizes[CmbFontSize.SelectedIndex];

            s.DiskNormalColor   = DiskNormalPicker.SelectedColor;
            s.DiskWarningColor  = DiskWarnPicker.SelectedColor;
            s.DiskCriticalColor = DiskCritPicker.SelectedColor;
            s.DiskWarningPercent  = (int)SldDiskWarn.Value;
            s.DiskCriticalPercent = (int)SldDiskCrit.Value;

            s.FolderLowColor    = FolderLowPicker.SelectedColor;
            s.FolderMediumColor = FolderMedPicker.SelectedColor;
            s.FolderHighColor   = FolderHighPicker.SelectedColor;
            s.FolderMediumPercent = (int)SldFolderMed.Value;
            s.FolderHighPercent   = (int)SldFolderHigh.Value;

            s.AutoScanOnNavigate = ChkAutoScan.IsChecked == true;
        }

        private void AnyChanged(object? sender, EventArgs e)
        {
            if (_loading) return;
            ReadIntoSettings();
            ThemeManager.Apply(_working);   // live preview
            BtnApply.IsEnabled = true;
            UpdateDerivedUi();
        }

        private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            // Keep thresholds ordered: warning < critical, medium < high
            _loading = true;
            if (sender == SldDiskWarn && SldDiskCrit.Value <= SldDiskWarn.Value) SldDiskCrit.Value = SldDiskWarn.Value + 1;
            if (sender == SldDiskCrit && SldDiskWarn.Value >= SldDiskCrit.Value) SldDiskWarn.Value = SldDiskCrit.Value - 1;
            if (sender == SldFolderMed && SldFolderHigh.Value <= SldFolderMed.Value) SldFolderHigh.Value = SldFolderMed.Value + 1;
            if (sender == SldFolderHigh && SldFolderMed.Value >= SldFolderHigh.Value) SldFolderMed.Value = SldFolderHigh.Value - 1;
            _loading = false;
            AnyChanged(sender, e);
        }

        private void OnThemeChanged(object? sender, EventArgs e) => UpdateDerivedUi();

        /// <summary>Labels, enabled states and previews that depend on current values.</summary>
        private void UpdateDerivedUi()
        {
            if (_loading || PageAppearance == null) return;
            var s = _working;

            RunWindowsMode.Text = $"  (Windows is currently {(WindowsTheme.AppsUseLightTheme ? "Light" : "Dark")})";
            AccentPicker.IsEnabled = ChkWindowsAccent.IsChecked != true;
            TxtContrastState.Text = WindowsTheme.HighContrast
                ? "A Windows contrast theme is on." + (s.FollowHighContrast ? " FolderDiag is using its colours." : " FolderDiag is ignoring it.")
                : "No Windows contrast theme is on (Settings › Accessibility › Contrast themes).";

            TxtScale.Text = $"{SldScale.Value:0}%";
            RunWindowsScale.Text    = $"  (currently {WindowsTheme.DisplayScale(this) * 100:0}%)";
            RunWindowsFont.Text     = $"  ({WindowsTheme.UiFontFamily.Source})";
            RunWindowsTextSize.Text = $"  (Text size {WindowsTheme.TextScaleFactor * 100:0}%)";

            // Manual controls only apply when the matching Windows option is off
            ScaleRow.IsEnabled    = ChkWindowsScaling.IsChecked != true;
            CmbFont.IsEnabled     = ChkWindowsFont.IsChecked != true;
            CmbFontSize.IsEnabled = ChkWindowsFontSize.IsChecked != true;

            LblDiskNormal.Text = $"Normal — below {s.DiskWarningPercent}% used";
            LblDiskWarn.Text   = $"Warning — from {s.DiskWarningPercent}%";
            LblDiskCrit.Text   = $"Critical — from {s.DiskCriticalPercent}%";
            LblFolderLow.Text  = $"Small — below {s.FolderMediumPercent}% of parent";
            LblFolderMed.Text  = $"Medium — from {s.FolderMediumPercent}%";
            LblFolderHigh.Text = $"Large — from {s.FolderHighPercent}%";

            BuildPreview(DiskPreview, new[]
            {
                ("C:", (s.DiskWarningPercent) / 2.0),
                ("D:", (s.DiskWarningPercent + s.DiskCriticalPercent) / 2.0),
                ("E:", Math.Min(99, s.DiskCriticalPercent + (100 - s.DiskCriticalPercent) / 2.0)),
            }, ThemeManager.DiskBarBrush, "used");
            BuildPreview(FolderPreview, new[]
            {
                ("Music",     s.FolderMediumPercent / 2.0),
                ("Pictures",  (s.FolderMediumPercent + s.FolderHighPercent) / 2.0),
                ("Videos",    Math.Min(99, s.FolderHighPercent + (100 - s.FolderHighPercent) / 2.0)),
            }, ThemeManager.FolderBarBrush, "of parent");
        }

        private void BuildPreview(StackPanel host, (string Name, double Pct)[] rows, Func<double, Brush> brushFor, string suffix)
        {
            host.Children.Clear();
            foreach (var (name, pct) in rows)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                g.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });

                var track = new Grid { Height = 14, VerticalAlignment = VerticalAlignment.Center };
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pct, GridUnitType.Star) });
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - pct, GridUnitType.Star) });
                var bg = new Border { CornerRadius = new CornerRadius(4) };
                bg.SetResourceReference(Border.BackgroundProperty, "Win11BorderBrush");
                Grid.SetColumnSpan(bg, 2);
                track.Children.Add(bg);
                track.Children.Add(new Border { Background = brushFor(pct), CornerRadius = new CornerRadius(4) });
                Grid.SetColumn(track, 1);
                g.Children.Add(track);

                var lbl = new TextBlock { Text = $"{pct:0}% {suffix}", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "Win11SubTextBrush");
                Grid.SetColumn(lbl, 2);
                g.Children.Add(lbl);

                host.Children.Add(g);
            }
        }

        // ── Navigation ────────────────────────────────────────────────
        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageAppearance == null || NavList.SelectedItem is not ListBoxItem item) return;
            string tag = (string)item.Tag;
            PageAppearance.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
            PageFonts.Visibility      = tag == "Fonts"      ? Visibility.Visible : Visibility.Collapsed;
            PageColours.Visibility    = tag == "Colours"    ? Visibility.Visible : Visibility.Collapsed;
            PageGeneral.Visibility    = tag == "General"    ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Buttons ───────────────────────────────────────────────────
        private void BtnResetScale_Click(object sender, RoutedEventArgs e)
        {
            SldScale.Value = 100;
            ChkWindowsScaling.IsChecked = true;
        }

        private void BtnResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Reset all settings to their defaults?\n\nThe app will follow your Windows theme, accent colour and text size again.",
                    "Reset settings", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _working = new AppSettings();
            LoadFromSettings();
            ThemeManager.Apply(_working);
            BtnApply.IsEnabled = true;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ReadIntoSettings();
            SettingsService.Save(_working.Clone());
            _original = _working.Clone();
            ThemeManager.Apply(_working);
            BtnApply.IsEnabled = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            BtnApply_Click(sender, e);
            _saved = true;
            DialogResult = true;
        }

        private void BtnOpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsService.FilePath)!;
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Settings"); }
        }
    }
}
