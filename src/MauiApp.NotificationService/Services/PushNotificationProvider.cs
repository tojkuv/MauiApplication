using MauiApp.Core.DTOs;
using Microsoft.Azure.NotificationHubs;

namespace MauiApp.NotificationService.Services;

public class PushNotificationProvider : IPushNotificationProvider
{
    private readonly NotificationHubClient _notificationHubClient;
    private readonly ILogger<PushNotificationProvider> _logger;

    public PushNotificationProvider(IConfiguration configuration, ILogger<PushNotificationProvider> logger)
    {
        _logger = logger;
        
        var connectionString = configuration.GetConnectionString("NotificationHub") ?? 
                              "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=test";
        var hubName = configuration["NotificationHub:HubName"] ?? "TestHub";
        
        _notificationHubClient = NotificationHubClient.CreateClientFromConnectionString(connectionString, hubName);
    }

    public async Task<bool> SendPushNotificationAsync(PushNotificationDto notification)
    {
        try
        {
            var results = new List<NotificationOutcome>();

            // Group device tokens by platform for optimal sending
            var iosTokens = notification.DeviceTokens.Where(t => IsIOSToken(t)).ToList();
            var androidTokens = notification.DeviceTokens.Where(t => IsAndroidToken(t)).ToList();

            // Send iOS notifications
            if (iosTokens.Any())
            {
                var iosPayload = CreateIOSPayload(notification);
                var iosResult = await _notificationHubClient.SendAppleNativeNotificationAsync(iosPayload, iosTokens);
                results.Add(iosResult);
            }

            // Send Android notifications
            if (androidTokens.Any())
            {
                var androidPayload = CreateAndroidPayload(notification);
                var androidResult = await _notificationHubClient.SendFcmNativeNotificationAsync(androidPayload, androidTokens);
                results.Add(androidResult);
            }

            // Log results
            foreach (var result in results)
            {
                _logger.LogInformation("Push notification result: Success={Success}, Failure={Failure}", 
                    result.Success, result.Failure);
            }

            return results.All(r => r.Success > 0 || r.Failure == 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification");
            return false;
        }
    }

    private string CreateIOSPayload(PushNotificationDto notification)
    {
        var payload = new
        {
            aps = new
            {
                alert = new
                {
                    title = notification.Title,
                    body = notification.Body
                },
                badge = notification.Badge,
                sound = notification.Sound ?? "default",
                contentAvailable = 1
            },
            data = notification.Data
        };

        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private string CreateAndroidPayload(PushNotificationDto notification)
    {
        var payload = new
        {
            notification = new
            {
                title = notification.Title,
                body = notification.Body,
                icon = "ic_notification",
                image = notification.ImageUrl
            },
            data = notification.Data,
            priority = GetAndroidPriority(notification.Priority),
            collapse_key = notification.CollapseKey
        };

        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private bool IsIOSToken(string token)
    {
        // iOS tokens are typically 64 characters of hex
        return token.Length == 64 && token.All(c => "0123456789abcdefABCDEF".Contains(c));
    }

    private bool IsAndroidToken(string token)
    {
        // FCM tokens are typically longer and contain various characters
        return !IsIOSToken(token);
    }

    private string GetAndroidPriority(NotificationPriority priority)
    {
        return priority switch
        {
            NotificationPriority.Critical => "high",
            NotificationPriority.High => "high",
            NotificationPriority.Normal => "normal",
            NotificationPriority.Low => "normal",
            _ => "normal"
        };
    }
}