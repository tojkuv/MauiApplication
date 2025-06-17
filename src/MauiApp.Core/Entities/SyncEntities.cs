using MauiApp.Core.DTOs;
using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class SyncItem : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Data { get; set; } = string.Empty; // JSON payload
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    public DateTime? LastRetry { get; set; }
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual SyncClient Client { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}

public class SyncClient : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ClientInfo { get; set; } = string.Empty; // Device info, app version, etc.
    public DateTime LastSyncTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, DateTime> EntityLastSyncTimestamps { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<SyncItem> SyncItems { get; set; } = new List<SyncItem>();
    public virtual ICollection<SyncConflict> Conflicts { get; set; } = new List<SyncConflict>();
}

public class SyncConflict : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string ClientData { get; set; } = string.Empty; // JSON
    public string ServerData { get; set; } = string.Empty; // JSON
    public DateTime ClientTimestamp { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public ConflictResolutionStrategy RecommendedStrategy { get; set; }
    public ConflictResolutionStrategy? AppliedStrategy { get; set; }
    public string ConflictReason { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
    public string? ResolutionData { get; set; } // JSON for resolved data
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual SyncClient Client { get; set; } = null!;
    public virtual ApplicationUser? ResolvedByUser { get; set; }
}

public class SyncConfiguration : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 1;
    public int BatchSize { get; set; } = 100;
    public bool AutoResolveConflicts { get; set; } = false;
    public ConflictResolutionStrategy DefaultConflictStrategy { get; set; } = ConflictResolutionStrategy.ServerWins;
    public string EntityConfigurations { get; set; } = "{}"; // JSON serialized EntitySyncConfig dictionary
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}

public class SyncLog : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid? SyncItemId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string LogLevel { get; set; } = "Info"; // Info, Warning, Error
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; } // JSON for additional details
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? Duration { get; set; }

    // Navigation properties
    public virtual SyncClient Client { get; set; } = null!;
    public virtual SyncItem? SyncItem { get; set; }
}

public class EntitySubscription : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastNotificationAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual SyncClient Client { get; set; } = null!;
}