using System.Globalization;

namespace MauiApp.Converters;

public class InsightTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string type)
        {
            return type.ToLower() switch
            {
                "deadline" => "⏰",
                "capacity" => "📊",
                "risk" => "⚠️",
                "performance" => "🚀",
                "quality" => "⭐",
                "efficiency" => "⚡",
                "workload" => "📈",
                "collaboration" => "🤝",
                _ => "💡"
            };
        }
        return "💡";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}