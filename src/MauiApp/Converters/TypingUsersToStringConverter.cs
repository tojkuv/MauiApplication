using System.Collections;
using System.Globalization;
using MauiApp.ViewModels.Collaboration;

namespace MauiApp.Converters;

public class TypingUsersToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable typingUsers)
        {
            var users = typingUsers.Cast<UserTypingIndicator>().ToList();
            
            return users.Count switch
            {
                0 => "",
                1 => users[0].UserName,
                2 => $"{users[0].UserName} and {users[1].UserName}",
                _ => $"{users[0].UserName} and {users.Count - 1} others"
            };
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CountToIsAreConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 1 ? "is" : "are";
        }
        return "are";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}