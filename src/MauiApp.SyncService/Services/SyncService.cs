using Microsoft.EntityFrameworkCore;
using MauiApp.SyncService.Data;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Entities;
using Newtonsoft.Json;
using System.Diagnostics;

namespace MauiApp.SyncService.Services;

public class SyncService : ISyncService
{
    private readonly SyncDbContext _context;
    private readonly ILogger<SyncService> _logger;

    public SyncService(SyncDbContext context, ILogger<SyncService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SyncResponseDto> ProcessSyncRequestAsync(SyncRequestDto request, Guid userId)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SyncResponseDto
        {
            ServerTimestamp = DateTime.UtcNow,
            Statistics = new SyncStatistics()
        };

        try
        {
            _logger.LogInformation("Processing sync request for client {ClientId}, user {UserId}", request.ClientId, userId);

            // Ensure client exists
            await EnsureClientExistsAsync(request.ClientId, userId);

            // Process client changes
            await ProcessClientChangesAsync(request.ClientChanges, request.ClientId, userId, response);

            // Get server changes since last sync
            var serverChanges = await GetServerChangesSinceAsync(request.LastSyncTimestamp, request.ClientId, userId);
            response.ServerChanges = serverChanges;

            // Update client's last sync timestamp
            await UpdateClientLastSyncAsync(request.ClientId, response.ServerTimestamp);

            stopwatch.Stop();
            response.Statistics.ProcessingTime = stopwatch.Elapsed;
            response.Statistics.TotalItemsProcessed = request.ClientChanges.Count + serverChanges.Count;

            _logger.LogInformation("Sync request processed successfully for client {ClientId}", request.ClientId);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sync request for client {ClientId}", request.ClientId);
            throw;
        }
    }

