using System.Globalization;

namespace MauiApp.Converters;

public class ProductivityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double score)
        {
            return score switch
            {
                >= 8.0 => Colors.Green,
                >= 6.0 => Colors.LightGreen,
                >= 4.0 => Colors.Orange,
                >= 2.0 => Colors.Yellow,
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