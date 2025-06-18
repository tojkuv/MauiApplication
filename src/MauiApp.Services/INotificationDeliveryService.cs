using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface INotificationDeliveryService
{
    // Core Delivery Methods
    Task<NotificationDeliveryResult> SendNotificationAsync(NotificationDto notification, List<string> recipients, NotificationChannel channel);
    Task<List<NotificationDeliveryResult>> SendBulkNotificationAsync(BulkNotificationRequestDto request);
    Task<NotificationDeliveryResult> SendScheduledNotificationAsync(Guid scheduledNotificationId);
    
    // Channel-Specific Delivery
    Task<NotificationDeliveryResult> SendPushNotificationAsync(PushNotificationDto pushNotification);
    Task<NotificationDeliveryResult> SendEmailNotificationAsync(EmailNotificationDto emailNotification);
    Task<NotificationDeliveryResult> SendSmsNotificationAsync(SmsNotificationDto smsNotification);
    Task<NotificationDeliveryResult> SendInAppNotificationAsync(InAppNotificationDto inAppNotification);
    
    // Template-Based Delivery
    Task<NotificationDeliveryResult> SendFromTemplateAsync(Guid templateId, Dictionary<string, object> templateData, List<string> recipients, NotificationChannel channel);
    Task<List<NotificationDeliveryResult>> SendBulkFromTemplateAsync(Guid templateId, List<TemplateRecipientData> recipientData, NotificationChannel channel);
    
    // Delivery Management
    Task<NotificationDeliveryStatus> GetDeliveryStatusAsync(Guid deliveryId);
    Task<List<NotificationDeliveryDto>> GetDeliveryHistoryAsync(Guid notificationId);
    Task<bool> CancelScheduledDeliveryAsync(Guid deliveryId);
    Task<bool> RetryFailedDeliveryAsync(Guid deliveryId);
    
    // Delivery Tracking
    Task TrackDeliveryEventAsync(Guid deliveryId, NotificationDeliveryEvent deliveryEvent, Dictionary<string, object>? metadata = null);
    Task<NotificationDeliveryAnalyticsDto> GetDeliveryAnalyticsAsync(DateTime startDate, DateTime endDate, NotificationChannel? channel = null);
    
    // Provider Management
    Task<List<DeliveryProviderDto>> GetAvailableProvidersAsync(NotificationChannel channel);
    Task<bool> TestProviderConnectionAsync(NotificationChannel channel, string providerId);
    Task<DeliveryProviderHealthDto> GetProviderHealthAsync(string providerId);
}

public interface INotificationTemplateService
{
    // Template Management
    Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequestDto request);
    Task<NotificationTemplateDto> GetTemplateAsync(Guid templateId);
    Task<List<NotificationTemplateDto>> GetTemplatesAsync(NotificationType? type = null, bool? isActive = null);
    Task<NotificationTemplateDto> UpdateTemplateAsync(Guid templateId, NotificationTemplateDto template);
    Task<bool> DeleteTemplateAsync(Guid templateId);
    Task<bool> ActivateTemplateAsync(Guid templateId, bool isActive);
    
    // Template Rendering
    Task<string> RenderTemplateAsync(Guid templateId, Dictionary<string, object> data, string culture = "en-US");
    Task<NotificationContentDto> RenderNotificationAsync(Guid templateId, Dictionary<string, object> data, NotificationChannel channel, string culture = "en-US");
    Task<bool> ValidateTemplateAsync(NotificationTemplateDto template);
    
    // Template Categories
    Task<List<string>> GetTemplateCategoriesAsync();
    Task<List<NotificationTemplateDto>> GetTemplatesByCategoryAsync(string category);
    
    // Template Analytics
    Task<TemplateUsageAnalyticsDto> GetTemplateUsageAsync(Guid templateId, DateTime startDate, DateTime endDate);
    Task<List<TemplatePerformanceDto>> GetTopPerformingTemplatesAsync(int count = 10);
}

public interface INotificationSchedulingService
{
    // Scheduling
    Task<ScheduledNotificationDto> ScheduleNotificationAsync(ScheduleNotificationRequestDto request);
    Task<List<ScheduledNotificationDto>> ScheduleBulkNotificationAsync(List<ScheduleNotificationRequestDto> requests);
    Task<bool> CancelScheduledNotificationAsync(Guid scheduledNotificationId);
    Task<bool> UpdateScheduledNotificationAsync(Guid scheduledNotificationId, ScheduledNotificationDto notification);
    
