using System.Globalization;

namespace MauiApp.Converters;

public class BoolToSendIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSending)
        {
            return isSending ? "⏳" : "➤";
        }
        return "➤";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}