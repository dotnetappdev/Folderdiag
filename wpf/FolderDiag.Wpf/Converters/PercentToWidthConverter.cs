using System.Globalization;
using System.Windows.Data;

namespace FolderDiag.Wpf.Converters;

public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type _, object __, CultureInfo ___)
    {
        if (values.Length >= 2 &&
            values[0] is double percent &&
            values[1] is double totalWidth &&
            totalWidth > 0)
        {
            return Math.Max(0, totalWidth * percent / 100.0);
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
