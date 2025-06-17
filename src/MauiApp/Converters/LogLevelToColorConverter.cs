using System.Globalization;

namespace MauiApp.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string logLevel)
        {
            return logLevel.ToLower() switch
            {
                "error" => Colors.Red,
                "warning" => Colors.Orange,
                "info" => Colors.Blue,
                "debug" => Colors.Gray,
                "trace" => Colors.LightGray,
                "performance" => Colors.Purple,
                "business" => Colors.Green,
                _ => Colors.Gray
            };
        }
        
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}