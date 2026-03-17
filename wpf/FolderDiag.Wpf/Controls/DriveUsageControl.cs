using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderDiag.Wpf.Controls;

/// <summary>Drive usage ring indicator.</summary>
public sealed class DriveUsageControl : Control
{
    public static readonly DependencyProperty UsedPercentProperty =
        DependencyProperty.Register(nameof(UsedPercent), typeof(double), typeof(DriveUsageControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(DriveUsageControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public double UsedPercent
    {
        get => (double)GetValue(UsedPercentProperty);
        set => SetValue(UsedPercentProperty, Math.Clamp(value, 0, 100));
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    static DriveUsageControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DriveUsageControl),
            new FrameworkPropertyMetadata(typeof(DriveUsageControl)));
        WidthProperty.OverrideMetadata(typeof(DriveUsageControl), new FrameworkPropertyMetadata(80.0));
        HeightProperty.OverrideMetadata(typeof(DriveUsageControl), new FrameworkPropertyMetadata(80.0));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double cx = ActualWidth / 2, cy = ActualHeight / 2;
        double r = Math.Min(cx, cy) - 4;
        double thickness = 10;
        double ri = r - thickness;

        // Background ring
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(50, 50, 50)), thickness),
            new Point(cx, cy), r, r);

        // Used arc
        if (UsedPercent > 0)
        {
            var color = UsedPercent > 90
                ? Color.FromRgb(244, 67, 54)
                : UsedPercent > 70
                    ? Color.FromRgb(255, 152, 0)
                    : Color.FromRgb(0, 120, 212);

            var geo = CreateArcGeometry(new Point(cx, cy), r, -90, UsedPercent / 100.0 * 360 - 0.001);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), geo);
        }

        // Center text
        var ft = new FormattedText($"{UsedPercent:F0}%",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Display"),
            12, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));

        if (!string.IsNullOrEmpty(Label))
        {
            var lt = new FormattedText(Label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Variable Display"),
                9, new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(lt, new Point(cx - lt.Width / 2, cy + ft.Height / 2 + 2));
        }
    }

    private static Geometry CreateArcGeometry(Point center, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        double toRad = Math.PI / 180.0;
        double startRad = startAngleDeg * toRad;
        double endRad = (startAngleDeg + sweepAngleDeg) * toRad;

        var startPoint = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        var endPoint = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

        bool isLarge = Math.Abs(sweepAngleDeg) > 180;

        var fig = new PathFigure { StartPoint = startPoint };
        fig.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0,
            isLarge, SweepDirection.Clockwise, true));

        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return geo;
    }
}
