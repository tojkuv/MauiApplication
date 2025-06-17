using MauiApp.SyncService.Data;
using MauiApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MauiApp.SyncService.Services;

public class SyncProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncProcessingService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(1);

    public SyncProcessingService(IServiceProvider serviceProvider, ILogger<SyncProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync Processing Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingSyncItems(stoppingToken);
                await ProcessFailedItems(stoppingToken);
                await CleanupOldData(stoppingToken);
                
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync processing service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Sync Processing Service stopped");
    }

    private async Task ProcessPendingSyncItems(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        try
        {
            var pendingItems = await context.SyncItems
                .Where(si => si.Status == Core.DTOs.SyncStatus.Pending)
                .OrderBy(si => si.Timestamp)
                .Take(100)
                .ToListAsync(cancellationToken);

            if (pendingItems.Any())
            {
                _logger.LogInformation("Processing {Count} pending sync items", pendingItems.Count);

                foreach (var item in pendingItems)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ProcessSyncItem(item, context, cancellationToken);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending sync items");
        }
    }

    private async Task ProcessSyncItem(SyncItem item, SyncDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Mark as in progress
            item.Status = Core.DTOs.SyncStatus.InProgress;
            item.UpdatedAt = DateTime.UtcNow;

            // Process the sync item based on entity type and operation
            var success = await ApplySyncItemChange(item, context, cancellationToken);

            if (success)
            {
                item.Status = Core.DTOs.SyncStatus.Completed;
                _logger.LogDebug("Successfully processed sync item {Id} for entity {EntityId}", item.Id, item.EntityId);
            }
            else
            {
                item.Status = Core.DTOs.SyncStatus.Failed;
                item.RetryCount++;
                item.ErrorMessage = "Failed to apply sync item change";
                item.LastRetry = DateTime.UtcNow;
                _logger.LogWarning("Failed to process sync item {Id} for entity {EntityId}", item.Id, item.EntityId);
            }
        }
        catch (Exception ex)
        {
            item.Status = Core.DTOs.SyncStatus.Failed;
            item.RetryCount++;
            item.ErrorMessage = ex.Message;
            item.LastRetry = DateTime.UtcNow;
            _logger.LogError(ex, "Error processing sync item {Id}", item.Id);
        }
    }

    private async Task<bool> ApplySyncItemChange(SyncItem item, SyncDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            // This is where you would apply the actual changes to the database
            // Based on the entity type and operation (Create, Update, Delete)
            // For now, we'll just mark it as successful since the actual implementation
            // would depend on the specific entity types and business logic
            
            await LogSyncOperation(item, context, "Applied sync item change", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await LogSyncOperation(item, context, $"Failed to apply sync item change: {ex.Message}", cancellationToken);
            return false;
        }
    }

    private async Task ProcessFailedItems(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Retry failed items after 5 minutes
            
            var failedItems = await context.SyncItems
                .Where(si => si.Status == Core.DTOs.SyncStatus.Failed &&
                           si.RetryCount < 3 &&
                           (si.LastRetry == null || si.LastRetry < cutoffTime))
                .OrderBy(si => si.LastRetry ?? si.Timestamp)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (failedItems.Any())
            {
                _logger.LogInformation("Retrying {Count} failed sync items", failedItems.Count);

                foreach (var item in failedItems)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Reset to pending for retry
                    item.Status = Core.DTOs.SyncStatus.Pending;
                    item.ErrorMessage = null;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing failed sync items");
        }
    }

    private async Task CleanupOldData(CancellationToken cancellationToken)
    {
        // Run cleanup once per hour
        if (DateTime.UtcNow.Minute != 0)
            return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep data for 30 days

            // Clean up old completed sync items
            var oldSyncItems = await context.SyncItems
                .Where(si => si.Status == Core.DTOs.SyncStatus.Completed && si.CreatedAt < cutoffDate)
                .Take(1000) // Process in batches to avoid large transactions
                .ToListAsync(cancellationToken);

            if (oldSyncItems.Any())
            {
                context.SyncItems.RemoveRange(oldSyncItems);
                _logger.LogInformation("Cleaned up {Count} old sync items", oldSyncItems.Count);
            }

            // Clean up old sync logs
            var oldLogs = await context.SyncLogs
                .Where(sl => sl.Timestamp < cutoffDate)
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (oldLogs.Any())
            {
                context.SyncLogs.RemoveRange(oldLogs);
                _logger.LogInformation("Cleaned up {Count} old sync logs", oldLogs.Count);
            }

            // Clean up resolved conflicts older than 7 days
            var oldConflicts = await context.SyncConflicts
                .Where(sc => sc.IsResolved && sc.ResolvedAt < DateTime.UtcNow.AddDays(-7))
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (oldConflicts.Any())
            {
                context.SyncConflicts.RemoveRange(oldConflicts);
                _logger.LogInformation("Cleaned up {Count} old resolved conflicts", oldConflicts.Count);
            }

            if (oldSyncItems.Any() || oldLogs.Any() || oldConflicts.Any())
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    private async Task LogSyncOperation(SyncItem item, SyncDbContext context, string message, CancellationToken cancellationToken)
    {
        try
        {
            var log = new SyncLog
            {
                ClientId = item.ClientId,
                SyncItemId = item.Id,
                Operation = item.Operation,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                LogLevel = "Info",
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            context.SyncLogs.Add(log);
            // Note: SaveChanges will be called by the caller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging sync operation");
        }
    }
}