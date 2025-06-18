using MauiApp.Core.DTOs;
using System.Globalization;

namespace MauiApp.Converters;

public class NotificationTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.TaskAssigned => "Task Assigned",
                NotificationType.TaskCompleted => "Task Completed",
                NotificationType.TaskOverdue => "Task Overdue",
                NotificationType.CommentAdded => "Comment",
                NotificationType.ProjectUpdated => "Project Update",
                NotificationType.ProjectInvitation => "Project Invitation",
                NotificationType.FileShared => "File Shared",
                NotificationType.Reminder => "Reminder",
                NotificationType.System => "System",
                NotificationType.Marketing => "Marketing",
                NotificationType.General => "General",
                _ => "Notification"
            };
        }
        return "Notification";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}