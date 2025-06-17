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

    // Placeholder implementations for remaining interface methods
    public Task<List<SyncItemDto>> GetEntityChangesAsync(string entityType, DateTime since, Guid userId, int maxItems = 100) => throw new NotImplementedException();
    public Task<bool> ApplyEntityChangeAsync(SyncItemDto syncItem, Guid userId) => throw new NotImplementedException();
    public Task<bool> ValidateEntityChangeAsync(SyncItemDto syncItem, Guid userId) => throw new NotImplementedException();
    public Task<SyncResponseDto> ProcessBatchSyncAsync(List<SyncRequestDto> requests, Guid userId) => throw new NotImplementedException();
    public Task<bool> QueueBatchSyncItemsAsync(List<SyncItemDto> items, Guid userId) => throw new NotImplementedException();
    public Task NotifyClientsOfChangesAsync(string entityType, Guid entityId, string operation, Guid userId) => Task.CompletedTask;
    public Task<bool> SubscribeToEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId) => throw new NotImplementedException();
    public Task<bool> UnsubscribeFromEntityChangesAsync(Guid clientId, List<string> entityTypes, Guid userId) => throw new NotImplementedException();

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
        // This would query the appropriate entity based on entityType
        // Simplified implementation
        return await Task.FromResult<object?>(null);
    }

    private async Task<bool> HasConflictAsync(SyncItemDto change, object existingEntity)
    {
        // Implement conflict detection logic
        return await Task.FromResult(false);
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
}