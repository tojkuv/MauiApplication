using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public NotificationStatus Status { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
    
    // Related entity IDs for navigation
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? FileId { get; set; }
}

public class SendNotificationRequestDto
{
    [Required]
    public List<Guid> UserIds { get; set; } = new();
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; } = NotificationType.General;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public Dictionary<string, string> Data { get; set; } = new();
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool SendPush { get; set; } = true;
    public bool SendEmail { get; set; } = false;
    public bool SendInApp { get; set; } = true;
}

public class BulkNotificationRequestDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; } = NotificationType.General;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public Dictionary<string, string> Data { get; set; } = new();
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Target audience filters
    public List<Guid>? UserIds { get; set; }
    public List<Guid>? ProjectIds { get; set; }
    public List<string>? UserRoles { get; set; }
    public bool AllUsers { get; set; } = false;
    
    // Delivery options
    public bool SendPush { get; set; } = true;
    public bool SendEmail { get; set; } = false;
    public bool SendInApp { get; set; } = true;
    public DateTime? ScheduledAt { get; set; }
}

public class NotificationPreferencesDto
{
    public Guid UserId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
    
    // Channel preferences by notification type
    public Dictionary<NotificationType, Dictionary<NotificationChannel, bool>> ChannelPreferences { get; set; } = new();
    
    // Opted-out channels
    public List<NotificationChannel> OptedOutChannels { get; set; } = new();
    
    // Quiet hours
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
    public List<DayOfWeek> QuietHoursDays { get; set; } = new();
    
    // Frequency limits
    public int MaxNotificationsPerHour { get; set; } = 10;
    public int MaxNotificationsPerDay { get; set; } = 50;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class NotificationChannelPreferences
{
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public NotificationPriority MinimumPriority { get; set; } = NotificationPriority.Low;
}

public class DeviceTokenDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}

public class RegisterDeviceTokenRequestDto
{
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    public DevicePlatform Platform { get; set; }
    
    public string AppVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? HtmlTemplate { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? ActionUrlTemplate { get; set; }
    public Dictionary<string, object> ChannelSettings { get; set; } = new();
    public List<TemplateVariableDto> Variables { get; set; } = new();
    public Dictionary<string, string> DefaultData { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateNotificationTemplateRequestDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    [Required]
    public NotificationType Type { get; set; }
    
    [Required]
    [StringLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string BodyTemplate { get; set; } = string.Empty;
    
    public string? HtmlTemplate { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? ActionUrlTemplate { get; set; }
    public Dictionary<string, object> ChannelSettings { get; set; } = new();
    public List<TemplateVariableDto> Variables { get; set; } = new();
    public Dictionary<string, string> DefaultData { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class TemplateVariableDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
}

public class NotificationStatsDto
{
    public DateTime Date { get; set; }
    public int TotalSent { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalRead { get; set; }
    public int TotalFailed { get; set; }
    public double DeliveryRate { get; set; }
    public double ReadRate { get; set; }
    public Dictionary<NotificationType, int> TypeBreakdown { get; set; } = new();
    public Dictionary<DevicePlatform, int> PlatformBreakdown { get; set; } = new();
}

public class NotificationHistoryRequestDto
{
    public Guid? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public NotificationType? Type { get; set; }
    public NotificationStatus? Status { get; set; }
    public int PageSize { get; set; } = 20;
    public int PageNumber { get; set; } = 1;
}

public class NotificationHistoryResponseDto
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class MarkNotificationRequestDto
{
    [Required]
    public List<Guid> NotificationIds { get; set; } = new();
}

public class NotificationDeliveryDto
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetry { get; set; }
}

public class PushNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Sound { get; set; } = "default";
    public int? Badge { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public List<string> DeviceTokens { get; set; } = new();
    public string? CollapseKey { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public TimeSpan? TimeToLive { get; set; }
}

// Scheduling DTOs
public class ScheduledNotificationDto
{
    public Guid Id { get; set; }
    public Guid? TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public NotificationDto? Notification { get; set; }
    public List<string> Recipients { get; set; } = new();
    public List<string> EmailRecipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public DateTime ScheduledAt { get; set; }
    public NotificationScheduleStatus Status { get; set; }
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public string? TimeZone { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public ReportConfigDto ReportConfig { get; set; } = new();
}

public class ReportConfigDto
{
    public Guid? TemplateId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class CreateNotificationSubscriptionDto
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public Dictionary<string, object> Criteria { get; set; } = new();
}

// Enums
public enum NotificationType
{
    General = 1,
    TaskAssigned = 2,
    TaskCompleted = 3,
    TaskOverdue = 4,
    CommentAdded = 5,
    ProjectUpdated = 6,
    ProjectInvitation = 7,
    FileShared = 8,
    Reminder = 9,
    System = 10,
    Marketing = 11,
    Alert = 12,
    Critical = 13
}

public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

public enum NotificationStatus
{
    Created = 1,
    Queued = 2,
    Sending = 3,
    Sent = 4,
    Delivered = 5,
    Read = 6,
    Failed = 7,
    Expired = 8
}

public enum DevicePlatform
{
    iOS = 1,
    Android = 2,
    Windows = 3,
    macOS = 4,
    Web = 5
}

public enum NotificationChannel
{
    Push = 1,
    Email = 2,
    InApp = 3,
    SMS = 4
}

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5,
    Expired = 6
}

public enum NotificationScheduleStatus
{
    Pending = 1,
    Executed = 2,
    Failed = 3,
    Cancelled = 4
}