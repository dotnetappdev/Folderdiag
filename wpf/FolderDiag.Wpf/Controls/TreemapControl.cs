using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.Controls;

/// <summary>
/// Squarified treemap control for visualizing folder/file sizes.
/// </summary>
public sealed class TreemapControl : Control
{
    private Canvas? _canvas;

    public static readonly DependencyProperty RootEntryProperty =
        DependencyProperty.Register(nameof(RootEntry), typeof(FileSystemEntry), typeof(TreemapControl),
            new PropertyMetadata(null, OnRootChanged));

    public static readonly DependencyProperty MaxDepthProperty =
        DependencyProperty.Register(nameof(MaxDepth), typeof(int), typeof(TreemapControl),
            new PropertyMetadata(3, OnRootChanged));

    public static readonly DependencyProperty SelectedEntryProperty =
        DependencyProperty.Register(nameof(SelectedEntry), typeof(FileSystemEntry), typeof(TreemapControl),
            new PropertyMetadata(null));

    public FileSystemEntry? RootEntry
    {
        get => (FileSystemEntry?)GetValue(RootEntryProperty);
        set => SetValue(RootEntryProperty, value);
    }

    public int MaxDepth
    {
        get => (int)GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    public FileSystemEntry? SelectedEntry
    {
        get => (FileSystemEntry?)GetValue(SelectedEntryProperty);
        set => SetValue(SelectedEntryProperty, value);
    }

    public event EventHandler<FileSystemEntry>? EntryClicked;
    public event EventHandler<FileSystemEntry>? EntryDoubleClicked;

    private static readonly Color[] PaletteColors =
    [
        Color.FromRgb(0, 120, 212),
        Color.FromRgb(0, 180, 216),
        Color.FromRgb(6, 214, 160),
        Color.FromRgb(255, 209, 102),
        Color.FromRgb(239, 71, 111),
        Color.FromRgb(155, 93, 229),
        Color.FromRgb(255, 107, 107),
        Color.FromRgb(78, 205, 196),
        Color.FromRgb(255, 159, 28),
        Color.FromRgb(17, 138, 178),
    ];

    static TreemapControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TreemapControl),
            new FrameworkPropertyMetadata(typeof(TreemapControl)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        if (_canvas != null) Render();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Render();
    }

