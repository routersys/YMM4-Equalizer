using System.Globalization;
using System.Windows.Data;

namespace Equalizer.Converters;

[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}