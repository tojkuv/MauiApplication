using System.Globalization;
using MauiApp.Core.Entities;

namespace MauiApp.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MauiApp.Core.Entities.TaskStatus status)
        {
            return status switch
            {
                MauiApp.Core.Entities.TaskStatus.Todo => Colors.Gray,
                MauiApp.Core.Entities.TaskStatus.InProgress => Colors.Blue,
                MauiApp.Core.Entities.TaskStatus.Review => Colors.Orange,
                MauiApp.Core.Entities.TaskStatus.Done => Colors.Green,
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