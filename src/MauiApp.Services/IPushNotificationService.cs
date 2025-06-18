using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface IPushNotificationService
{
    Task InitializeAsync();
    Task<string?> GetDeviceTokenAsync();
    Task RegisterDeviceAsync(string deviceToken, Guid userId);
    Task UnregisterDeviceAsync();
    Task RequestPermissionAsync();
    bool IsPermissionGranted { get; }
    Task HandleNotificationAsync(NotificationDto notification);
    Task ShowLocalNotificationAsync(string title, string message, Dictionary<string, string>? data = null);
    Task ClearAllNotificationsAsync();
    Task<List<NotificationDto>> GetNotificationHistoryAsync();
    Task MarkNotificationAsReadAsync(Guid notificationId);
    
    // Events
    event EventHandler<NotificationDto>? NotificationReceived;
    event EventHandler<NotificationDto>? NotificationTapped;
    event EventHandler<string>? TokenRefreshed;
}

public interface ILocalNotificationService
{
    Task ShowNotificationAsync(string title, string message, int notificationId = 0, Dictionary<string, string>? data = null);
    Task CancelNotificationAsync(int notificationId);
    Task CancelAllNotificationsAsync();
    Task<bool> RequestPermissionAsync();
    void HandleNotificationTapped(Dictionary<string, string> data);
    
    event EventHandler<Dictionary<string, string>>? NotificationTapped;
}