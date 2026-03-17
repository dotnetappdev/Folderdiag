using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.Controls;

/// <summary>Horizontal bar chart showing top N items by size.</summary>
public sealed class BarChartControl : Control
{
    private Canvas? _canvas;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<FileSystemEntry>), typeof(BarChartControl),
            new PropertyMetadata(null, OnItemsChanged));

    public static readonly DependencyProperty MaxItemsProperty =
        DependencyProperty.Register(nameof(MaxItems), typeof(int), typeof(BarChartControl),
            new PropertyMetadata(20, OnItemsChanged));

    public IEnumerable<FileSystemEntry>? ItemsSource
    {
        get => (IEnumerable<FileSystemEntry>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public event EventHandler<FileSystemEntry>? EntryClicked;

    private static readonly Color[] ChartColors =
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

    static BarChartControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BarChartControl),
            new FrameworkPropertyMetadata(typeof(BarChartControl)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        Render();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Render();
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BarChartControl ctrl) ctrl.Render();
    }

    private void Render()
    {
        if (_canvas == null) return;
        _canvas.Children.Clear();

        var items = ItemsSource?
            .OrderByDescending(x => x.TotalSize > 0 ? x.TotalSize : x.Size)
            .Take(MaxItems)
            .ToList();

        if (items == null || items.Count == 0) return;

        double maxSize = items.Max(x => x.TotalSize > 0 ? x.TotalSize : x.Size);
        if (maxSize <= 0) return;

        double rowHeight = 28;
        double labelWidth = 160;
        double barAreaWidth = Math.Max(100, ActualWidth - labelWidth - 100);
        double sizeTextWidth = 90;
        double padding = 4;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var size = item.TotalSize > 0 ? item.TotalSize : item.Size;
            double ratio = size / maxSize;
            double barWidth = Math.Max(4, barAreaWidth * ratio);
            double y = i * (rowHeight + padding) + padding;

            // Name label
            var nameText = new TextBlock
            {
                Text = item.Name,
                Foreground = item.IsDirectory
                    ? new SolidColorBrush(Color.FromRgb(96, 205, 255))
                    : new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 11,
                Width = labelWidth - 8,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = item.Name,
                Margin = new Thickness(0, (rowHeight - 14) / 2, 0, 0)
            };
            Canvas.SetLeft(nameText, 4);
            Canvas.SetTop(nameText, y);
            _canvas.Children.Add(nameText);

            // Bar
            var color = ChartColors[i % ChartColors.Length];
            var lighter = Color.FromRgb(
                (byte)Math.Min(255, color.R + 30),
                (byte)Math.Min(255, color.G + 30),
                (byte)Math.Min(255, color.B + 30));

            var bar = new Border
            {
                Width = barWidth,
                Height = rowHeight - 4,
                Background = new LinearGradientBrush(lighter, color, 90),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                ToolTip = $"{item.Name}: {item.TotalSizeFormatted} ({item.PercentOfParent:F1}%)"
            };

            var capturedItem = item;
            bar.MouseLeftButtonUp += (_, _) => EntryClicked?.Invoke(this, capturedItem);

            Canvas.SetLeft(bar, labelWidth);
            Canvas.SetTop(bar, y + 2);
            _canvas.Children.Add(bar);

            // Size label
            var sizeText = new TextBlock
            {
                Text = item.TotalSizeFormatted,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 10,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, (rowHeight - 14) / 2, 0, 0)
            };
            Canvas.SetLeft(sizeText, labelWidth + barWidth + 6);
            Canvas.SetTop(sizeText, y);
            _canvas.Children.Add(sizeText);
        }

        // Set canvas height
        _canvas.Height = items.Count * (rowHeight + padding) + padding;
    }
}
