using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class NotificationPreferencesService : INotificationPreferencesService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationPreferencesService> _logger;

    public NotificationPreferencesService(
        IApiService apiService,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        ILogger<NotificationPreferencesService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<NotificationPreferencesDto> GetUserPreferencesAsync(Guid userId)
    {
        try
        {
            var cacheKey = $"notification-preferences-{userId}";
            var cached = await _cacheService.GetAsync<NotificationPreferencesDto>(cacheKey);
            if (cached != null) return cached;

            var preferences = await _apiService.GetAsync<NotificationPreferencesDto>($"/api/notifications/preferences/{userId}");
            
            if (preferences != null)
            {
                await _cacheService.SetAsync(cacheKey, preferences, TimeSpan.FromMinutes(30));
            }

            return preferences ?? CreateDefaultPreferences(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user notification preferences: {UserId}", userId);
            return CreateDefaultPreferences(userId);
        }
    }

    public async Task<NotificationPreferencesDto> UpdateUserPreferencesAsync(Guid userId, NotificationPreferencesDto preferences)
    {
        try
        {
            _logger.LogInformation("Updating notification preferences for user: {UserId}", userId);
            preferences.UserId = userId;
            preferences.UpdatedAt = DateTime.UtcNow;

            var updatedPreferences = await _apiService.PutAsync<NotificationPreferencesDto>($"/api/notifications/preferences/{userId}", preferences);
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-preferences-{userId}");
            
            _logger.LogInformation("Updated notification preferences for user: {UserId}", userId);
            return updatedPreferences ?? preferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification preferences for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> OptOutUserAsync(Guid userId, NotificationChannel channel)
    {
        try
        {
            _logger.LogInformation("Opting out user {UserId} from channel: {Channel}", userId, channel);
            
            await _apiService.PostAsync($"/api/notifications/preferences/{userId}/opt-out", new { Channel = channel });
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-preferences-{userId}");
            
            _logger.LogInformation("Opted out user {UserId} from channel: {Channel}", userId, channel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to opt out user {UserId} from channel: {Channel}", userId, channel);
            return false;
        }
    }

    public async Task<bool> OptInUserAsync(Guid userId, NotificationChannel channel)
    {
        try
        {
            _logger.LogInformation("Opting in user {UserId} to channel: {Channel}", userId, channel);
            
            await _apiService.PostAsync($"/api/notifications/preferences/{userId}/opt-in", new { Channel = channel });
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-preferences-{userId}");
            
            _logger.LogInformation("Opted in user {UserId} to channel: {Channel}", userId, channel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to opt in user {UserId} to channel: {Channel}", userId, channel);
            return false;
        }
    }

    public async Task<GlobalNotificationSettingsDto> GetGlobalSettingsAsync()
    {
        try
        {
            var cacheKey = "global-notification-settings";
            var cached = await _cacheService.GetAsync<GlobalNotificationSettingsDto>(cacheKey);
            if (cached != null) return cached;

            var settings = await _apiService.GetAsync<GlobalNotificationSettingsDto>("/api/notifications/settings/global");
            
            if (settings != null)
            {
                await _cacheService.SetAsync(cacheKey, settings, TimeSpan.FromHours(1));
            }

            return settings ?? CreateDefaultGlobalSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get global notification settings");
            return CreateDefaultGlobalSettings();
        }
    }

    public async Task<GlobalNotificationSettingsDto> UpdateGlobalSettingsAsync(GlobalNotificationSettingsDto settings)
    {
        try
        {
            _logger.LogInformation("Updating global notification settings");
            
            var updatedSettings = await _apiService.PutAsync<GlobalNotificationSettingsDto>("/api/notifications/settings/global", settings);
            
            // Clear cache
            await _cacheService.RemoveAsync("global-notification-settings");
            
            _logger.LogInformation("Updated global notification settings");
            return updatedSettings ?? settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update global notification settings");
            throw;
        }
    }

    public async Task<List<NotificationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        try
        {
            var cacheKey = $"notification-subscriptions-{userId}";
            var cached = await _cacheService.GetAsync<List<NotificationSubscriptionDto>>(cacheKey);
            if (cached != null) return cached;

            var subscriptions = await _apiService.GetAsync<List<NotificationSubscriptionDto>>($"/api/notifications/subscriptions/{userId}");
            
            if (subscriptions != null)
            {
                await _cacheService.SetAsync(cacheKey, subscriptions, TimeSpan.FromMinutes(15));
            }

            return subscriptions ?? new List<NotificationSubscriptionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user subscriptions: {UserId}", userId);
            return new List<NotificationSubscriptionDto>();
        }
    }

    public async Task<NotificationSubscriptionDto> CreateSubscriptionAsync(CreateNotificationSubscriptionDTO request)
    {
        try
        {
            _logger.LogInformation("Creating notification subscription for user {UserId}: {Type}-{Channel}", 
                request.UserId, request.Type, request.Channel);
            
            var subscription = await _apiService.PostAsync<NotificationSubscriptionDto>("/api/notifications/subscriptions", request);
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-subscriptions-{request.UserId}");
            
            _logger.LogInformation("Created notification subscription: {SubscriptionId}", subscription?.Id);
            return subscription ?? new NotificationSubscriptionDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification subscription for user: {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<bool> UpdateSubscriptionAsync(Guid subscriptionId, NotificationSubscriptionDto subscription)
    {
        try
        {
            _logger.LogInformation("Updating notification subscription: {SubscriptionId}", subscriptionId);
            
            await _apiService.PutAsync($"/api/notifications/subscriptions/{subscriptionId}", subscription);
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-subscriptions-{subscription.UserId}");
            
            _logger.LogInformation("Updated notification subscription: {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification subscription: {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid subscriptionId)
    {
        try
        {
            _logger.LogInformation("Deleting notification subscription: {SubscriptionId}", subscriptionId);
            
            // Get subscription to clear cache
            var subscription = await _apiService.GetAsync<NotificationSubscriptionDto>($"/api/notifications/subscriptions/single/{subscriptionId}");
            
            await _apiService.DeleteAsync($"/api/notifications/subscriptions/{subscriptionId}");
            
            // Clear cache
            if (subscription != null)
            {
                await _cacheService.RemoveAsync($"notification-subscriptions-{subscription.UserId}");
            }
            
            _logger.LogInformation("Deleted notification subscription: {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete notification subscription: {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    public async Task<bool> CanSendNotificationAsync(Guid userId, NotificationType type, NotificationChannel channel)
    {
        try
        {
            var preferences = await GetUserPreferencesAsync(userId);
            var globalSettings = await GetGlobalSettingsAsync();

            // Check if notifications are globally enabled
            if (!globalSettings.NotificationsEnabled)
            {
                return false;
            }

            // Check if user has opted out of this channel
            if (preferences.OptedOutChannels.Contains(channel))
            {
                return false;
            }

            // Check if this channel is enabled for this notification type
            if (preferences.ChannelPreferences.TryGetValue(type, out var channelSettings))
            {
                if (!channelSettings.TryGetValue(channel, out var isEnabled) || !isEnabled)
                {
                    return false;
                }
            }

            // Check quiet hours
            if (await IsWithinQuietHoursAsync(userId))
            {
                // Allow high priority notifications during quiet hours
                return type == NotificationType.Alert || type == NotificationType.Critical;
            }

            // Check rate limiting
            if (await IsRateLimitExceededAsync(userId, globalSettings))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if notification can be sent to user: {UserId}", userId);
            return false; // Fail closed
        }
    }

    public async Task<List<NotificationChannel>> GetAllowedChannelsAsync(Guid userId, NotificationType type)
    {
        try
        {
            var preferences = await GetUserPreferencesAsync(userId);
            var globalSettings = await GetGlobalSettingsAsync();
            var allowedChannels = new List<NotificationChannel>();

            if (!globalSettings.NotificationsEnabled)
            {
                return allowedChannels;
            }

            foreach (var channel in Enum.GetValues<NotificationChannel>())
            {
                if (await CanSendNotificationAsync(userId, type, channel))
                {
                    allowedChannels.Add(channel);
                }
            }

            return allowedChannels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get allowed channels for user: {UserId}", userId);
            return new List<NotificationChannel>();
        }
    }

    public async Task<bool> IsWithinQuietHoursAsync(Guid userId)
    {
        try
        {
            var preferences = await GetUserPreferencesAsync(userId);
            var globalSettings = await GetGlobalSettingsAsync();

            var quietStart = preferences.QuietHoursStart ?? globalSettings.DefaultQuietHoursStart;
            var quietEnd = preferences.QuietHoursEnd ?? globalSettings.DefaultQuietHoursEnd;

            var now = DateTime.Now.TimeOfDay;

            // Handle quiet hours that span midnight
            if (quietStart > quietEnd)
            {
                return now >= quietStart || now <= quietEnd;
            }
            else
            {
                return now >= quietStart && now <= quietEnd;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check quiet hours for user: {UserId}", userId);
            return false;
        }
    }

    // Helper Methods
    private static NotificationPreferencesDto CreateDefaultPreferences(Guid userId)
    {
        return new NotificationPreferencesDto
        {
            UserId = userId,
            EmailEnabled = true,
            PushEnabled = true,
            InAppEnabled = true,
            SmsEnabled = false,
            ChannelPreferences = new Dictionary<NotificationType, Dictionary<NotificationChannel, bool>>
            {
                [NotificationType.General] = new Dictionary<NotificationChannel, bool>
                {
                    [NotificationChannel.Email] = true,
                    [NotificationChannel.Push] = true,
                    [NotificationChannel.InApp] = true,
                    [NotificationChannel.SMS] = false
                },
                [NotificationType.Alert] = new Dictionary<NotificationChannel, bool>
                {
                    [NotificationChannel.Email] = true,
                    [NotificationChannel.Push] = true,
                    [NotificationChannel.InApp] = true,
                    [NotificationChannel.SMS] = true
                }
            },
            OptedOutChannels = new List<NotificationChannel>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static GlobalNotificationSettingsDto CreateDefaultGlobalSettings()
    {
        return new GlobalNotificationSettingsDto
        {
            NotificationsEnabled = true,
            MaxNotificationsPerUser = 100,
            MaxNotificationsPerHour = 20,
            DefaultQuietHoursStart = TimeSpan.FromHours(22),
            DefaultQuietHoursEnd = TimeSpan.FromHours(8),
            BlockedDomains = new List<string>(),
            ChannelDefaults = new Dictionary<NotificationChannel, bool>
            {
                [NotificationChannel.Email] = true,
                [NotificationChannel.Push] = true,
                [NotificationChannel.InApp] = true,
                [NotificationChannel.SMS] = false
            },
            RetentionDays = 90
        };
    }

    private async Task<bool> IsRateLimitExceededAsync(Guid userId, GlobalNotificationSettingsDto globalSettings)
    {
        try
        {
            var cacheKey = $"notification-rate-limit-{userId}";
            var currentCount = await _cacheService.GetAsync<int>(cacheKey);
            
            return currentCount >= globalSettings.MaxNotificationsPerHour;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check rate limit for user: {UserId}", userId);
            return false;
        }
    }
}