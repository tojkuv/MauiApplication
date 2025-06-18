using System.Globalization;

namespace MauiApp.Converters;

public class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double confidence)
        {
            return confidence switch
            {
                >= 0.8 => Colors.Green,
                >= 0.6 => Colors.Orange,
                >= 0.4 => Colors.Yellow,
                _ => Colors.Red
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}