using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using FolderDiag.Wpf.Settings;
using FolderDiag.Wpf.Theming;

namespace FolderDiag.Wpf.Controls
{
    /// <summary>
    /// A row of colour swatches (Windows 11 accent palette) with an optional
    /// "Windows accent" swatch and a Custom… button. <see cref="SelectedColor"/> is a
    /// hex string such as "#0078D4", or <see cref="AppSettings.AccentToken"/>.
    /// </summary>
    public class SwatchPicker : WrapPanel
    {
        // Subset of the Windows 11 Settings › Personalisation › Colours grid
        private static readonly (string Name, string Hex)[] Palette =
        {
            ("Yellow gold", "#FFB900"), ("Gold", "#FF8C00"), ("Orange bright", "#F7630C"), ("Orange dark", "#CA5010"),
            ("Rust", "#DA3B01"), ("Red", "#E81123"), ("Red dark", "#C50F1F"), ("Rose", "#E3008C"),
            ("Plum", "#9A0089"), ("Orchid", "#B146C2"), ("Iris", "#8764B8"), ("Violet", "#744DA9"),
            ("Navy blue", "#0063B1"), ("Blue", "#0078D4"), ("Sky", "#0099BC"), ("Teal", "#00B7C3"),
            ("Sea foam", "#00B294"), ("Mint", "#018574"), ("Green", "#10893E"), ("Light green", "#3DBE6C"),
            ("Sport green", "#107C10"), ("Olive", "#498205"), ("Steel", "#567C73"), ("Graphite", "#69797E"),
        };

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(string), typeof(SwatchPicker),
            new FrameworkPropertyMetadata("#0078D4", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((SwatchPicker)d).UpdateSelection()));

        public string SelectedColor
        {
            get => (string)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        /// <summary>Show a first swatch that tracks the current (Windows) accent colour.</summary>
        public bool AllowAccent { get; set; }

        /// <summary>Accessible name prefix, e.g. "Warning colour".</summary>
        public string Label { get; set; } = "Colour";

        public event EventHandler? SelectedColorChanged;

        private readonly List<(Button Button, string Value)> _swatches = new();

        public SwatchPicker()
        {
            Orientation = Orientation.Horizontal;
            Loaded += (_, _) =>
            {
                if (Children.Count == 0) Build();
                ThemeManager.ThemeChanged += OnThemeChanged;
                RefreshAccentSwatch();
            };
            Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged; // static event: avoid leaks
        }

        private void OnThemeChanged(object? sender, EventArgs e) => RefreshAccentSwatch();

        private void Build()
        {
            if (AllowAccent)
                AddSwatch("Windows accent", AppSettings.AccentToken, ThemeManager.Accent, isAccent: true);
            foreach (var (name, hex) in Palette)
                AddSwatch(name, hex, (Color)ColorConverter.ConvertFromString(hex), isAccent: false);

            var custom = new Button
            {
                Content = "Custom…", Height = 26, Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(4, 2, 2, 2), ToolTip = "Pick any colour",
            };
            AutomationProperties.SetName(custom, $"{Label}: custom colour");
            custom.Click += (_, _) => PickCustom();
            Children.Add(custom);
            UpdateSelection();
        }

        private void AddSwatch(string name, string value, Color color, bool isAccent)
        {
            var fill = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), BorderThickness = new Thickness(1),
                Child = isAccent
                    ? new TextBlock { Text = "A", FontWeight = FontWeights.Bold, FontSize = 11,
                                      Foreground = new SolidColorBrush(ColorUtil.ReadableOn(color)),
                                      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    : null,
            };
            var ring = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Child = fill,
            };
            var btn = new Button
            {
                Content = ring, Width = 32, Height = 32, Padding = new Thickness(0), Margin = new Thickness(1),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                ToolTip = isAccent ? "Use the Windows accent colour" : $"{name} ({value})",
                Tag = ring,
            };
            AutomationProperties.SetName(btn, $"{Label}: {name}");
            btn.Click += (_, _) => Select(value);
            _swatches.Add((btn, value));
            Children.Add(btn);
        }

        private void RefreshAccentSwatch()
        {
            foreach (var (b, v) in _swatches)
                if (v == AppSettings.AccentToken && b.Tag is Border { Child: Border fill })
                {
                    fill.Background = new SolidColorBrush(ThemeManager.Accent);
                    if (fill.Child is TextBlock t) t.Foreground = new SolidColorBrush(ColorUtil.ReadableOn(ThemeManager.Accent));
                }
            UpdateSelection();
        }

        private void Select(string value)
        {
            if (SelectedColor == value) return;
            SelectedColor = value;
            SelectedColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSelection()
        {
            var ringBrush = (Brush?)TryFindResource("Win11TextBrush") ?? Brushes.Black;
            foreach (var (b, v) in _swatches)
                if (b.Tag is Border ring)
                    ring.BorderBrush = string.Equals(v, SelectedColor, StringComparison.OrdinalIgnoreCase) ? ringBrush : Brushes.Transparent;
        }

        private void PickCustom()
        {
            var start = ThemeManager.ResolveColor(SelectedColor);
            using var dlg = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true, AnyColor = true,
                Color = System.Drawing.Color.FromArgb(start.R, start.G, start.B),
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Select(ColorUtil.ToHex(Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B)));
        }
    }
}
