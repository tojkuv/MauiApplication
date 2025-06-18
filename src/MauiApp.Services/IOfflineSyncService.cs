using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface IOfflineSyncService
{
    // Sync Operations
    Task<SyncResult> SyncAllDataAsync(bool forceFullSync = false);
    Task<SyncResult> SyncEntityAsync<T>(string entityType, Guid entityId) where T : class;
    Task<SyncResult> SyncEntityTypeAsync(string entityType, DateTime? lastSyncTime = null);
    
    // Conflict Resolution
    Task<List<ConflictResolutionDto>> GetPendingConflictsAsync();
    Task<bool> ResolveConflictAsync(Guid conflictId, ConflictResolutionStrategy strategy, object? customResolution = null);
    Task<bool> ResolveAllConflictsAsync(ConflictResolutionStrategy defaultStrategy);
    
    // Offline Queue Management
    Task<bool> QueueOfflineActionAsync(OfflineActionDto action);
    Task<List<OfflineActionDto>> GetPendingActionsAsync();
    Task<bool> ProcessPendingActionsAsync();
    Task<bool> ClearPendingActionsAsync();
    
    // Sync Status and Monitoring
    Task<SyncStatusDto> GetSyncStatusAsync();
    Task<List<SyncHistoryDto>> GetSyncHistoryAsync(int maxRecords = 50);
    Task<bool> IsSyncInProgressAsync();
    Task<DateTime?> GetLastSyncTimeAsync(string? entityType = null);
    
    // Advanced Conflict Resolution
    Task<ConflictAnalysisDto> AnalyzeConflictAsync(Guid conflictId);
    Task<List<ConflictResolutionSuggestionDto>> GetConflictResolutionSuggestionsAsync(Guid conflictId);
    Task<bool> ApplyCustomMergeStrategyAsync(Guid conflictId, Dictionary<string, object> mergeRules);
    
    // Data Integrity and Validation
    Task<DataIntegrityReportDto> ValidateDataIntegrityAsync();
    Task<bool> RepairDataInconsistenciesAsync();
    Task<List<SyncErrorDto>> GetSyncErrorsAsync(DateTime? since = null);
    
    // Advanced Sync Control
    Task<bool> PauseSyncAsync();
    Task<bool> ResumeSyncAsync();
    Task<bool> SetSyncIntervalAsync(TimeSpan interval);
    Task<bool> ConfigureSyncPriorityAsync(string entityType, SyncPriority priority);
    Task<bool> EnableSelectiveSyncAsync(List<string> entityTypes);
    
    // Bandwidth and Performance
    Task<bool> SetBandwidthLimitAsync(long bytesPerSecond);
    Task<SyncPerformanceMetricsDto> GetPerformanceMetricsAsync();
    Task<bool> OptimizeSyncPerformanceAsync();
    
    // Events
    event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;
    event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    event EventHandler<SyncErrorEventArgs>? SyncErrorOccurred;
}

