using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        value is Visibility.Collapsed;
}

[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) => value is not true;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) => value is not true;
}

[ValueConversion(typeof(long), typeof(string))]
public sealed class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is long l ? FileSizeHelper.Format(l) : "0 B";
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public sealed class IsDirectoryToIconConverter : IValueConverter
{
    public static readonly IsDirectoryToIconConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? "\uE8B7" : "\uE8A5"; // Folder / Document Segoe MDL2
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class IsDirectoryToColorConverter : IValueConverter
{
    public static readonly IsDirectoryToColorConverter Instance = new();
    private static readonly Brush FolderBrush = new SolidColorBrush(Color.FromRgb(96, 205, 255));
    private static readonly Brush FileBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? FolderBrush : FileBrush;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}


[ValueConversion(typeof(double), typeof(Color))]
public sealed class PercentToColorConverter : IValueConverter
{
    public static readonly PercentToColorConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        if (value is not double d) return Color.FromRgb(0, 120, 212);
        return d switch
        {
            > 80 => Color.FromRgb(244, 67, 54),
            > 50 => Color.FromRgb(255, 152, 0),
            > 20 => Color.FromRgb(0, 120, 212),
            _ => Color.FromRgb(6, 214, 160)
        };
    }
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(string), typeof(string))]
public sealed class ExtensionToFileTypeConverter : IValueConverter
{
    public static readonly ExtensionToFileTypeConverter Instance = new();
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is string ext ? GetFileTypeIcon(ext) : "\uE8A5";
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();

    // Segoe MDL2 icons
    private static string GetFileTypeIcon(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uEB9F", // Image
        ".mp4" or ".avi" or ".mkv" or ".mov" => "\uE8B2",   // Video
        ".mp3" or ".wav" or ".flac" or ".aac" => "\uEC4F",  // Music
        ".pdf" => "\uEA90",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\uE8B1", // Zip
        ".exe" or ".msi" => "\uE756",  // Application
        ".txt" or ".log" => "\uE8A5",  // Text
        ".xml" or ".json" or ".yaml" => "\uE8A5",
        ".cs" or ".py" or ".js" or ".ts" or ".cpp" or ".java" => "\uE943", // Code
        _ => "\uE8A5"  // Generic file
    };
}

public sealed class MultiValueBoolConverter : IMultiValueConverter
{
    public static readonly MultiValueBoolConverter Instance = new();
    public object Convert(object[] values, Type _, object __, CultureInfo ___) =>
        values.All(v => v is true);
    public object[] ConvertBack(object value, Type[] _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}
