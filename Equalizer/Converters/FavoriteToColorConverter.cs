using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Equalizer.Converters;

[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class FavoriteToColorConverter : IValueConverter
{
    public static readonly FavoriteToColorConverter Instance = new();

    private static readonly SolidColorBrush GoldBrush = CreateFrozen(Colors.Gold);
    private static readonly SolidColorBrush GrayBrush = CreateFrozen(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? GoldBrush : GrayBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush CreateFrozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}