using System.Globalization;

namespace MauiApp.Converters;

public class BoolToSyncIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOffline)
        {
            return isOffline ? "⚠️" : "✅";
        }
        return "❓";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}