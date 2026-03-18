using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.Converters
{
    // Formats bytes to human-readable string
    public class BytesToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes) return FolderItem.FormatBytes(bytes);
            if (value is int i)     return FolderItem.FormatBytes(i);
            return value?.ToString() ?? "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Converts percentage (0-100) to grid column width as GridLength
    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value is double d ? d : value is float f ? f : 0;
            double max = parameter is string s && double.TryParse(s, out double p) ? p : 200.0;
            double width = Math.Max(2, Math.Min(max, pct / 100.0 * max));
            return new GridLength(width);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Converts percentage to a width for bar column (double)
    public class PercentToBarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value is double d ? d : value is float f ? f : 0;
            double max = parameter is string s && double.TryParse(s, out double p) ? p : 120.0;
            return Math.Max(1, Math.Min(max, pct / 100.0 * max));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Converts a percentage to a color (green -> yellow -> red)
    public class PercentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value is double d ? d : 0;
            if (pct < 50) return new SolidColorBrush(Color.FromRgb(0x3D, 0xBE, 0x6C));
            if (pct < 80) return new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23));
            return new SolidColorBrush(Color.FromRgb(0xE8, 0x4E, 0x4E));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // BoolToVisibility (true = Visible, false = Collapsed)
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter is string s && s == "invert";
            bool val = value is bool b && b;
            if (invert) val = !val;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Formats a DateTime nicely
    public class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
            if (value is DateTime? ndt && ndt.HasValue) return ndt.Value.ToString("yyyy-MM-dd HH:mm:ss");
            return "-";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Converts int age in days to friendly string
    public class AgeDaysToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int days) return "-";
            if (days == 0)    return "Today";
            if (days < 7)     return $"{days}d ago";
            if (days < 30)    return $"{days / 7}w ago";
            if (days < 365)   return $"{days / 30}mo ago";
            return $"{days / 365}y ago";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Extension to category icon/emoji
    public class ExtensionToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = value?.ToString() ?? "";
            return cat switch
            {
                "Images"      => "🖼",
                "Video"       => "🎬",
                "Audio"       => "🎵",
                "Documents"   => "📄",
                "Archives"    => "🗜",
                "Executables" => "⚙",
                "Source Code" => "💻",
                "Temporary"   => "🗑",
                "Config"      => "⚙",
                "Text"        => "📝",
                _             => "📁"
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
