using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Equalizer.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public static readonly BooleanToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = false;

        if (value is bool b)
            flag = b;
        else if (value is bool?)
            flag = ((bool?)value).GetValueOrDefault();

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}