    public async Task<DeltaSyncResponseDto> GetDeltaChangesAsync(DeltaSyncRequestDto request, Guid userId)
    {
        try
        {
            var query = _context.SyncItems
                .Where(si => si.EntityType == request.EntityType &&
                           si.UserId == userId &&
                           si.Status == SyncStatus.Completed);

            if (request.LastSyncTimestamp.HasValue)
            {
                query = query.Where(si => si.Timestamp > request.LastSyncTimestamp.Value);
            }

            if (request.RequestedEntityIds.Any())
            {
                query = query.Where(si => request.RequestedEntityIds.Contains(si.EntityId));
            }

            var totalChanges = await query.CountAsync();
            var changes = await query
                .OrderBy(si => si.Timestamp)
                .Skip(GetSkipCount(request.ContinuationToken))
                .Take(request.PageSize)
                .Select(si => new SyncItemDto
                {
                    Id = si.Id,
                    EntityType = si.EntityType,
                    EntityId = si.EntityId,
                    Operation = si.Operation,
                    Data = si.Data,
                    Timestamp = si.Timestamp,
                    Status = si.Status,
                    RetryCount = si.RetryCount,
                    ErrorMessage = si.ErrorMessage,
                    LastRetry = si.LastRetry
                })
                .ToListAsync();

            var hasMoreData = changes.Count == request.PageSize;
            var nextToken = hasMoreData ? GenerateContinuationToken(changes.LastOrDefault()?.Timestamp) : null;

            return new DeltaSyncResponseDto
            {
                EntityType = request.EntityType,
                Changes = changes,
                ServerTimestamp = DateTime.UtcNow,
                HasMoreData = hasMoreData,
                ContinuationToken = nextToken,
                TotalChanges = totalChanges
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delta changes for entity type {EntityType}", request.EntityType);
            throw;
        }
    }

    public async Task<SyncStatusDto> GetSyncStatusAsync(Guid clientId, Guid userId)
    {
        try
        {
            var client = await _context.SyncClients
                .FirstOrDefaultAsync(c => c.Id == clientId && c.UserId == userId);

            if (client == null)
                throw new ArgumentException("Client not found");

            var pendingItems = await _context.SyncItems
                .Where(si => si.ClientId == clientId && si.Status != SyncStatus.Completed)
                .Select(si => new PendingSyncItemDto
                {
                    Id = si.Id,
                    EntityType = si.EntityType,
                    EntityId = si.EntityId,
                    Operation = si.Operation,
                    Timestamp = si.Timestamp,
                    RetryCount = si.RetryCount,
                    ErrorMessage = si.ErrorMessage
                })
                .ToListAsync();

            var failedItemsCount = pendingItems.Count(p => p.RetryCount > 0);
            var lastSuccessfulSync = await _context.SyncItems
                .Where(si => si.ClientId == clientId && si.Status == SyncStatus.Completed)
                .MaxAsync(si => (DateTime?)si.Timestamp);

            var health = new SyncHealth
            {
                IsHealthy = failedItemsCount == 0 && pendingItems.Count < 100,
                PendingItemsCount = pendingItems.Count,
                FailedItemsCount = failedItemsCount,
                LastSuccessfulSync = lastSuccessfulSync,
                TimeSinceLastSync = lastSuccessfulSync.HasValue ? DateTime.UtcNow - lastSuccessfulSync.Value : null
            };

            if (!health.IsHealthy)
            {
                health.HealthIssues.Add($"{failedItemsCount} failed sync items");
                if (pendingItems.Count >= 100)
                    health.HealthIssues.Add("Too many pending sync items");
            }

            return new SyncStatusDto
            {
                ClientId = clientId,
                LastSyncTimestamp = client.LastSyncTimestamp,
                EntityLastSyncTimestamps = client.EntityLastSyncTimestamps,
                PendingItems = pendingItems,
                Health = health
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<List<ConflictResolutionDto>> GetPendingConflictsAsync(Guid clientId, Guid userId)
    {
        try
        {
            return await _context.SyncConflicts
                .Where(sc => sc.ClientId == clientId && !sc.IsResolved)
                .Select(sc => new ConflictResolutionDto
                {
                    EntityId = sc.EntityId,
                    EntityType = sc.EntityType,
                    ClientData = sc.ClientData,
                    ServerData = sc.ServerData,
                    ClientTimestamp = sc.ClientTimestamp,
                    ServerTimestamp = sc.ServerTimestamp,
                    RecommendedStrategy = sc.RecommendedStrategy,
                    ConflictReason = sc.ConflictReason
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending conflicts for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<ConflictResolutionResponseDto> ResolveConflictAsync(ConflictResolutionRequestDto request, Guid userId)
    {
        try
        {
            var conflict = await _context.SyncConflicts
                .FirstOrDefaultAsync(sc => sc.EntityId == request.EntityId && 
                                         sc.EntityType == request.EntityType && 
                                         !sc.IsResolved);

            if (conflict == null)
                return new ConflictResolutionResponseDto { Success = false, ErrorMessage = "Conflict not found" };

            var resolvedData = await ApplyConflictResolutionAsync(conflict, request.Strategy, request.CustomResolutionData);

            conflict.IsResolved = true;
            conflict.AppliedStrategy = request.Strategy;
            conflict.ResolutionData = resolvedData;
            conflict.ResolvedByUserId = userId;
            conflict.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ConflictResolutionResponseDto
            {
                EntityId = request.EntityId,
                EntityType = request.EntityType,
                Success = true,
                ResolvedData = resolvedData,
                ResolutionTimestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict for entity {EntityId}", request.EntityId);
            return new ConflictResolutionResponseDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> AutoResolveConflictsAsync(Guid clientId, Guid userId)
    {
        try
        {
            var config = await GetSyncConfigurationAsync(userId);
            if (!config.AutoResolveConflicts)
                return false;

            var conflicts = await _context.SyncConflicts
                .Where(sc => sc.ClientId == clientId && !sc.IsResolved)
                .ToListAsync();

            foreach (var conflict in conflicts)
            {
                var strategy = config.EntityConfigurations.ContainsKey(conflict.EntityType)
                    ? config.EntityConfigurations[conflict.EntityType].DefaultStrategy
                    : config.DefaultConflictStrategy;

                if (strategy != ConflictResolutionStrategy.ManualResolution)
                {
                    var resolvedData = await ApplyConflictResolutionAsync(conflict, strategy, null);
                    conflict.IsResolved = true;
                    conflict.AppliedStrategy = strategy;
                    conflict.ResolutionData = resolvedData;
                    conflict.ResolvedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-resolving conflicts for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<SyncItemDto> CreateSyncItemAsync(string entityType, Guid entityId, string operation, string data, Guid userId)
    {
        try
        {
            var syncItem = new SyncItem
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = operation,
                Data = data,
                UserId = userId,
                ClientId = Guid.Empty, // Will be set when processed by specific client
                Status = SyncStatus.Pending,
                Timestamp = DateTime.UtcNow
            };

            _context.SyncItems.Add(syncItem);
            await _context.SaveChangesAsync();

            // Notify subscribed clients
            await NotifyClientsOfChangesAsync(entityType, entityId, operation, userId);

            return new SyncItemDto
            {
                Id = syncItem.Id,
                EntityType = syncItem.EntityType,
                EntityId = syncItem.EntityId,
                Operation = syncItem.Operation,
                Data = syncItem.Data,
                Timestamp = syncItem.Timestamp,
                Status = syncItem.Status,
                RetryCount = syncItem.RetryCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sync item for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<bool> MarkSyncItemCompletedAsync(Guid syncItemId, Guid userId)
    {
        try
        {
            var syncItem = await _context.SyncItems
                .FirstOrDefaultAsync(si => si.Id == syncItemId && si.UserId == userId);

            if (syncItem == null)
                return false;

            syncItem.Status = SyncStatus.Completed;
            syncItem.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sync item {SyncItemId} as completed", syncItemId);
            return false;
        }
    }

    public async Task<bool> MarkSyncItemFailedAsync(Guid syncItemId, string errorMessage, Guid userId)
    {
        try
        {
            var syncItem = await _context.SyncItems
                .FirstOrDefaultAsync(si => si.Id == syncItemId && si.UserId == userId);

            if (syncItem == null)
                return false;

            syncItem.Status = SyncStatus.Failed;
            syncItem.ErrorMessage = errorMessage;
            syncItem.RetryCount++;
            syncItem.LastRetry = DateTime.UtcNow;
            syncItem.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sync item {SyncItemId} as failed", syncItemId);
            return false;
        }
    }

    public async Task<List<SyncItemDto>> GetPendingSyncItemsAsync(Guid clientId, Guid userId, int maxItems = 100)
    {
        try
        {
            return await _context.SyncItems
                .Where(si => si.ClientId == clientId && 
                           si.UserId == userId && 
                           si.Status == SyncStatus.Pending)
                .OrderBy(si => si.Timestamp)
                .Take(maxItems)
                .Select(si => new SyncItemDto
                {
                    Id = si.Id,
                    EntityType = si.EntityType,
                    EntityId = si.EntityId,
                    Operation = si.Operation,
                    Data = si.Data,
                    Timestamp = si.Timestamp,
                    Status = si.Status,
                    RetryCount = si.RetryCount,
                    ErrorMessage = si.ErrorMessage,
                    LastRetry = si.LastRetry
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending sync items for client {ClientId}", clientId);
            throw;
        }
    }

    // Additional interface methods with basic implementations
    public async Task<SyncConfigurationDto> GetSyncConfigurationAsync(Guid userId)
    {
        var config = await _context.SyncConfigurations
            .FirstOrDefaultAsync(sc => sc.UserId == userId);

        if (config == null)
        {
            return new SyncConfigurationDto(); // Return default configuration
        }

        var entityConfigs = JsonConvert.DeserializeObject<Dictionary<string, EntitySyncConfig>>(config.EntityConfigurations) 
                           ?? new Dictionary<string, EntitySyncConfig>();

        return new SyncConfigurationDto
        {
            MaxRetryAttempts = config.MaxRetryAttempts,
            RetryDelay = TimeSpan.FromMinutes(config.RetryDelayMinutes),
            BatchSize = config.BatchSize,
            AutoResolveConflicts = config.AutoResolveConflicts,
            DefaultConflictStrategy = config.DefaultConflictStrategy,
            EntityConfigurations = entityConfigs
        };
    }

    public async Task<bool> UpdateSyncConfigurationAsync(SyncConfigurationDto configuration, Guid userId)
    {
        try
        {
            var config = await _context.SyncConfigurations
                .FirstOrDefaultAsync(sc => sc.UserId == userId);

            if (config == null)
            {
                config = new SyncConfiguration { UserId = userId };
                _context.SyncConfigurations.Add(config);
            }

            config.MaxRetryAttempts = configuration.MaxRetryAttempts;
            config.RetryDelayMinutes = (int)configuration.RetryDelay.TotalMinutes;
            config.BatchSize = configuration.BatchSize;
            config.AutoResolveConflicts = configuration.AutoResolveConflicts;
            config.DefaultConflictStrategy = configuration.DefaultConflictStrategy;
            config.EntityConfigurations = JsonConvert.SerializeObject(configuration.EntityConfigurations);
            config.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync configuration for user {UserId}", userId);
            return false;
        }
    }

    public async Task<SyncHealth> GetSyncHealthAsync(Guid clientId, Guid userId)
    {
        var status = await GetSyncStatusAsync(clientId, userId);
        return status.Health;
    }

    public async Task ProcessFailedSyncItemsAsync(Guid clientId, Guid userId)
    {
        var failedItems = await _context.SyncItems
            .Where(si => si.ClientId == clientId && si.Status == SyncStatus.Failed)
            .ToListAsync();

        foreach (var item in failedItems)
        {
            if (item.RetryCount < 3) // Max retry attempts
            {
                item.Status = SyncStatus.Pending;
                item.RetryCount++;
                item.LastRetry = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task CleanupOldSyncDataAsync(DateTime cutoffDate)
    {
        var oldItems = await _context.SyncItems
            .Where(si => si.CreatedAt < cutoffDate && si.Status == SyncStatus.Completed)
            .ToListAsync();

        _context.SyncItems.RemoveRange(oldItems);

        var oldLogs = await _context.SyncLogs
            .Where(sl => sl.Timestamp < cutoffDate)
            .ToListAsync();

        _context.SyncLogs.RemoveRange(oldLogs);

        await _context.SaveChangesAsync();
    }

    public async Task<Guid> RegisterClientAsync(string clientInfo, Guid userId)
    {
        var client = new SyncClient
        {
            UserId = userId,
            ClientInfo = clientInfo,
            LastSyncTimestamp = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.SyncClients.Add(client);
        await _context.SaveChangesAsync();

        return client.Id;
    }

    public async Task<bool> UpdateClientLastSeenAsync(Guid clientId, Guid userId)
    {
        var client = await _context.SyncClients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.UserId == userId);

        if (client == null)
            return false;

        client.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Guid>> GetActiveClientsAsync(Guid userId)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24); // Consider clients active if seen in last 24 hours
        
        return await _context.SyncClients
            .Where(c => c.UserId == userId && c.IsActive && c.LastSeenAt > cutoff)
            .Select(c => c.Id)
            .ToListAsync();
    }

    // Full implementations for remaining interface methods
    public async Task<List<SyncItemDto>> GetEntityChangesAsync(string entityType, DateTime since, Guid userId, int maxItems = 100)
    {
        try
        {
            return await _context.SyncItems
                .Where(si => si.EntityType == entityType && 
                           si.UserId == userId && 
                           si.Timestamp > since && 
                           si.Status == SyncStatus.Completed)
                .OrderBy(si => si.Timestamp)
                .Take(maxItems)
                .Select(si => new SyncItemDto
                {
                    Id = si.Id,
                    EntityType = si.EntityType,
                    EntityId = si.EntityId,
                    Operation = si.Operation,
                    Data = si.Data,
                    Timestamp = si.Timestamp,
                    Status = si.Status,
                    RetryCount = si.RetryCount,
                    ErrorMessage = si.ErrorMessage,
                    LastRetry = si.LastRetry
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity changes for type {EntityType}", entityType);
            throw;
        }
    }

    public async Task<bool> ApplyEntityChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            // Validate the change first
            if (!await ValidateEntityChangeAsync(syncItem, userId))
            {
                return false;
            }

            // Apply the change based on entity type and operation
            var applied = await ApplyEntityChangeByTypeAsync(syncItem, userId);
            
            if (applied)
            {
                // Log successful application
                await LogSyncOperationAsync(syncItem.EntityId, syncItem.EntityType, 
                    syncItem.Operation, true, null, userId);
                
                // Update sync item status
                await MarkSyncItemCompletedAsync(syncItem.Id, userId);
            }
            else
            {
                // Log failed application
                await LogSyncOperationAsync(syncItem.EntityId, syncItem.EntityType, 
                    syncItem.Operation, false, "Failed to apply entity change", userId);
                
                // Mark sync item as failed
                await MarkSyncItemFailedAsync(syncItem.Id, "Failed to apply entity change", userId);
            }

            return applied;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying entity change for {EntityId}", syncItem.EntityId);
            await MarkSyncItemFailedAsync(syncItem.Id, ex.Message, userId);
            return false;
        }
    }

    public async Task<bool> ValidateEntityChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            // Basic validation
            if (syncItem.EntityId == Guid.Empty || string.IsNullOrEmpty(syncItem.EntityType))
            {
                return false;
            }

            // Entity-specific validation
            return syncItem.EntityType.ToLower() switch
            {
                "project" => await ValidateProjectChangeAsync(syncItem, userId),
                "task" => await ValidateTaskChangeAsync(syncItem, userId),
                "projectfile" => await ValidateFileChangeAsync(syncItem, userId),
                "chatmessage" => await ValidateChatMessageChangeAsync(syncItem, userId),
                _ => true // Allow unknown entity types by default
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating entity change for {EntityId}", syncItem.EntityId);
            return false;
        }
    }

    public async Task<SyncResponseDto> ProcessBatchSyncAsync(List<SyncRequestDto> requests, Guid userId)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchResponse = new SyncResponseDto
        {
            ServerTimestamp = DateTime.UtcNow,
            Statistics = new SyncStatistics()
        };

        try
        {
            _logger.LogInformation("Processing batch sync with {RequestCount} requests for user {UserId}", 
                requests.Count, userId);

            foreach (var request in requests)
            {
                try
                {
                    var response = await ProcessSyncRequestAsync(request, userId);
                    
                    // Aggregate responses
                    batchResponse.ServerChanges.AddRange(response.ServerChanges);
                    batchResponse.Conflicts.AddRange(response.Conflicts);
                    batchResponse.Errors.AddRange(response.Errors);
                    
                    // Aggregate statistics
                    batchResponse.Statistics.SuccessfulSyncs += response.Statistics.SuccessfulSyncs;
                    batchResponse.Statistics.FailedSyncs += response.Statistics.FailedSyncs;
                    batchResponse.Statistics.ConflictsDetected += response.Statistics.ConflictsDetected;
                    batchResponse.Statistics.TotalItemsProcessed += response.Statistics.TotalItemsProcessed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch request for client {ClientId}", request.ClientId);
                    batchResponse.Statistics.FailedSyncs++;
                    batchResponse.Errors.Add(new SyncErrorDto
                    {
                        EntityId = Guid.Empty,
                        EntityType = "BatchRequest",
                        Operation = "ProcessBatch",
                        ErrorMessage = ex.Message,
                        IsRetryable = true
                    });
                }
            }

            stopwatch.Stop();
            batchResponse.Statistics.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogInformation("Batch sync completed for user {UserId}. Processed {TotalRequests} requests", 
                userId, requests.Count);

            return batchResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch sync for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> QueueBatchSyncItemsAsync(List<SyncItemDto> items, Guid userId)
    {
        try
        {
            var syncItems = items.Select(dto => new SyncItem
            {
                Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                Operation = dto.Operation,
                Data = dto.Data,
                Timestamp = dto.Timestamp != default ? dto.Timestamp : DateTime.UtcNow,
                Status = SyncStatus.Pending,
                UserId = userId,
                ClientId = Guid.Empty, // Will be set when processed
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.SyncItems.AddRange(syncItems);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Queued {ItemCount} sync items for user {UserId}", items.Count, userId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing batch sync items for user {UserId}", userId);
            return false;
        }
    }

    public async Task NotifyClientsOfChangesAsync(string entityType, Guid entityId, string operation, Guid userId)
    {
        try
        {
            // Get all subscribed clients for this entity type
            var subscribedClients = await _context.EntitySubscriptions
                .Where(es => es.EntityType == entityType && es.UserId == userId && es.IsActive)
                .Select(es => es.ClientId)
                .Distinct()
                .ToListAsync();

            if (subscribedClients.Any())
            {
                // In a real implementation, you would use SignalR to notify clients
                // For now, we'll log the notification
                _logger.LogInformation("Notifying {ClientCount} clients of {Operation} on {EntityType} {EntityId}", 
                    subscribedClients.Count, operation, entityType, entityId);

                // Create notification records for each client
                var notifications = subscribedClients.Select(clientId => new SyncLog
                {
                    ClientId = clientId,
                    EntityId = entityId,
                    EntityType = entityType,
                    Operation = operation,
                    Success = true,
                    Message = $"Entity {operation} notification sent",
                    Timestamp = DateTime.UtcNow
                }).ToList();

                _context.SyncLogs.AddRange(notifications);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying clients of changes for {EntityType} {EntityId}", entityType, entityId);
        }
    }

    public async Task<bool> SubscribeToEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId)
    {
        try
        {
            // Remove existing subscriptions for these entity types
            var existingSubscriptions = await _context.EntitySubscriptions
                .Where(es => es.ClientId == clientId && es.UserId == userId && entityTypes.Contains(es.EntityType))
                .ToListAsync();

            _context.EntitySubscriptions.RemoveRange(existingSubscriptions);

            // Add new subscriptions
            var newSubscriptions = entityTypes.Select(entityType => new EntitySubscription
            {
                ClientId = clientId,
                UserId = userId,
                EntityType = entityType,
                IsActive = true,
                SubscribedAt = DateTime.UtcNow
            }).ToList();

            _context.EntitySubscriptions.AddRange(newSubscriptions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Client {ClientId} subscribed to {EntityCount} entity types", 
                clientId, entityTypes.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing client {ClientId} to entity changes", clientId);
            return false;
        }
    }

    public async Task<bool> UnsubscribeFromEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId)
    {
        try
        {
            var subscriptions = await _context.EntitySubscriptions
                .Where(es => es.ClientId == clientId && es.UserId == userId && entityTypes.Contains(es.EntityType))
                .ToListAsync();

            foreach (var subscription in subscriptions)
            {
                subscription.IsActive = false;
                subscription.UnsubscribedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Client {ClientId} unsubscribed from {EntityCount} entity types", 
                clientId, entityTypes.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing client {ClientId} from entity changes", clientId);
            return false;
        }
    }

    // Helper methods
    private async Task EnsureClientExistsAsync(Guid clientId, Guid userId)
    {
        var exists = await _context.SyncClients
            .AnyAsync(c => c.Id == clientId && c.UserId == userId);

        if (!exists)
        {
            var client = new SyncClient
            {
                Id = clientId,
                UserId = userId,
                ClientInfo = "Auto-registered",
                LastSyncTimestamp = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.SyncClients.Add(client);
            await _context.SaveChangesAsync();
        }
    }

    private async Task ProcessClientChangesAsync(List<SyncItemDto> clientChanges, Guid clientId, Guid userId, SyncResponseDto response)
    {
        foreach (var change in clientChanges)
        {
            try
            {
                // Check for conflicts
                var existingItem = await FindExistingEntityAsync(change.EntityType, change.EntityId);
                if (existingItem != null && await HasConflictAsync(change, existingItem))
                {
                    var conflict = await CreateConflictAsync(change, existingItem, clientId);
                    response.Conflicts.Add(MapToConflictDto(conflict));
                    response.Statistics.ConflictsDetected++;
                }
                else
                {
                    // Apply change
                    var syncItem = MapToSyncItem(change, clientId, userId);
                    _context.SyncItems.Add(syncItem);
                    response.Statistics.SuccessfulSyncs++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing client change for entity {EntityId}", change.EntityId);
                response.Errors.Add(new SyncErrorDto
                {
                    EntityId = change.EntityId,
                    EntityType = change.EntityType,
                    Operation = change.Operation,
                    ErrorMessage = ex.Message,
                    IsRetryable = true
                });
                response.Statistics.FailedSyncs++;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<SyncItemDto>> GetServerChangesSinceAsync(DateTime lastSync, Guid clientId, Guid userId)
    {
        return await _context.SyncItems
            .Where(si => si.UserId == userId && 
                        si.Timestamp > lastSync && 
                        si.ClientId != clientId &&
                        si.Status == SyncStatus.Completed)
            .OrderBy(si => si.Timestamp)
            .Take(100) // Limit batch size
            .Select(si => new SyncItemDto
            {
                Id = si.Id,
                EntityType = si.EntityType,
                EntityId = si.EntityId,
                Operation = si.Operation,
                Data = si.Data,
                Timestamp = si.Timestamp,
                Status = si.Status
            })
            .ToListAsync();
    }

    private async Task UpdateClientLastSyncAsync(Guid clientId, DateTime timestamp)
    {
        var client = await _context.SyncClients.FindAsync(clientId);
        if (client != null)
        {
            client.LastSyncTimestamp = timestamp;
            client.LastSeenAt = timestamp;
            await _context.SaveChangesAsync();
        }
    }

    private int GetSkipCount(string? continuationToken)
    {
        if (string.IsNullOrEmpty(continuationToken))
            return 0;

        // Simple implementation - in production you'd want a more robust token system
        return int.TryParse(continuationToken, out var skip) ? skip : 0;
    }

    private string? GenerateContinuationToken(DateTime? lastTimestamp)
    {
        // Simple implementation - in production you'd want a more robust token system
        return lastTimestamp?.Ticks.ToString();
    }

    private async Task<object?> FindExistingEntityAsync(string entityType, Guid entityId)
    {
        try
        {
            return entityType.ToLower() switch
            {
                "project" => await _context.Projects.FirstOrDefaultAsync(p => p.Id == entityId),
                "task" => await _context.Tasks.FirstOrDefaultAsync(t => t.Id == entityId),
                "projectfile" => await _context.ProjectFiles.FirstOrDefaultAsync(f => f.Id == entityId),
                "chatmessage" => await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == entityId),
                "user" => await _context.Users.FirstOrDefaultAsync(u => u.Id == entityId),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding existing entity {EntityId} of type {EntityType}", entityId, entityType);
            return null;
        }
    }

    private async Task<bool> HasConflictAsync(SyncItemDto change, object existingEntity)
    {
        try
        {
            if (existingEntity == null)
                return false;

            // Parse the incoming change data
            var changeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(change.Data);
            if (changeData == null)
                return false;

            // Check if the entity has been modified more recently than the change timestamp
            var lastModified = GetEntityLastModified(existingEntity);
            if (lastModified.HasValue && lastModified.Value > change.Timestamp)
            {
                _logger.LogInformation("Conflict detected for {EntityType} {EntityId}: Server modified at {ServerTime}, client change at {ClientTime}", 
                    change.EntityType, change.EntityId, lastModified.Value, change.Timestamp);
                return true;
            }

            // Additional entity-specific conflict detection
            return change.EntityType.ToLower() switch
            {
                "project" => await HasProjectConflictAsync(change, existingEntity, changeData),
                "task" => await HasTaskConflictAsync(change, existingEntity, changeData),
                "projectfile" => await HasFileConflictAsync(change, existingEntity, changeData),
                "chatmessage" => await HasChatMessageConflictAsync(change, existingEntity, changeData),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking conflict for entity {EntityId}", change.EntityId);
            return false;
        }
    }

    private async Task<SyncConflict> CreateConflictAsync(SyncItemDto change, object existingEntity, Guid clientId)
    {
        var conflict = new SyncConflict
        {
            ClientId = clientId,
            EntityId = change.EntityId,
            EntityType = change.EntityType,
            ClientData = change.Data,
            ServerData = JsonConvert.SerializeObject(existingEntity),
            ClientTimestamp = change.Timestamp,
            ServerTimestamp = DateTime.UtcNow,
            RecommendedStrategy = ConflictResolutionStrategy.ServerWins,
            ConflictReason = "Concurrent modification detected"
        };

        _context.SyncConflicts.Add(conflict);
        await _context.SaveChangesAsync();
        return conflict;
    }

    private ConflictResolutionDto MapToConflictDto(SyncConflict conflict)
    {
        return new ConflictResolutionDto
        {
            EntityId = conflict.EntityId,
            EntityType = conflict.EntityType,
            ClientData = conflict.ClientData,
            ServerData = conflict.ServerData,
            ClientTimestamp = conflict.ClientTimestamp,
            ServerTimestamp = conflict.ServerTimestamp,
            RecommendedStrategy = conflict.RecommendedStrategy,
            ConflictReason = conflict.ConflictReason
        };
    }

    private SyncItem MapToSyncItem(SyncItemDto dto, Guid clientId, Guid userId)
    {
        return new SyncItem
        {
            Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Operation = dto.Operation,
            Data = dto.Data,
            Timestamp = dto.Timestamp,
            Status = SyncStatus.Completed,
            ClientId = clientId,
            UserId = userId
        };
    }

    private async Task<string> ApplyConflictResolutionAsync(SyncConflict conflict, ConflictResolutionStrategy strategy, string? customData)
    {
        return strategy switch
        {
            ConflictResolutionStrategy.ClientWins => conflict.ClientData,
            ConflictResolutionStrategy.ServerWins => conflict.ServerData,
            ConflictResolutionStrategy.LastWriterWins => conflict.ClientTimestamp > conflict.ServerTimestamp 
                ? conflict.ClientData : conflict.ServerData,
            ConflictResolutionStrategy.ManualResolution => customData ?? conflict.ServerData,
            _ => conflict.ServerData
        };
    }

    // Entity-specific change application methods
    private async Task<bool> ApplyEntityChangeByTypeAsync(SyncItemDto syncItem, Guid userId)
    {
        return syncItem.EntityType.ToLower() switch
        {
            "project" => await ApplyProjectChangeAsync(syncItem, userId),
            "task" => await ApplyTaskChangeAsync(syncItem, userId),
            "projectfile" => await ApplyFileChangeAsync(syncItem, userId),
            "chatmessage" => await ApplyChatMessageChangeAsync(syncItem, userId),
            _ => false
        };
    }

    private async Task<bool> ApplyProjectChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var projectData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (projectData == null) return false;

            switch (syncItem.Operation.ToLower())
            {
                case "create":
                    // Project creation would be handled by the Projects service
                    _logger.LogInformation("Project creation sync item {SyncItemId} - delegating to Projects service", syncItem.Id);
                    return true;

                case "update":
                    // Project update would be handled by the Projects service
                    _logger.LogInformation("Project update sync item {SyncItemId} - delegating to Projects service", syncItem.Id);
                    return true;

                case "delete":
                    // Project deletion would be handled by the Projects service
                    _logger.LogInformation("Project deletion sync item {SyncItemId} - delegating to Projects service", syncItem.Id);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying project change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ApplyTaskChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var taskData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (taskData == null) return false;

            switch (syncItem.Operation.ToLower())
            {
                case "create":
                    _logger.LogInformation("Task creation sync item {SyncItemId} - delegating to Tasks service", syncItem.Id);
                    return true;

                case "update":
                    _logger.LogInformation("Task update sync item {SyncItemId} - delegating to Tasks service", syncItem.Id);
                    return true;

                case "delete":
                    _logger.LogInformation("Task deletion sync item {SyncItemId} - delegating to Tasks service", syncItem.Id);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying task change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ApplyFileChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            switch (syncItem.Operation.ToLower())
            {
                case "create":
                case "update":
                case "delete":
                    _logger.LogInformation("File {Operation} sync item {SyncItemId} - delegating to Files service", 
                        syncItem.Operation, syncItem.Id);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying file change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ApplyChatMessageChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            switch (syncItem.Operation.ToLower())
            {
                case "create":
                case "update":
                case "delete":
                    _logger.LogInformation("Chat message {Operation} sync item {SyncItemId} - delegating to Collaboration service", 
                        syncItem.Operation, syncItem.Id);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying chat message change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    // Entity-specific validation methods
    private async Task<bool> ValidateProjectChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var projectData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (projectData == null) return false;

            // Basic validation
            if (syncItem.Operation.ToLower() == "create" && !projectData.ContainsKey("Name"))
                return false;

            // Check if user has access to the project
            var hasAccess = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == syncItem.EntityId && pm.UserId == userId && pm.IsActive);

            return hasAccess || syncItem.Operation.ToLower() == "create";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating project change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ValidateTaskChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var taskData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (taskData == null) return false;

            // Basic validation
            if (syncItem.Operation.ToLower() == "create" && !taskData.ContainsKey("Title"))
                return false;

            // Check if user has access to the task's project
            if (taskData.ContainsKey("ProjectId"))
            {
                var projectId = Guid.Parse(taskData["ProjectId"].ToString() ?? "");
                var hasAccess = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);
                return hasAccess;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating task change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ValidateFileChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var fileData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (fileData == null) return false;

            // Check if user has access to the file's project
            if (fileData.ContainsKey("ProjectId"))
            {
                var projectId = Guid.Parse(fileData["ProjectId"].ToString() ?? "");
                var hasAccess = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);
                return hasAccess;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    private async Task<bool> ValidateChatMessageChangeAsync(SyncItemDto syncItem, Guid userId)
    {
        try
        {
            var messageData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syncItem.Data);
            if (messageData == null) return false;

            // Basic validation
            if (syncItem.Operation.ToLower() == "create" && !messageData.ContainsKey("Content"))
                return false;

            // Check if user has access to the message's project
            if (messageData.ContainsKey("ProjectId"))
            {
                var projectId = Guid.Parse(messageData["ProjectId"].ToString() ?? "");
                var hasAccess = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);
                return hasAccess;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating chat message change for sync item {SyncItemId}", syncItem.Id);
            return false;
        }
    }

    // Entity-specific conflict detection methods
    private async Task<bool> HasProjectConflictAsync(SyncItemDto change, object existingEntity, Dictionary<string, object> changeData)
    {
        // Implementation for project-specific conflict detection
        return await Task.FromResult(false);
    }

    private async Task<bool> HasTaskConflictAsync(SyncItemDto change, object existingEntity, Dictionary<string, object> changeData)
    {
        // Implementation for task-specific conflict detection
        return await Task.FromResult(false);
    }

    private async Task<bool> HasFileConflictAsync(SyncItemDto change, object existingEntity, Dictionary<string, object> changeData)
    {
        // Implementation for file-specific conflict detection
        return await Task.FromResult(false);
    }

    private async Task<bool> HasChatMessageConflictAsync(SyncItemDto change, object existingEntity, Dictionary<string, object> changeData)
    {
        // Chat messages generally don't have conflicts (append-only)
        return await Task.FromResult(false);
    }

    // Utility methods
    private DateTime? GetEntityLastModified(object entity)
    {
        try
        {
            // Use reflection to get UpdatedAt or similar timestamp field
            var entityType = entity.GetType();
            
            var updatedAtProperty = entityType.GetProperty("UpdatedAt") ?? 
                                  entityType.GetProperty("ModifiedAt") ?? 
                                  entityType.GetProperty("LastModified");
            
            if (updatedAtProperty != null && updatedAtProperty.PropertyType == typeof(DateTime?))
            {
                return (DateTime?)updatedAtProperty.GetValue(entity);
            }

            if (updatedAtProperty != null && updatedAtProperty.PropertyType == typeof(DateTime))
            {
                return (DateTime)updatedAtProperty.GetValue(entity);
            }

            // Fallback to CreatedAt if no UpdatedAt field
            var createdAtProperty = entityType.GetProperty("CreatedAt") ?? 
                                  entityType.GetProperty("Created");
            
            if (createdAtProperty != null && createdAtProperty.PropertyType == typeof(DateTime))
            {
                return (DateTime)createdAtProperty.GetValue(entity);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity last modified timestamp");
            return null;
        }
    }

    private async Task LogSyncOperationAsync(Guid entityId, string entityType, string operation, bool success, string? message, Guid userId)
    {
        try
        {
            var syncLog = new SyncLog
            {
                EntityId = entityId,
                EntityType = entityType,
                Operation = operation,
                Success = success,
                Message = message ?? (success ? "Operation completed successfully" : "Operation failed"),
                Timestamp = DateTime.UtcNow,
                ClientId = Guid.Empty // Set by specific client operations
            };

            _context.SyncLogs.Add(syncLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging sync operation for {EntityType} {EntityId}", entityType, entityId);
        }
    }
}