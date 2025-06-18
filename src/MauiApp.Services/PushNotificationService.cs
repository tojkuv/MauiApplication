using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MauiApp.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly IApiService _apiService;
    private readonly ILocalNotificationService _localNotificationService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly List<NotificationDto> _notificationHistory = new();

    private string? _deviceToken;
    private bool _isInitialized;

    public bool IsPermissionGranted { get; private set; }

    public event EventHandler<NotificationDto>? NotificationReceived;
    public event EventHandler<NotificationDto>? NotificationTapped;
    public event EventHandler<string>? TokenRefreshed;

    public PushNotificationService(
        IApiService apiService,
        ILocalNotificationService localNotificationService,
        ISecureStorageService secureStorageService,
        ILogger<PushNotificationService> logger)
    {
        _apiService = apiService;
        _localNotificationService = localNotificationService;
        _secureStorageService = secureStorageService;
        _logger = logger;

        // Subscribe to local notification events
        _localNotificationService.NotificationTapped += OnLocalNotificationTapped;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing push notification service");

            // Request permission first
            await RequestPermissionAsync();

            if (IsPermissionGranted)
            {
                // Get device token
                _deviceToken = await GetDeviceTokenAsync();
                
                if (!string.IsNullOrEmpty(_deviceToken))
                {
                    // Try to get current user and register device
                    var storedUserId = await _secureStorageService.GetAsync("current_user_id");
                    if (!string.IsNullOrEmpty(storedUserId) && Guid.TryParse(storedUserId, out var userId))
                    {
                        await RegisterDeviceAsync(_deviceToken, userId);
                    }
                }
            }

            _isInitialized = true;
            _logger.LogInformation("Push notification service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize push notification service");
        }
    }

    public async Task<string?> GetDeviceTokenAsync()
    {
        try
        {
#if ANDROID
            return await GetAndroidTokenAsync();
#elif IOS
            return await GetiOSTokenAsync();
#else
            // For Windows and other platforms, we'll use a generated identifier
            return await GenerateFallbackTokenAsync();
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device token");
            return null;
        }
    }

    public async Task RegisterDeviceAsync(string deviceToken, Guid userId)
    {
        try
        {
            var deviceInfo = new DeviceRegistrationRequest
            {
                DeviceToken = deviceToken,
                UserId = userId,
                Platform = DeviceInfo.Platform.ToString(),
                DeviceModel = DeviceInfo.Model,
                OperatingSystem = DeviceInfo.VersionString,
                AppVersion = AppInfo.VersionString,
                TimeZone = TimeZoneInfo.Local.Id
            };

            await _apiService.PostAsync("/api/notifications/devices/register", deviceInfo);
            
            // Store registration info
            await _secureStorageService.SetAsync("device_token", deviceToken);
            await _secureStorageService.SetAsync("registered_user_id", userId.ToString());

            _logger.LogInformation("Device registered for push notifications: {DeviceToken}", deviceToken[..8] + "...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device for push notifications");
            throw;
        }
    }

    public async Task UnregisterDeviceAsync()
    {
        try
        {
            var deviceToken = await _secureStorageService.GetAsync("device_token");
            if (!string.IsNullOrEmpty(deviceToken))
            {
                await _apiService.DeleteAsync($"/api/notifications/devices/{deviceToken}");
                
                // Clear stored registration info
                await _secureStorageService.RemoveAsync("device_token");
                await _secureStorageService.RemoveAsync("registered_user_id");
            }

            _logger.LogInformation("Device unregistered from push notifications");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister device from push notifications");
        }
    }

    public async Task RequestPermissionAsync()
    {
        try
        {
            IsPermissionGranted = await _localNotificationService.RequestPermissionAsync();
            _logger.LogInformation("Push notification permission: {Permission}", IsPermissionGranted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request push notification permission");
            IsPermissionGranted = false;
        }
    }

    public async Task HandleNotificationAsync(NotificationDto notification)
    {
        try
        {
            _logger.LogInformation("Handling notification: {NotificationId}", notification.Id);

            // Add to history
            _notificationHistory.Insert(0, notification);

            // Keep only last 100 notifications
            if (_notificationHistory.Count > 100)
            {
                _notificationHistory.RemoveRange(100, _notificationHistory.Count - 100);
            }

            // Trigger event for listeners
            NotificationReceived?.Invoke(this, notification);

            // Show local notification if app is not in foreground
            if (!IsAppInForeground())
            {
                var data = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = notification.Type,
                    ["projectId"] = notification.ProjectId?.ToString() ?? "",
                    ["taskId"] = notification.TaskId?.ToString() ?? ""
                };

                await _localNotificationService.ShowNotificationAsync(
                    notification.Title,
                    notification.Message,
                    (int)(notification.Id.GetHashCode() & 0x7FFFFFFF),
                    data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle notification: {NotificationId}", notification.Id);
        }
    }

    public async Task ShowLocalNotificationAsync(string title, string message, Dictionary<string, string>? data = null)
    {
        try
        {
            await _localNotificationService.ShowNotificationAsync(title, message, 0, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show local notification");
        }
    }

    public async Task ClearAllNotificationsAsync()
    {
        try
        {
            await _localNotificationService.CancelAllNotificationsAsync();
            _notificationHistory.Clear();
            _logger.LogInformation("All notifications cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear notifications");
        }
    }

    public Task<List<NotificationDto>> GetNotificationHistoryAsync()
    {
        return Task.FromResult(_notificationHistory.ToList());
    }

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        try
        {
            await _apiService.PutAsync($"/api/notifications/{notificationId}/read", new { });
            
            var notification = _notificationHistory.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Notification marked as read: {NotificationId}", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read: {NotificationId}", notificationId);
        }
    }

    private void OnLocalNotificationTapped(object? sender, Dictionary<string, string> data)
    {
        try
        {
            _logger.LogInformation("Local notification tapped with data: {Data}", JsonSerializer.Serialize(data));

            if (data.TryGetValue("notificationId", out var notificationIdStr) && 
                Guid.TryParse(notificationIdStr, out var notificationId))
            {
                var notification = _notificationHistory.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    NotificationTapped?.Invoke(this, notification);
                    
                    // Mark as read
                    _ = Task.Run(async () => await MarkNotificationAsReadAsync(notificationId));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notification tap");
        }
    }

    private bool IsAppInForeground()
    {
        // Simple check - in a real implementation, you might want to track app state more accurately
        return Application.Current?.Windows?.Any(w => w.IsActivated) == true;
    }

#if ANDROID
    private async Task<string?> GetAndroidTokenAsync()
    {
        // This would integrate with Firebase Cloud Messaging
        // For now, return a mock token
        return await Task.FromResult($"android_token_{DeviceInfo.Name}_{DateTime.UtcNow.Ticks}");
    }
#endif

#if IOS
    private async Task<string?> GetiOSTokenAsync()
    {
        // This would integrate with Apple Push Notification Service
        // For now, return a mock token
        return await Task.FromResult($"ios_token_{DeviceInfo.Name}_{DateTime.UtcNow.Ticks}");
    }
#endif

    private async Task<string> GenerateFallbackTokenAsync()
    {
        // Generate a unique identifier for platforms without native push support
        var existingToken = await _secureStorageService.GetAsync("fallback_device_token");
        if (!string.IsNullOrEmpty(existingToken))
            return existingToken;

        var fallbackToken = $"fallback_{DeviceInfo.Platform}_{Guid.NewGuid()}";
        await _secureStorageService.SetAsync("fallback_device_token", fallbackToken);
        return fallbackToken;
    }
}

public class DeviceRegistrationRequest
{
    public string DeviceToken { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
}