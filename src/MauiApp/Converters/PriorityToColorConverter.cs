using System.Globalization;
using MauiApp.Core.Entities;

namespace MauiApp.Converters;

public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => Colors.Green,
                TaskPriority.Medium => Colors.Orange,
                TaskPriority.High => Colors.Red,
                TaskPriority.Critical => Colors.DarkRed,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}