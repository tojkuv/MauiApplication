using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.DTOs;

public class SyncItemDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Data { get; set; } = string.Empty; // JSON payload
    public DateTime Timestamp { get; set; }
    public SyncStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastRetry { get; set; }
}

public class SyncRequestDto
{
    [Required]
    public Guid ClientId { get; set; }
    
    [Required]
    public DateTime LastSyncTimestamp { get; set; }
    
    public List<SyncItemDto> ClientChanges { get; set; } = new();
    
    public string ClientVersion { get; set; } = string.Empty;
    
    public Dictionary<string, DateTime> EntityTimestamps { get; set; } = new();
}

public class SyncResponseDto
{
    public DateTime ServerTimestamp { get; set; }
    public List<SyncItemDto> ServerChanges { get; set; } = new();
    public List<ConflictResolutionDto> Conflicts { get; set; } = new();
    public List<SyncErrorDto> Errors { get; set; } = new();
    public bool HasMoreData { get; set; }
    public string? NextToken { get; set; }
    public SyncStatistics Statistics { get; set; } = new();
}

public class ConflictResolutionDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string ClientData { get; set; } = string.Empty; // JSON
    public string ServerData { get; set; } = string.Empty; // JSON
    public DateTime ClientTimestamp { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public ConflictResolutionStrategy RecommendedStrategy { get; set; }
    public string ConflictReason { get; set; } = string.Empty;
}

public class SyncErrorDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
}

public class SyncStatistics
{
    public int TotalItemsProcessed { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, int> EntityTypeCounts { get; set; } = new();
}

public class DeltaSyncRequestDto
{
    [Required]
    public Guid ClientId { get; set; }
    
    [Required]
    public string EntityType { get; set; } = string.Empty;
    
    public DateTime? LastSyncTimestamp { get; set; }
    
    public int PageSize { get; set; } = 100;
    
    public string? ContinuationToken { get; set; }
    
    public List<Guid> RequestedEntityIds { get; set; } = new();
}

public class DeltaSyncResponseDto
{
    public string EntityType { get; set; } = string.Empty;
    public List<SyncItemDto> Changes { get; set; } = new();
    public DateTime ServerTimestamp { get; set; }
    public bool HasMoreData { get; set; }
    public string? ContinuationToken { get; set; }
    public int TotalChanges { get; set; }
}

public class SyncStatusDto
{
    public Guid ClientId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public Dictionary<string, DateTime> EntityLastSyncTimestamps { get; set; } = new();
    public List<PendingSyncItemDto> PendingItems { get; set; } = new();
    public SyncHealth Health { get; set; } = new();
}

public class PendingSyncItemDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncHealth
{
    public bool IsHealthy { get; set; }
    public int PendingItemsCount { get; set; }
    public int FailedItemsCount { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public TimeSpan? TimeSinceLastSync { get; set; }
    public List<string> HealthIssues { get; set; } = new();
}

public class ConflictResolutionRequestDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public ConflictResolutionStrategy Strategy { get; set; }
    public string? CustomResolutionData { get; set; } // JSON for manual resolution
    public Guid UserId { get; set; }
}

public class ConflictResolutionResponseDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ResolvedData { get; set; } = string.Empty; // JSON
    public DateTime ResolutionTimestamp { get; set; }
}

public class SyncConfigurationDto
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
    public int BatchSize { get; set; } = 100;
    public bool AutoResolveConflicts { get; set; } = false;
    public ConflictResolutionStrategy DefaultConflictStrategy { get; set; } = ConflictResolutionStrategy.ServerWins;
    public Dictionary<string, EntitySyncConfig> EntityConfigurations { get; set; } = new();
}

public class EntitySyncConfig
{
    public string EntityType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ConflictResolutionStrategy DefaultStrategy { get; set; } = ConflictResolutionStrategy.ServerWins;
    public int Priority { get; set; } = 1; // 1 = highest, 10 = lowest
    public bool RequiresManualConflictResolution { get; set; } = false;
    public List<string> ConflictFields { get; set; } = new(); // Fields to check for conflicts
}

// Enums
public enum SyncStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Conflict = 6
}

public enum ConflictResolutionStrategy
{
    ClientWins = 1,
    ServerWins = 2,
    LastWriterWins = 3,
    ManualResolution = 4,
    MergeChanges = 5,
    CreateDuplicate = 6
}

public enum SyncDirection
{
    Upload = 1,
    Download = 2,
    Bidirectional = 3
}