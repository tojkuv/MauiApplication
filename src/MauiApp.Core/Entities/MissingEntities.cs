using MauiApp.Core.Data;
using MauiApp.Core.DTOs;

namespace MauiApp.Core.Entities;

// ========================================================
// MISSING ENTITIES FOR COMPREHENSIVE SCHEMA
// ========================================================


public class FileVersion : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FileId { get; set; }
    public int VersionNumber { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ProjectFile File { get; set; } = null!;
    public virtual ApplicationUser UploadedBy { get; set; } = null!;
}


public class WeeklyMetric : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime WeekStart { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MonthlyMetric : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime MonthStart { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AnalyticsCache : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CacheKey { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty; // JSON data
    public string DataType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

public class DailyMetric : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; }
    public string MetricType { get; set; } = string.Empty; // "productivity", "completion", "time"
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty; // "user", "project", "global"
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; } // JSON for additional data
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserActivityLog : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty; // "task_created", "comment_added", etc.
    public string Description { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? AdditionalData { get; set; } // JSON for extra context
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}