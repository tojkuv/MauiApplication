using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface INotificationService
{
    // Core notification operations
    Task<NotificationDto> CreateNotificationAsync(SendNotificationRequestDto request, Guid senderId);
    Task<List<NotificationDto>> CreateBulkNotificationAsync(BulkNotificationRequestDto request, Guid senderId);
    Task<bool> SendNotificationAsync(Guid notificationId);
    Task<bool> SendPushNotificationAsync(PushNotificationDto pushNotification);

    // Notification management
    Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId);
    Task<NotificationHistoryResponseDto> GetNotificationHistoryAsync(NotificationHistoryRequestDto request, Guid userId);
    Task<List<NotificationDto>> GetUnreadNotificationsAsync(Guid userId, int maxCount = 50);
    Task<int> GetUnreadCountAsync(Guid userId);

    // Mark notifications
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<bool> MarkMultipleAsReadAsync(List<Guid> notificationIds, Guid userId);
    Task<bool> MarkAllAsReadAsync(Guid userId);
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId);

    // Device token management
    Task<DeviceTokenDto> RegisterDeviceTokenAsync(RegisterDeviceTokenRequestDto request, Guid userId);
    Task<bool> UpdateDeviceTokenAsync(Guid tokenId, RegisterDeviceTokenRequestDto request, Guid userId);
    Task<bool> RemoveDeviceTokenAsync(Guid tokenId, Guid userId);
    Task<bool> RemoveDeviceTokenByValueAsync(string token, Guid userId);
    Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(Guid userId);

    // User preferences
    Task<NotificationPreferencesDto> GetUserPreferencesAsync(Guid userId);
    Task<bool> UpdateUserPreferencesAsync(NotificationPreferencesDto preferences, Guid userId);
    Task<bool> CanSendNotificationToUserAsync(Guid userId, NotificationType type, NotificationChannel channel);

    // Templates
    Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequestDto request, Guid userId);
    Task<NotificationTemplateDto?> GetTemplateAsync(Guid templateId);
    Task<List<NotificationTemplateDto>> GetTemplatesAsync(NotificationType? type = null);
    Task<bool> UpdateTemplateAsync(Guid templateId, CreateNotificationTemplateRequestDto request, Guid userId);
    Task<bool> DeleteTemplateAsync(Guid templateId, Guid userId);
    Task<NotificationDto> CreateNotificationFromTemplateAsync(Guid templateId, Dictionary<string, string> variables, List<Guid> userIds, Guid senderId);

    // Statistics and analytics
    Task<NotificationStatsDto> GetNotificationStatsAsync(DateTime date);
    Task<List<NotificationStatsDto>> GetNotificationStatsRangeAsync(DateTime startDate, DateTime endDate);
    Task<Dictionary<NotificationType, int>> GetTypeStatsAsync(Guid userId, DateTime startDate, DateTime endDate);
    Task<Dictionary<string, object>> GetUserEngagementStatsAsync(Guid userId, DateTime startDate, DateTime endDate);

    // Admin and system operations
    Task<bool> SendSystemNotificationAsync(string title, string message, List<Guid>? userIds = null);
    Task<bool> SendMarketingNotificationAsync(string title, string message, Dictionary<string, string>? data = null, List<Guid>? userIds = null);
    Task ProcessScheduledNotificationsAsync();
    Task CleanupExpiredNotificationsAsync();
    Task RetryFailedNotificationsAsync();

    // Real-time notifications
    Task NotifyUserAsync(Guid userId, string title, string message, NotificationType type = NotificationType.General);
    Task NotifyProjectMembersAsync(Guid projectId, string title, string message, NotificationType type = NotificationType.ProjectUpdated);
    Task NotifyTaskAssigneeAsync(Guid taskId, string title, string message);

    // Delivery tracking
    Task<List<NotificationDeliveryDto>> GetDeliveryStatusAsync(Guid notificationId);
    Task<bool> UpdateDeliveryStatusAsync(Guid deliveryId, NotificationDeliveryStatus status, string? errorMessage = null);
    Task<Dictionary<NotificationChannel, int>> GetDeliveryStatsAsync(DateTime startDate, DateTime endDate);

    // Batch operations
    Task<bool> ProcessNotificationQueueAsync(int batchSize = 100);
    Task<bool> QueueNotificationForDeliveryAsync(Guid notificationId, List<NotificationChannel> channels);
    Task<int> GetQueueSizeAsync();

    // Health and monitoring
    Task<bool> IsHealthyAsync();
    Task<Dictionary<string, object>> GetServiceMetricsAsync();
}