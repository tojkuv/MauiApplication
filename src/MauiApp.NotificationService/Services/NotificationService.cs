using Microsoft.EntityFrameworkCore;
using MauiApp.NotificationService.Data;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Entities;
using System.Text.RegularExpressions;

namespace MauiApp.NotificationService.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IPushNotificationProvider _pushProvider;
    private readonly IEmailNotificationProvider _emailProvider;

    public NotificationService(
        NotificationDbContext context, 
        ILogger<NotificationService> logger,
        IPushNotificationProvider pushProvider,
        IEmailNotificationProvider emailProvider)
    {
        _context = context;
        _logger = logger;
        _pushProvider = pushProvider;
        _emailProvider = emailProvider;
    }

    public async Task<NotificationDto> CreateNotificationAsync(SendNotificationRequestDto request, Guid senderId)
    {
        try
        {
            var notification = new Notification
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                Priority = request.Priority,
                Data = request.Data,
                ActionUrl = request.ActionUrl,
                ImageUrl = request.ImageUrl,
                ExpiresAt = request.ExpiresAt,
                SenderId = senderId,
                Status = NotificationStatus.Created
            };

            // Create notifications for all specified users
            var notifications = new List<Notification>();
            foreach (var userId in request.UserIds)
            {
                var userNotification = new Notification
                {
                    UserId = userId,
                    Title = notification.Title,
                    Message = notification.Message,
                    Type = notification.Type,
                    Priority = notification.Priority,
                    Data = notification.Data,
                    ActionUrl = notification.ActionUrl,
                    ImageUrl = notification.ImageUrl,
                    ExpiresAt = notification.ExpiresAt,
                    SenderId = senderId,
                    Status = NotificationStatus.Created
                };

                notifications.Add(userNotification);
            }

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Queue for delivery
            foreach (var notif in notifications)
            {
                var channels = new List<NotificationChannel>();
                if (request.SendInApp) channels.Add(NotificationChannel.InApp);
                if (request.SendPush) channels.Add(NotificationChannel.Push);
                if (request.SendEmail) channels.Add(NotificationChannel.Email);

                await QueueNotificationForDeliveryAsync(notif.Id, channels);
            }

            // Return the first notification as representative
            return MapToDto(notifications.First());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification");
            throw;
        }
    }

    public async Task<List<NotificationDto>> CreateBulkNotificationAsync(BulkNotificationRequestDto request, Guid senderId)
    {
        try
        {
            var userIds = await GetTargetUserIdsAsync(request);
            if (!userIds.Any())
                return new List<NotificationDto>();

            var notifications = new List<Notification>();
            var baseNotification = new Notification
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                Priority = request.Priority,
                Data = request.Data,
                ActionUrl = request.ActionUrl,
                ImageUrl = request.ImageUrl,
                ExpiresAt = request.ExpiresAt,
                SenderId = senderId,
                Status = NotificationStatus.Created,
                ScheduledAt = request.ScheduledAt,
                IsScheduled = request.ScheduledAt.HasValue
            };

            foreach (var userId in userIds)
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = baseNotification.Title,
                    Message = baseNotification.Message,
                    Type = baseNotification.Type,
                    Priority = baseNotification.Priority,
                    Data = baseNotification.Data,
                    ActionUrl = baseNotification.ActionUrl,
                    ImageUrl = baseNotification.ImageUrl,
                    ExpiresAt = baseNotification.ExpiresAt,
                    SenderId = senderId,
                    Status = baseNotification.Status,
                    ScheduledAt = baseNotification.ScheduledAt,
                    IsScheduled = baseNotification.IsScheduled
                };

                notifications.Add(notification);
            }

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Queue for delivery if not scheduled
            if (!request.ScheduledAt.HasValue)
            {
                foreach (var notification in notifications)
                {
                    var channels = new List<NotificationChannel>();
                    if (request.SendInApp) channels.Add(NotificationChannel.InApp);
                    if (request.SendPush) channels.Add(NotificationChannel.Push);
                    if (request.SendEmail) channels.Add(NotificationChannel.Email);

                    await QueueNotificationForDeliveryAsync(notification.Id, channels);
                }
            }

            _logger.LogInformation("Created {Count} bulk notifications", notifications.Count);
            return notifications.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk notification");
            throw;
        }
    }

    public async Task<bool> SendNotificationAsync(Guid notificationId)
    {
        try
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
                return false;

            notification.Status = NotificationStatus.Sending;
            notification.SentAt = DateTime.UtcNow;

            // Get user preferences
            var preferences = await GetUserPreferencesAsync(notification.UserId);
            
            // Send via appropriate channels
            var success = true;

            // In-app notification is always created
            notification.Status = NotificationStatus.Sent;

            // Send push notification if enabled
            if (preferences.PushNotificationsEnabled && 
                await CanSendNotificationToUserAsync(notification.UserId, notification.Type, NotificationChannel.Push))
            {
                success &= await SendPushNotificationToUserAsync(notification);
            }

            // Send email notification if enabled
            if (preferences.EmailNotificationsEnabled && 
                await CanSendNotificationToUserAsync(notification.UserId, notification.Type, NotificationChannel.Email))
            {
                success &= await SendEmailNotificationToUserAsync(notification);
            }

            if (success)
            {
                notification.Status = NotificationStatus.Delivered;
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
            }

            await _context.SaveChangesAsync();
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<bool> SendPushNotificationAsync(PushNotificationDto pushNotification)
    {
        try
        {
            return await _pushProvider.SendPushNotificationAsync(pushNotification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification");
            return false;
        }
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            return notification == null ? null : MapToDto(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification {NotificationId}", notificationId);
            return null;
        }
    }

    public async Task<NotificationHistoryResponseDto> GetNotificationHistoryAsync(NotificationHistoryRequestDto request, Guid userId)
    {
        try
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);

            if (request.StartDate.HasValue)
                query = query.Where(n => n.CreatedAt >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(n => n.CreatedAt <= request.EndDate.Value);

            if (request.Type.HasValue)
                query = query.Where(n => n.Type == request.Type.Value);

            if (request.Status.HasValue)
                query = query.Where(n => n.Status == request.Status.Value);

            var totalCount = await query.CountAsync();

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new NotificationHistoryResponseDto
            {
                Notifications = notifications.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                HasNextPage = (request.PageNumber * request.PageSize) < totalCount,
                HasPreviousPage = request.PageNumber > 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification history for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<NotificationDto>> GetUnreadNotificationsAsync(Guid userId, int maxCount = 50)
    {
        try
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .OrderByDescending(n => n.CreatedAt)
                .Take(maxCount)
                .ToListAsync();

            return notifications.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        try
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null || notification.ReadAt.HasValue)
                return false;

            notification.ReadAt = DateTime.UtcNow;
            notification.Status = NotificationStatus.Read;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            return false;
        }
    }

    public async Task<bool> MarkMultipleAsReadAsync(List<Guid> notificationIds, Guid userId)
    {
        try
        {
            var notifications = await _context.Notifications
                .Where(n => notificationIds.Contains(n.Id) && n.UserId == userId && n.ReadAt == null)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.ReadAt = DateTime.UtcNow;
                notification.Status = NotificationStatus.Read;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking multiple notifications as read");
            return false;
        }
    }

    public async Task<bool> MarkAllAsReadAsync(Guid userId)
    {
        try
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.ReadAt = DateTime.UtcNow;
                notification.Status = NotificationStatus.Read;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<DeviceTokenDto> RegisterDeviceTokenAsync(RegisterDeviceTokenRequestDto request, Guid userId)
    {
        try
        {
            // Check if token already exists for this user and platform
            var existingToken = await _context.DeviceTokens
                .FirstOrDefaultAsync(dt => dt.Token == request.Token && dt.Platform == request.Platform);

            if (existingToken != null)
            {
                // Update existing token
                existingToken.UserId = userId;
                existingToken.AppVersion = request.AppVersion;
                existingToken.DeviceModel = request.DeviceModel;
                existingToken.OSVersion = request.OSVersion;
                existingToken.IsActive = true;
                existingToken.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new token
                existingToken = new DeviceToken
                {
                    UserId = userId,
                    Token = request.Token,
                    Platform = request.Platform,
                    AppVersion = request.AppVersion,
                    DeviceModel = request.DeviceModel,
                    OSVersion = request.OSVersion,
                    IsActive = true,
                    LastUsedAt = DateTime.UtcNow
                };

                _context.DeviceTokens.Add(existingToken);
            }

            await _context.SaveChangesAsync();

            return new DeviceTokenDto
            {
                Id = existingToken.Id,
                UserId = existingToken.UserId,
                Token = existingToken.Token,
                Platform = existingToken.Platform,
                AppVersion = existingToken.AppVersion,
                DeviceModel = existingToken.DeviceModel,
                OSVersion = existingToken.OSVersion,
                IsActive = existingToken.IsActive,
                CreatedAt = existingToken.CreatedAt,
                LastUsedAt = existingToken.LastUsedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device token for user {UserId}", userId);
            throw;
        }
    }

    // Additional interface methods with basic implementations
    public Task<bool> UpdateDeviceTokenAsync(Guid tokenId, RegisterDeviceTokenRequestDto request, Guid userId) => throw new NotImplementedException();
    public Task<bool> RemoveDeviceTokenAsync(Guid tokenId, Guid userId) => throw new NotImplementedException();
    public Task<bool> RemoveDeviceTokenByValueAsync(string token, Guid userId) => throw new NotImplementedException();
    public Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(Guid userId) => throw new NotImplementedException();

    public async Task<NotificationPreferencesDto> GetUserPreferencesAsync(Guid userId)
    {
        try
        {
            var preferences = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (preferences == null)
            {
                // Return default preferences
                return new NotificationPreferencesDto { UserId = userId };
            }

            return new NotificationPreferencesDto
            {
                UserId = preferences.UserId,
                PushNotificationsEnabled = preferences.PushNotificationsEnabled,
                EmailNotificationsEnabled = preferences.EmailNotificationsEnabled,
                InAppNotificationsEnabled = preferences.InAppNotificationsEnabled,
                TypePreferences = preferences.TypePreferences,
                QuietHoursStart = preferences.QuietHoursStart,
                QuietHoursEnd = preferences.QuietHoursEnd,
                QuietHoursDays = preferences.QuietHoursDays,
                MaxNotificationsPerHour = preferences.MaxNotificationsPerHour,
                MaxNotificationsPerDay = preferences.MaxNotificationsPerDay
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateUserPreferencesAsync(NotificationPreferencesDto preferences, Guid userId)
    {
        try
        {
            var existing = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (existing == null)
            {
                existing = new NotificationPreferences { UserId = userId };
                _context.NotificationPreferences.Add(existing);
            }

            existing.PushNotificationsEnabled = preferences.PushNotificationsEnabled;
            existing.EmailNotificationsEnabled = preferences.EmailNotificationsEnabled;
            existing.InAppNotificationsEnabled = preferences.InAppNotificationsEnabled;
            existing.TypePreferences = preferences.TypePreferences;
            existing.QuietHoursStart = preferences.QuietHoursStart;
            existing.QuietHoursEnd = preferences.QuietHoursEnd;
            existing.QuietHoursDays = preferences.QuietHoursDays;
            existing.MaxNotificationsPerHour = preferences.MaxNotificationsPerHour;
            existing.MaxNotificationsPerDay = preferences.MaxNotificationsPerDay;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> CanSendNotificationToUserAsync(Guid userId, NotificationType type, NotificationChannel channel)
    {
        try
        {
            var preferences = await GetUserPreferencesAsync(userId);

            // Check global channel preferences
            switch (channel)
            {
                case NotificationChannel.Push when !preferences.PushNotificationsEnabled:
                case NotificationChannel.Email when !preferences.EmailNotificationsEnabled:
                case NotificationChannel.InApp when !preferences.InAppNotificationsEnabled:
                    return false;
            }

            // Check type-specific preferences
            if (preferences.TypePreferences.ContainsKey(type))
            {
                var typePrefs = preferences.TypePreferences[type];
                switch (channel)
                {
                    case NotificationChannel.Push when !typePrefs.PushEnabled:
                    case NotificationChannel.Email when !typePrefs.EmailEnabled:
                    case NotificationChannel.InApp when !typePrefs.InAppEnabled:
                        return false;
                }
            }

            // Check quiet hours
            if (IsInQuietHours(preferences))
                return false;

            // Check rate limits
            if (await IsRateLimitExceededAsync(userId, preferences))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if can send notification to user {UserId}", userId);
            return false;
        }
    }

    // Placeholder implementations for remaining interface methods
    public Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequestDto request, Guid userId) => throw new NotImplementedException();
    public Task<NotificationTemplateDto?> GetTemplateAsync(Guid templateId) => throw new NotImplementedException();
    public Task<List<NotificationTemplateDto>> GetTemplatesAsync(NotificationType? type = null) => throw new NotImplementedException();
    public Task<bool> UpdateTemplateAsync(Guid templateId, CreateNotificationTemplateRequestDto request, Guid userId) => throw new NotImplementedException();
    public Task<bool> DeleteTemplateAsync(Guid templateId, Guid userId) => throw new NotImplementedException();
    public Task<NotificationDto> CreateNotificationFromTemplateAsync(Guid templateId, Dictionary<string, string> variables, List<Guid> userIds, Guid senderId) => throw new NotImplementedException();
    public Task<NotificationStatsDto> GetNotificationStatsAsync(DateTime date) => throw new NotImplementedException();
    public Task<List<NotificationStatsDto>> GetNotificationStatsRangeAsync(DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<Dictionary<NotificationType, int>> GetTypeStatsAsync(Guid userId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<Dictionary<string, object>> GetUserEngagementStatsAsync(Guid userId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<bool> SendSystemNotificationAsync(string title, string message, List<Guid>? userIds = null) => throw new NotImplementedException();
    public Task<bool> SendMarketingNotificationAsync(string title, string message, Dictionary<string, string>? data = null, List<Guid>? userIds = null) => throw new NotImplementedException();
    public Task ProcessScheduledNotificationsAsync() => throw new NotImplementedException();
    public Task CleanupExpiredNotificationsAsync() => throw new NotImplementedException();
    public Task RetryFailedNotificationsAsync() => throw new NotImplementedException();
    public Task NotifyUserAsync(Guid userId, string title, string message, NotificationType type = NotificationType.General) => throw new NotImplementedException();
    public Task NotifyProjectMembersAsync(Guid projectId, string title, string message, NotificationType type = NotificationType.ProjectUpdated) => throw new NotImplementedException();
    public Task NotifyTaskAssigneeAsync(Guid taskId, string title, string message) => throw new NotImplementedException();
    public Task<List<NotificationDeliveryDto>> GetDeliveryStatusAsync(Guid notificationId) => throw new NotImplementedException();
    public Task<bool> UpdateDeliveryStatusAsync(Guid deliveryId, NotificationDeliveryStatus status, string? errorMessage = null) => throw new NotImplementedException();
    public Task<Dictionary<NotificationChannel, int>> GetDeliveryStatsAsync(DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<bool> ProcessNotificationQueueAsync(int batchSize = 100) => throw new NotImplementedException();
    
    public async Task<bool> QueueNotificationForDeliveryAsync(Guid notificationId, List<NotificationChannel> channels)
    {
        try
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
                return false;

            foreach (var channel in channels)
            {
                var queueItem = new NotificationQueue
                {
                    NotificationId = notificationId,
                    Channel = channel,
                    Priority = notification.Priority
                };

                _context.NotificationQueue.Add(queueItem);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing notification {NotificationId} for delivery", notificationId);
            return false;
        }
    }

    public Task<int> GetQueueSizeAsync() => throw new NotImplementedException();
    public Task<bool> IsHealthyAsync() => throw new NotImplementedException();
    public Task<Dictionary<string, object>> GetServiceMetricsAsync() => throw new NotImplementedException();

    // Helper methods
    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            Priority = notification.Priority,
            Status = notification.Status,
            Data = notification.Data,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            SentAt = notification.SentAt,
            ActionUrl = notification.ActionUrl,
            ImageUrl = notification.ImageUrl,
            ExpiresAt = notification.ExpiresAt
        };
    }

    private async Task<List<Guid>> GetTargetUserIdsAsync(BulkNotificationRequestDto request)
    {
        if (request.AllUsers)
        {
            return await _context.Users.Select(u => u.Id).ToListAsync();
        }

        var userIds = new HashSet<Guid>();

        if (request.UserIds?.Any() == true)
        {
            userIds.UnionWith(request.UserIds);
        }

        if (request.ProjectIds?.Any() == true)
        {
            var projectUserIds = await _context.ProjectMembers
                .Where(pm => request.ProjectIds.Contains(pm.ProjectId))
                .Select(pm => pm.UserId)
                .ToListAsync();
            userIds.UnionWith(projectUserIds);
        }

        if (request.UserRoles?.Any() == true)
        {
            // Role-based filtering would need to be implemented with Identity roles
            // For now, skip role-based filtering
            _logger.LogWarning("Role-based filtering not implemented yet");
        }

        return userIds.ToList();
    }

    private async Task<bool> SendPushNotificationToUserAsync(Notification notification)
    {
        try
        {
            var deviceTokens = await _context.DeviceTokens
                .Where(dt => dt.UserId == notification.UserId && dt.IsActive)
                .Select(dt => dt.Token)
                .ToListAsync();

            if (!deviceTokens.Any())
                return true; // No device tokens, but not an error

            var pushNotification = new PushNotificationDto
            {
                Title = notification.Title,
                Body = notification.Message,
                ImageUrl = notification.ImageUrl,
                Data = notification.Data,
                DeviceTokens = deviceTokens,
                Priority = notification.Priority
            };

            return await _pushProvider.SendPushNotificationAsync(pushNotification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification for notification {NotificationId}", notification.Id);
            return false;
        }
    }

    private async Task<bool> SendEmailNotificationToUserAsync(Notification notification)
    {
        try
        {
            var user = await _context.Users.FindAsync(notification.UserId);
            if (user == null || string.IsNullOrEmpty(user.Email))
                return false;

            return await _emailProvider.SendEmailNotificationAsync(
                user.Email, 
                notification.Title, 
                notification.Message, 
                notification.ActionUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification for notification {NotificationId}", notification.Id);
            return false;
        }
    }

    private bool IsInQuietHours(NotificationPreferencesDto preferences)
    {
        if (!preferences.QuietHoursStart.HasValue || !preferences.QuietHoursEnd.HasValue)
            return false;

        var now = DateTime.Now.TimeOfDay;
        var currentDay = DateTime.Now.DayOfWeek;

        if (preferences.QuietHoursDays.Contains(currentDay))
        {
            var start = preferences.QuietHoursStart.Value;
            var end = preferences.QuietHoursEnd.Value;

            if (start <= end)
            {
                return now >= start && now <= end;
            }
            else
            {
                // Quiet hours span midnight
                return now >= start || now <= end;
            }
        }

        return false;
    }

    private async Task<bool> IsRateLimitExceededAsync(Guid userId, NotificationPreferencesDto preferences)
    {
        try
        {
            var hourAgo = DateTime.UtcNow.AddHours(-1);
            var dayAgo = DateTime.UtcNow.AddDays(-1);

            var hourlyCount = await _context.Notifications
                .Where(n => n.UserId == userId && n.CreatedAt > hourAgo)
                .CountAsync();

            var dailyCount = await _context.Notifications
                .Where(n => n.UserId == userId && n.CreatedAt > dayAgo)
                .CountAsync();

            return hourlyCount >= preferences.MaxNotificationsPerHour ||
                   dailyCount >= preferences.MaxNotificationsPerDay;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for user {UserId}", userId);
            return false; // Don't block on error
        }
    }
}

// Supporting service interfaces
public interface IPushNotificationProvider
{
    Task<bool> SendPushNotificationAsync(PushNotificationDto notification);
}

public interface IEmailNotificationProvider
{
    Task<bool> SendEmailNotificationAsync(string email, string subject, string body, string? actionUrl = null);
}