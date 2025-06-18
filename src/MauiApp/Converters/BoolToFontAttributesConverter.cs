using System.Globalization;

namespace MauiApp.Converters;

public class BoolToFontAttributesConverter : IValueConverter
{
    public FontAttributes TrueValue { get; set; } = FontAttributes.Bold;
    public FontAttributes FalseValue { get; set; } = FontAttributes.None;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}