    // Recurring Notifications
    Task<RecurringNotificationDto> CreateRecurringNotificationAsync(CreateRecurringNotificationRequestDto request);
    Task<List<RecurringNotificationDto>> GetRecurringNotificationsAsync(bool? isActive = null);
    Task<bool> UpdateRecurringNotificationAsync(Guid recurringNotificationId, RecurringNotificationDto notification);
    Task<bool> DeleteRecurringNotificationAsync(Guid recurringNotificationId);
    
    // Schedule Management
    Task<List<ScheduledNotificationDto>> GetScheduledNotificationsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<ScheduledNotificationDto>> GetPendingNotificationsAsync();
    Task ProcessPendingNotificationsAsync();
    
    // Schedule Analytics
    Task<SchedulingAnalyticsDto> GetSchedulingAnalyticsAsync(DateTime startDate, DateTime endDate);
}

public interface INotificationPreferencesService
{
    // User Preferences
    Task<NotificationPreferencesDto> GetUserPreferencesAsync(Guid userId);
    Task<NotificationPreferencesDto> UpdateUserPreferencesAsync(Guid userId, NotificationPreferencesDto preferences);
    Task<bool> OptOutUserAsync(Guid userId, NotificationChannel channel);
    Task<bool> OptInUserAsync(Guid userId, NotificationChannel channel);
    
    // Global Preferences
    Task<GlobalNotificationSettingsDto> GetGlobalSettingsAsync();
    Task<GlobalNotificationSettingsDto> UpdateGlobalSettingsAsync(GlobalNotificationSettingsDto settings);
    
    // Subscription Management
    Task<List<NotificationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
    Task<NotificationSubscriptionDto> CreateSubscriptionAsync(CreateNotificationSubscriptionDto request);
    Task<bool> UpdateSubscriptionAsync(Guid subscriptionId, NotificationSubscriptionDto subscription);
    Task<bool> DeleteSubscriptionAsync(Guid subscriptionId);
    
    // Preference Validation
    Task<bool> CanSendNotificationAsync(Guid userId, NotificationType type, NotificationChannel channel);
    Task<List<NotificationChannel>> GetAllowedChannelsAsync(Guid userId, NotificationType type);
    Task<bool> IsWithinQuietHoursAsync(Guid userId);
}

public interface INotificationQueueService
{
    // Queue Management
    Task<string> EnqueueNotificationAsync(NotificationQueueItem queueItem, NotificationPriority priority = NotificationPriority.Normal);
    Task<string> EnqueueBulkNotificationAsync(List<NotificationQueueItem> queueItems, NotificationPriority priority = NotificationPriority.Normal);
    Task<NotificationQueueItem?> DequeueNotificationAsync();
    Task<bool> RemoveFromQueueAsync(string queueItemId);
    
    // Queue Statistics
    Task<NotificationQueueStatsDto> GetQueueStatsAsync();
    Task<List<NotificationQueueItem>> GetQueueItemsAsync(NotificationPriority? priority = null, int maxCount = 100);
    Task<bool> ClearQueueAsync(NotificationPriority? priority = null);
    
    // Processing Control
    Task<bool> PauseQueueProcessingAsync();
    Task<bool> ResumeQueueProcessingAsync();
    Task<bool> IsQueueProcessingPausedAsync();
    
    // Dead Letter Queue
    Task<List<NotificationQueueItem>> GetDeadLetterQueueAsync();
    Task<bool> RequeueFromDeadLetterAsync(string queueItemId);
    Task<bool> ClearDeadLetterQueueAsync();
}

