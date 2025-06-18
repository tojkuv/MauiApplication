using System.Globalization;

namespace MauiApp.Converters;

public class BoolToThicknessConverter : IValueConverter
{
    public double TrueValue { get; set; } = 2.0;
    public double FalseValue { get; set; } = 0.0;

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