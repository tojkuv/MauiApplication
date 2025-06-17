using MauiApp.Core.DTOs;
using MauiApp.Core.Data;
using System.Text.Json;

namespace MauiApp.Core.Entities;

public class Notification : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? SenderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Created;
    public string DataJson { get; set; } = "{}"; // JSON serialized dictionary
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public bool IsScheduled { get; set; } = false;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser? Sender { get; set; }
    public virtual ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();

    // Helper property for data dictionary
    public Dictionary<string, string> Data
    {
        get => JsonSerializer.Deserialize<Dictionary<string, string>>(DataJson) ?? new Dictionary<string, string>();
        set => DataJson = JsonSerializer.Serialize(value);
    }
}

public class NotificationDelivery : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetry { get; set; }
    public string? ExternalId { get; set; } // For tracking with external services

    // Navigation properties
    public virtual Notification Notification { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}

public class DeviceToken : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}

public class NotificationPreferences : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public bool PushNotificationsEnabled { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool InAppNotificationsEnabled { get; set; } = true;
    public string TypePreferencesJson { get; set; } = "{}"; // JSON serialized type preferences
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
    public string QuietHoursDaysJson { get; set; } = "[]"; // JSON serialized list of days
    public int MaxNotificationsPerHour { get; set; } = 10;
    public int MaxNotificationsPerDay { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;

    // Helper properties
    public Dictionary<NotificationType, NotificationChannelPreferences> TypePreferences
    {
        get => JsonSerializer.Deserialize<Dictionary<NotificationType, NotificationChannelPreferences>>(TypePreferencesJson) 
               ?? new Dictionary<NotificationType, NotificationChannelPreferences>();
        set => TypePreferencesJson = JsonSerializer.Serialize(value);
    }

    public List<DayOfWeek> QuietHoursDays
    {
        get => JsonSerializer.Deserialize<List<DayOfWeek>>(QuietHoursDaysJson) ?? new List<DayOfWeek>();
        set => QuietHoursDaysJson = JsonSerializer.Serialize(value);
    }
}

public class NotificationTemplate : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public string DefaultDataJson { get; set; } = "{}"; // JSON serialized default data
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser CreatedBy { get; set; } = null!;

    // Helper property
    public Dictionary<string, string> DefaultData
    {
        get => JsonSerializer.Deserialize<Dictionary<string, string>>(DefaultDataJson) ?? new Dictionary<string, string>();
        set => DefaultDataJson = JsonSerializer.Serialize(value);
    }
}

public class NotificationQueue : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetry { get; set; }

    // Navigation properties
    public virtual Notification Notification { get; set; } = null!;
}

public class NotificationStats : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; }
    public NotificationType? Type { get; set; }
    public DevicePlatform? Platform { get; set; }
    public NotificationChannel? Channel { get; set; }
    public int TotalSent { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalRead { get; set; }
    public int TotalFailed { get; set; }
    public double DeliveryRate { get; set; }
    public double ReadRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationSubscription : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "Project", "Task", etc.
    public Guid EntityId { get; set; }
    public List<NotificationType> SubscribedTypes { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}

public class NotificationLog : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? NotificationId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // "created", "sent", "delivered", "read", "failed"
    public string Details { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Navigation properties
    public virtual Notification? Notification { get; set; }
    public virtual ApplicationUser? User { get; set; }
}