using System.Globalization;

namespace MauiApp.Converters;

public class IsCurrentUserConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Guid userId)
        {
            // This would need to be set from the current user context
            // For now, we'll use a placeholder implementation
            // In a real implementation, you'd get this from a service or static property
            return false; // Placeholder - would check against current user ID
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsNotCurrentUserConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCurrentUserConverter = new IsCurrentUserConverter();
        var result = isCurrentUserConverter.Convert(value, targetType, parameter, culture);
        return result is bool boolResult ? !boolResult : true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}