// Supporting DTOs and Models
public class SyncResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int EntitiesProcessed { get; set; }
    public int EntitiesUpdated { get; set; }
    public int EntitiesCreated { get; set; }
    public int EntitiesDeleted { get; set; }
    public int ConflictsDetected { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public List<SyncItemResult> ItemResults { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SyncItemResult
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public SyncOperation Operation { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public ConflictResolutionDto? Conflict { get; set; }
}

public class ConflictResolutionDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public ConflictType Type { get; set; }
    public object LocalVersion { get; set; } = new();
    public object ServerVersion { get; set; } = new();
    public object? BaseVersion { get; set; }
    public DateTime LocalModifiedAt { get; set; }
    public DateTime ServerModifiedAt { get; set; }
    public string LocalModifiedBy { get; set; } = string.Empty;
    public string ServerModifiedBy { get; set; } = string.Empty;
    public List<FieldConflictDto> FieldConflicts { get; set; } = new();
    public ConflictSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public ConflictResolutionStatus Status { get; set; } = ConflictResolutionStatus.Pending;
}

public class FieldConflictDto
{
    public string FieldName { get; set; } = string.Empty;
    public object? LocalValue { get; set; }
    public object? ServerValue { get; set; }
    public object? BaseValue { get; set; }
    public FieldConflictType Type { get; set; }
}

public class OfflineActionDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public SyncOperation Operation { get; set; }
    public object? Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime? ScheduledAt { get; set; }
    public OfflineActionStatus Status { get; set; } = OfflineActionStatus.Pending;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SyncStatusDto
{
    public bool IsSyncEnabled { get; set; }
    public bool IsSyncInProgress { get; set; }
    public bool IsSyncPaused { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public DateTime? NextScheduledSync { get; set; }
    public int PendingActions { get; set; }
    public int PendingConflicts { get; set; }
    public int PendingDownloads { get; set; }
    public int PendingUploads { get; set; }
    public SyncHealthStatus Health { get; set; } = SyncHealthStatus.Healthy;
    public List<string> Issues { get; set; } = new();
    public Dictionary<string, DateTime> LastSyncByEntityType { get; set; } = new();
}

public class SyncHistoryDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public SyncType Type { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int EntitiesProcessed { get; set; }
    public int EntitiesUpdated { get; set; }
    public int EntitiesCreated { get; set; }
    public int EntitiesDeleted { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public long DataTransferred { get; set; }
    public Dictionary<string, int> EntityTypeBreakdown { get; set; } = new();
}

public class ConflictAnalysisDto
{
    public Guid ConflictId { get; set; }
    public ConflictComplexity Complexity { get; set; }
    public double SimilarityScore { get; set; }
    public List<string> AffectedFields { get; set; } = new();
    public List<string> CriticalFields { get; set; } = new();
    public ConflictImpactAssessment Impact { get; set; } = new();
    public List<ConflictResolutionSuggestionDto> Suggestions { get; set; } = new();
    public Dictionary<string, object> AnalysisMetadata { get; set; } = new();
}

public class ConflictImpactAssessment
{
    public ConflictImpactLevel DataLoss { get; set; }
    public ConflictImpactLevel BusinessImpact { get; set; }
    public ConflictImpactLevel UserExperience { get; set; }
    public List<string> RisksIdentified { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class ConflictResolutionSuggestionDto
{
    public ConflictResolutionStrategy Strategy { get; set; }
    public string Description { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public ConflictImpactLevel RiskLevel { get; set; }
    public object? PreviewResult { get; set; }
    public List<string> Pros { get; set; } = new();
    public List<string> Cons { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class DataIntegrityReportDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DataIntegrityStatus OverallStatus { get; set; }
    public int TotalEntitiesChecked { get; set; }
    public int InconsistenciesFound { get; set; }
    public int CriticalIssues { get; set; }
    public int WarningIssues { get; set; }
    public List<DataIntegrityIssueDto> Issues { get; set; } = new();
    public Dictionary<string, int> IssuesByEntityType { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class DataIntegrityIssueDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DataIntegrityIssueType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DataIntegrityIssueSeverity Severity { get; set; }
    public bool IsAutoFixable { get; set; }
    public string? SuggestedFix { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public class SyncErrorDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public SyncOperation Operation { get; set; }
    public SyncErrorType ErrorType { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

public class SyncPerformanceMetricsDto
{
    public DateTime MeasurementPeriodStart { get; set; }
    public DateTime MeasurementPeriodEnd { get; set; }
    public TimeSpan TotalSyncTime { get; set; }
    public long TotalDataTransferred { get; set; }
    public double AverageTransferRate { get; set; }
    public double PeakTransferRate { get; set; }
    public int TotalSyncOperations { get; set; }
    public TimeSpan AverageOperationTime { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, TimeSpan> EntityTypePerformance { get; set; } = new();
    public List<string> PerformanceBottlenecks { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
}

// Event Args
public class SyncProgressEventArgs : EventArgs
{
    public string EntityType { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public double ProgressPercentage { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
}

public class ConflictDetectedEventArgs : EventArgs
{
    public ConflictResolutionDto Conflict { get; set; } = new();
}

public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = new();
}

public class SyncErrorEventArgs : EventArgs
{
    public SyncErrorDto Error { get; set; } = new();
}

// Enums
public enum SyncOperation
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Read = 4
}

public enum ConflictType
{
    UpdateUpdate = 1,
    UpdateDelete = 2,
    DeleteUpdate = 3,
    CreateCreate = 4,
    FieldLevel = 5,
    Structural = 6
}

public enum ConflictSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum FieldConflictType
{
    ValueDifference = 1,
    TypeMismatch = 2,
    FormatDifference = 3,
    RangeMismatch = 4
}

public enum ConflictResolutionStatus
{
    Pending = 1,
    Resolving = 2,
    Resolved = 3,
    Failed = 4,
    Ignored = 5
}

public enum ConflictResolutionStrategy
{
    TakeLocal = 1,
    TakeServer = 2,
    Merge = 3,
    Custom = 4,
    Interactive = 5,
    LastModifiedWins = 6,
    FirstModifiedWins = 7,
    UserDecision = 8
}

public enum OfflineActionStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum SyncHealthStatus
{
    Healthy = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum SyncType
{
    Full = 1,
    Incremental = 2,
    EntitySpecific = 3,
    Manual = 4,
    Scheduled = 5
}

public enum ConflictComplexity
{
    Simple = 1,
    Moderate = 2,
    Complex = 3,
    VeryComplex = 4
}

public enum ConflictImpactLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum DataIntegrityStatus
{
    Healthy = 1,
    MinorIssues = 2,
    MajorIssues = 3,
    Critical = 4
}

public enum DataIntegrityIssueType
{
    MissingReference = 1,
    OrphanedRecord = 2,
    DuplicateRecord = 3,
    InvalidData = 4,
    ConstraintViolation = 5,
    VersionMismatch = 6,
    ChecksumMismatch = 7
}

public enum DataIntegrityIssueSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum SyncErrorType
{
    NetworkError = 1,
    AuthenticationError = 2,
    ValidationError = 3,
    ConflictError = 4,
    DataError = 5,
    SystemError = 6,
    TimeoutError = 7
}

public enum SyncPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}