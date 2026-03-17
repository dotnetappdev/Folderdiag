using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderDiag.Wpf.Controls;

/// <summary>Horizontal size bar similar to FolderSizes.</summary>
public sealed class SizeBarControl : Control
{
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(SizeBarControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(SizeBarControl),
            new FrameworkPropertyMetadata(Color.FromRgb(0, 120, 212), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowTextProperty =
        DependencyProperty.Register(nameof(ShowText), typeof(bool), typeof(SizeBarControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, Math.Clamp(value, 0, 100));
    }

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    static SizeBarControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SizeBarControl),
            new FrameworkPropertyMetadata(typeof(SizeBarControl)));
        HeightProperty.OverrideMetadata(typeof(SizeBarControl), new FrameworkPropertyMetadata(14.0));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Background
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            null,
            new Rect(0, 0, w, h), 3, 3);

        // Fill bar
        if (Percent > 0)
        {
            var fillW = Math.Max(4, w * Percent / 100.0);
            var lighter = Color.FromRgb(
                (byte)Math.Min(255, BarColor.R + 40),
                (byte)Math.Min(255, BarColor.G + 40),
                (byte)Math.Min(255, BarColor.B + 40));
            var grad = new LinearGradientBrush(lighter, BarColor, 90);
            drawingContext.DrawRoundedRectangle(grad, null,
                new Rect(0, 0, fillW, h), 3, 3);
        }

        // Text
        if (ShowText && w > 40)
        {
            var text = $"{Percent:F1}%";
            var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Variable Display"),
                10, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(w - ft.Width - 4, (h - ft.Height) / 2));
        }
    }
}