    private static void OnRootChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl ctrl) ctrl.Render();
    }

    private void Render()
    {
        if (_canvas == null || RootEntry == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        _canvas.Children.Clear();

        var root = RootEntry;
        var totalSize = root.TotalSize > 0 ? root.TotalSize : root.Children.Sum(c => c.TotalSize);
        if (totalSize == 0) return;

        var items = root.Children
            .Where(c => c.TotalSize > 0 || c.Size > 0)
            .Select(c => new TreemapItem(c, (c.TotalSize > 0 ? c.TotalSize : c.Size) / (double)totalSize))
            .OrderByDescending(i => i.Weight)
            .ToList();

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        Squarify(items, bounds, 0, _canvas);
    }

    private void Squarify(List<TreemapItem> items, Rect bounds, int depth, Canvas canvas)
    {
        if (items.Count == 0 || bounds.Width < 2 || bounds.Height < 2) return;

        var row = new List<TreemapItem>();
        double w = Math.Min(bounds.Width, bounds.Height);

        foreach (var item in items)
        {
            row.Add(item);
            if (WorstRatio(row, w, bounds) <= WorstRatio(row.Take(row.Count - 1).ToList(), w, bounds))
            {
                // Commit row
                var newBounds = LayoutRow(row, bounds, depth, canvas);
                var remaining = items.Skip(row.Count).ToList();
                Squarify(remaining, newBounds, depth, canvas);
                return;
            }
        }

        // Fallback: commit current row
        if (row.Count > 0)
        {
            var newBounds = LayoutRow(row, bounds, depth, canvas);
            var remaining = items.Skip(row.Count).ToList();
            Squarify(remaining, newBounds, depth, canvas);
        }
    }

    private Rect LayoutRow(List<TreemapItem> row, Rect bounds, int depth, Canvas canvas)
    {
        double totalWeight = row.Sum(i => i.Weight);
        bool horizontal = bounds.Width >= bounds.Height;
        double x = bounds.X, y = bounds.Y;
        double width = horizontal ? totalWeight * bounds.Width / (bounds.Width >= bounds.Height ? 1 : double.NaN) : bounds.Width;
        double height = horizontal ? bounds.Height : totalWeight * bounds.Height;

        // Recalculate with actual dimension
        if (horizontal)
            width = totalWeight * bounds.Width;
        else
            height = totalWeight * bounds.Height;

        // Normalize - simpler approach
        if (horizontal)
        {
            double rowW = bounds.Width * totalWeight;
            double cx = bounds.X;
            foreach (var item in row)
            {
                double iw = bounds.Width * item.Weight;
                double ih = bounds.Height; // will be adjusted
                // Actually: use area approach
                ih = item.Weight / totalWeight * bounds.Height;
                DrawItem(item, new Rect(cx, bounds.Y, iw, bounds.Height), depth, canvas);
                cx += iw;
            }
            return new Rect(bounds.X, bounds.Y + bounds.Height, bounds.Width, 0); // shouldn't happen
        }
        else
        {
            // Use squarified algorithm properly
            return LayoutRowProper(row, bounds, canvas, depth);
        }
    }

    private Rect LayoutRowProper(List<TreemapItem> row, Rect bounds, Canvas canvas, int depth)
    {
        double totalWeight = row.Sum(i => i.Weight);
        bool horizontal = bounds.Width >= bounds.Height;

        if (horizontal)
        {
            double rowWidth = bounds.Width * totalWeight;
            double cy = bounds.Y;
            foreach (var item in row)
            {
                double h = (item.Weight / totalWeight) * bounds.Height;
                DrawItem(item, new Rect(bounds.X, cy, rowWidth, h), depth, canvas);
                cy += h;
            }
            return new Rect(bounds.X + rowWidth, bounds.Y, bounds.Width - rowWidth, bounds.Height);
        }
        else
        {
            double rowHeight = bounds.Height * totalWeight;
            double cx = bounds.X;
            foreach (var item in row)
            {
                double w = (item.Weight / totalWeight) * bounds.Width;
                DrawItem(item, new Rect(cx, bounds.Y, w, rowHeight), depth, canvas);
                cx += w;
            }
            return new Rect(bounds.X, bounds.Y + rowHeight, bounds.Width, bounds.Height - rowHeight);
        }
    }

    private void DrawItem(TreemapItem item, Rect rect, int depth, Canvas canvas)
    {
        if (rect.Width < 2 || rect.Height < 2) return;

        // Pick color based on index in parent
        var colorIdx = item.Entry.Parent?.Children.IndexOf(item.Entry) ?? 0;
        var baseColor = PaletteColors[colorIdx % PaletteColors.Length];

        var darker = Color.FromRgb(
            (byte)(baseColor.R * 0.75),
            (byte)(baseColor.G * 0.75),
            (byte)(baseColor.B * 0.75));

        var gradient = new LinearGradientBrush(baseColor, darker, new Point(0, 0), new Point(1, 1));
        var border = new Border
        {
            Width = rect.Width - 1,
            Height = rect.Height - 1,
            Background = gradient,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(depth == 0 ? 6 : 2),
            Cursor = Cursors.Hand,
            ToolTip = $"{item.Entry.Name}\n{item.Entry.TotalSizeFormatted}\n{item.Entry.PercentOfParent:F1}% of parent"
        };

        // Label
        if (rect.Width > 50 && rect.Height > 20)
        {
            var panel = new StackPanel { Margin = new Thickness(4, 2, 4, 2) };
            panel.Children.Add(new TextBlock
            {
                Text = item.Entry.Name,
                Foreground = Brushes.White,
                FontSize = Math.Min(13, Math.Max(9, rect.Height / 4)),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });
            if (rect.Height > 36)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = item.Entry.TotalSizeFormatted,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize = Math.Min(11, Math.Max(8, rect.Height / 6)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            border.Child = panel;
        }

        var entry = item.Entry;
        border.MouseLeftButtonUp += (_, _) => EntryClicked?.Invoke(this, entry);
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) EntryDoubleClicked?.Invoke(this, entry);
        };

        Canvas.SetLeft(border, rect.X);
        Canvas.SetTop(border, rect.Y);
        canvas.Children.Add(border);
    }

    private static double WorstRatio(List<TreemapItem> row, double w, Rect bounds)
    {
        if (row.Count == 0) return double.MaxValue;
        double s = row.Sum(i => i.Weight);
        double maxW = row.Max(i => i.Weight);
        double minW = row.Min(i => i.Weight);
        double s2 = s * s;
        double w2 = w * w;
        return Math.Max(w2 * maxW / s2, s2 / (w2 * minW));
    }

    private sealed class TreemapItem
    {
        public FileSystemEntry Entry { get; }
        public double Weight { get; }
        public TreemapItem(FileSystemEntry entry, double weight) { Entry = entry; Weight = weight; }
    }
}
