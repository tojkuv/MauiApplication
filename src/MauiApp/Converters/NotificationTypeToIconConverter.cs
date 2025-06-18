using MauiApp.Core.DTOs;
using System.Globalization;

namespace MauiApp.Converters;

public class NotificationTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.TaskAssigned => "📝",
                NotificationType.TaskCompleted => "✅",
                NotificationType.TaskOverdue => "⏰",
                NotificationType.CommentAdded => "💬",
                NotificationType.ProjectUpdated => "📊",
                NotificationType.ProjectInvitation => "📩",
                NotificationType.FileShared => "📎",
                NotificationType.Reminder => "⏰",
                NotificationType.System => "⚙️",
                NotificationType.Marketing => "📢",
                NotificationType.General => "🔔",
                _ => "🔔"
            };
        }
        return "🔔";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}