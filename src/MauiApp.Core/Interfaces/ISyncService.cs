using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface ISyncService
{
    // Main sync operations
    Task<SyncResponseDto> ProcessSyncRequestAsync(SyncRequestDto request, Guid userId);
    Task<DeltaSyncResponseDto> GetDeltaChangesAsync(DeltaSyncRequestDto request, Guid userId);
    Task<SyncStatusDto> GetSyncStatusAsync(Guid clientId, Guid userId);

    // Conflict resolution
    Task<List<ConflictResolutionDto>> GetPendingConflictsAsync(Guid clientId, Guid userId);
    Task<ConflictResolutionResponseDto> ResolveConflictAsync(ConflictResolutionRequestDto request, Guid userId);
    Task<bool> AutoResolveConflictsAsync(Guid clientId, Guid userId);

    // Sync item management
    Task<SyncItemDto> CreateSyncItemAsync(string entityType, Guid entityId, string operation, string data, Guid userId);
    Task<bool> MarkSyncItemCompletedAsync(Guid syncItemId, Guid userId);
    Task<bool> MarkSyncItemFailedAsync(Guid syncItemId, string errorMessage, Guid userId);
    Task<List<SyncItemDto>> GetPendingSyncItemsAsync(Guid clientId, Guid userId, int maxItems = 100);

    // Sync configuration
    Task<SyncConfigurationDto> GetSyncConfigurationAsync(Guid userId);
    Task<bool> UpdateSyncConfigurationAsync(SyncConfigurationDto configuration, Guid userId);

    // Health and monitoring
    Task<SyncHealth> GetSyncHealthAsync(Guid clientId, Guid userId);
    Task ProcessFailedSyncItemsAsync(Guid clientId, Guid userId);
    Task CleanupOldSyncDataAsync(DateTime cutoffDate);

    // Client management
    Task<Guid> RegisterClientAsync(string clientInfo, Guid userId);
    Task<bool> UpdateClientLastSeenAsync(Guid clientId, Guid userId);
    Task<List<Guid>> GetActiveClientsAsync(Guid userId);

    // Entity-specific sync
    Task<List<SyncItemDto>> GetEntityChangesAsync(string entityType, DateTime since, Guid userId, int maxItems = 100);
    Task<bool> ApplyEntityChangeAsync(SyncItemDto syncItem, Guid userId);
    Task<bool> ValidateEntityChangeAsync(SyncItemDto syncItem, Guid userId);

    // Batch operations
    Task<SyncResponseDto> ProcessBatchSyncAsync(List<SyncRequestDto> requests, Guid userId);
    Task<bool> QueueBatchSyncItemsAsync(List<SyncItemDto> items, Guid userId);

    // Real-time sync notifications
    Task NotifyClientsOfChangesAsync(string entityType, Guid entityId, string operation, Guid userId);
    Task<bool> SubscribeToEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId);
    Task<bool> UnsubscribeFromEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId);
}