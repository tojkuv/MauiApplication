using System.Globalization;

namespace MauiApp.Converters;

public class BoolToSyncStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOffline)
        {
            return isOffline ? "Offline Mode" : "Online & Synced";
        }
        return "Unknown Status";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}