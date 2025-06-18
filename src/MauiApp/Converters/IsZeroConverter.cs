using System.Globalization;

namespace MauiApp.Converters;

public class IsZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return true;

        if (value is int intValue)
            return intValue == 0;

        if (value is double doubleValue)
            return doubleValue == 0.0;

        if (value is long longValue)
            return longValue == 0L;

        if (value is decimal decimalValue)
            return decimalValue == 0m;

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}