// Supporting DTOs and Models
public class NotificationDeliveryResult
{
    public Guid DeliveryId { get; set; } = Guid.NewGuid();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public NotificationChannel Channel { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderMessageId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public int RetryCount { get; set; }
    public TimeSpan? DeliveryTime { get; set; }
}

public class EmailNotificationDto
{
    public List<string> ToAddresses { get; set; } = new();
    public List<string> CcAddresses { get; set; } = new();
    public List<string> BccAddresses { get; set; } = new();
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public List<EmailAttachmentDto> Attachments { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
}

public class EmailAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? ContentId { get; set; }
}

public class SmsNotificationDto
{
    public List<string> PhoneNumbers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string? FromNumber { get; set; }
    public bool IsUnicode { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public Dictionary<string, object> ProviderOptions { get; set; } = new();
}

public class InAppNotificationDto
{
    public List<Guid> UserIds { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime? ExpiresAt { get; set; }
}

public class TemplateRecipientData
{
    public string Recipient { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public string? Culture { get; set; }
}

public class NotificationContentDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? Subject { get; set; }
    public Dictionary<string, object> ChannelSpecificData { get; set; } = new();
}

public class NotificationDeliveryAnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalDeliveries { get; set; }
    public long SuccessfulDeliveries { get; set; }
    public long FailedDeliveries { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageDeliveryTime { get; set; }
    public Dictionary<NotificationChannel, long> DeliveriesByChannel { get; set; } = new();
    public Dictionary<string, long> DeliveriesByProvider { get; set; } = new();
    public List<DeliveryTrendDataPoint> DeliveryTrend { get; set; } = new();
}

public class DeliveryTrendDataPoint
{
    public DateTime Date { get; set; }
    public long TotalDeliveries { get; set; }
    public long SuccessfulDeliveries { get; set; }
    public double SuccessRate { get; set; }
}

public class DeliveryProviderDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DeliveryProviderHealthDto Health { get; set; } = new();
}

public class DeliveryProviderHealthDto
{
    public string ProviderId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public double HealthScore { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public long RequestsInLastHour { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public List<string> HealthIssues { get; set; } = new();
}

public class TemplateUsageAnalyticsDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public long TotalUsage { get; set; }
    public long SuccessfulDeliveries { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<NotificationChannel, long> UsageByChannel { get; set; } = new();
    public List<TemplateUsageTrendDto> UsageTrend { get; set; } = new();
}

public class TemplateUsageTrendDto
{
    public DateTime Date { get; set; }
    public long UsageCount { get; set; }
    public double SuccessRate { get; set; }
}

public class TemplatePerformanceDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public double PerformanceScore { get; set; }
    public long TotalUsage { get; set; }
    public double SuccessRate { get; set; }
    public double EngagementRate { get; set; }
    public TimeSpan AverageDeliveryTime { get; set; }
}

public class ScheduleNotificationRequestDto
{
    public Guid? TemplateId { get; set; }
    public NotificationDto? Notification { get; set; }
    public List<string> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public DateTime ScheduledAt { get; set; }
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public string? TimeZone { get; set; }
}

public class CreateRecurringNotificationRequestDto
{
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public List<string> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TimeZone { get; set; }
}

public class RecurringNotificationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TimeZone { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class SchedulingAnalyticsDto
{
    public long TotalScheduledNotifications { get; set; }
    public long ExecutedNotifications { get; set; }
    public long PendingNotifications { get; set; }
    public long FailedNotifications { get; set; }
    public double ExecutionSuccessRate { get; set; }
    public TimeSpan AverageExecutionDelay { get; set; }
    public List<SchedulingTrendDto> SchedulingTrend { get; set; } = new();
}

public class SchedulingTrendDto
{
    public DateTime Date { get; set; }
    public long ScheduledCount { get; set; }
    public long ExecutedCount { get; set; }
    public double SuccessRate { get; set; }
}

public class GlobalNotificationSettingsDto
{
    public bool NotificationsEnabled { get; set; } = true;
    public int MaxNotificationsPerUser { get; set; } = 100;
    public int MaxNotificationsPerHour { get; set; } = 20;
    public TimeSpan DefaultQuietHoursStart { get; set; } = TimeSpan.FromHours(22);
    public TimeSpan DefaultQuietHoursEnd { get; set; } = TimeSpan.FromHours(8);
    public List<string> BlockedDomains { get; set; } = new();
    public Dictionary<NotificationChannel, bool> ChannelDefaults { get; set; } = new();
    public int RetentionDays { get; set; } = 90;
}

public class NotificationSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Criteria { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CreateNotificationSubscriptionDto
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public Dictionary<string, object> Criteria { get; set; } = new();
}

public class NotificationQueueItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public NotificationDto Notification { get; set; } = new();
    public List<string> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class NotificationQueueStatsDto
{
    public long TotalItems { get; set; }
    public long PendingItems { get; set; }
    public long ProcessingItems { get; set; }
    public long FailedItems { get; set; }
    public long DeadLetterItems { get; set; }
    public Dictionary<NotificationPriority, long> ItemsByPriority { get; set; } = new();
    public TimeSpan AverageProcessingTime { get; set; }
    public long ProcessedInLastHour { get; set; }
    public bool IsProcessingPaused { get; set; }
}

public enum NotificationDeliveryEvent
{
    Queued = 1,
    Processing = 2,
    Sent = 3,
    Delivered = 4,
    Opened = 5,
    Clicked = 6,
    Failed = 7,
    Bounced = 8,
    Complained = 9,
    Unsubscribed = 10
}