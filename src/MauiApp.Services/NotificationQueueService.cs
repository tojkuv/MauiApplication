using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MauiApp.Services;

public class NotificationQueueService : INotificationQueueService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<NotificationQueueService> _logger;
    private readonly ConcurrentDictionary<string, NotificationQueueItem> _localQueue;
    private readonly SemaphoreSlim _processingSemaphore;
    private bool _isProcessingPaused;

    public NotificationQueueService(
        IApiService apiService,
        ICacheService cacheService,
        ILogger<NotificationQueueService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
        _localQueue = new ConcurrentDictionary<string, NotificationQueueItem>();
        _processingSemaphore = new SemaphoreSlim(1, 1);
        _isProcessingPaused = false;
    }

    public async Task<string> EnqueueNotificationAsync(NotificationQueueItem queueItem, NotificationPriority priority = NotificationPriority.Normal)
    {
        try
        {
            queueItem.Priority = priority;
            queueItem.CreatedAt = DateTime.UtcNow;
            
            _logger.LogDebug("Enqueuing notification: {NotificationId} with priority {Priority}", 
                queueItem.Notification.Id, priority);

            // Add to local queue for immediate processing
            _localQueue.TryAdd(queueItem.Id, queueItem);

            // Send to server queue
            var response = await _apiService.PostAsync<string>("/api/notifications/queue", queueItem);
            var queueItemId = response ?? queueItem.Id;

            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");

            _logger.LogDebug("Enqueued notification: {QueueItemId}", queueItemId);
            return queueItemId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue notification: {NotificationId}", queueItem.Notification.Id);
            throw;
        }
    }

    public async Task<string> EnqueueBulkNotificationAsync(List<NotificationQueueItem> queueItems, NotificationPriority priority = NotificationPriority.Normal)
    {
        try
        {
            _logger.LogDebug("Enqueuing {Count} bulk notifications with priority {Priority}", 
                queueItems.Count, priority);

            foreach (var item in queueItems)
            {
                item.Priority = priority;
                item.CreatedAt = DateTime.UtcNow;
                _localQueue.TryAdd(item.Id, item);
            }

            var bulkRequest = new { QueueItems = queueItems, Priority = priority };
            var response = await _apiService.PostAsync<string>("/api/notifications/queue/bulk", bulkRequest);

            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");

            _logger.LogDebug("Enqueued {Count} bulk notifications", queueItems.Count);
            return response ?? Guid.NewGuid().ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue bulk notifications");
            throw;
        }
    }

    public async Task<NotificationQueueItem?> DequeueNotificationAsync()
    {
        try
        {
            await _processingSemaphore.WaitAsync();

            if (_isProcessingPaused)
            {
                return null;
            }

            // Try local queue first (for immediate processing)
            var localItem = GetHighestPriorityLocalItem();
            if (localItem != null)
            {
                _localQueue.TryRemove(localItem.Id, out _);
                return localItem;
            }

            // Dequeue from server
            var serverItem = await _apiService.GetAsync<NotificationQueueItem>("/api/notifications/queue/dequeue");
            
            if (serverItem != null)
            {
                _logger.LogDebug("Dequeued notification: {QueueItemId}", serverItem.Id);
            }

            return serverItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue notification");
            return null;
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    public async Task<bool> RemoveFromQueueAsync(string queueItemId)
    {
        try
        {
            _logger.LogDebug("Removing queue item: {QueueItemId}", queueItemId);

            // Remove from local queue
            _localQueue.TryRemove(queueItemId, out _);

            // Remove from server queue
            await _apiService.DeleteAsync($"/api/notifications/queue/{queueItemId}");

            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");

            _logger.LogDebug("Removed queue item: {QueueItemId}", queueItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove queue item: {QueueItemId}", queueItemId);
            return false;
        }
    }

    public async Task<NotificationQueueStatsDto> GetQueueStatsAsync()
    {
        try
        {
            var cacheKey = "notification-queue-stats";
            var cached = await _cacheService.GetAsync<NotificationQueueStatsDto>(cacheKey);
            if (cached != null) return cached;

            var stats = await _apiService.GetAsync<NotificationQueueStatsDto>("/api/notifications/queue/stats");
            
            if (stats != null)
            {
                // Add local queue stats
                stats.PendingItems += _localQueue.Count;
                stats.ItemsByPriority = stats.ItemsByPriority ?? new Dictionary<NotificationPriority, long>();
                
                foreach (var localItem in _localQueue.Values)
                {
                    if (stats.ItemsByPriority.TryGetValue(localItem.Priority, out var count))
                    {
                        stats.ItemsByPriority[localItem.Priority] = count + 1;
                    }
                    else
                    {
                        stats.ItemsByPriority[localItem.Priority] = 1;
                    }
                }

                stats.IsProcessingPaused = _isProcessingPaused;
                
                await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(1));
            }

            return stats ?? new NotificationQueueStatsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue stats");
            return new NotificationQueueStatsDto();
        }
    }

    public async Task<List<NotificationQueueItem>> GetQueueItemsAsync(NotificationPriority? priority = null, int maxCount = 100)
    {
        try
        {
            var queryParams = new Dictionary<string, object> { ["maxCount"] = maxCount };
            if (priority.HasValue) queryParams["priority"] = priority.Value.ToString();

            var serverItems = await _apiService.GetAsync<List<NotificationQueueItem>>("/api/notifications/queue/items", queryParams);
            var items = serverItems ?? new List<NotificationQueueItem>();

            // Add local queue items
            var localItems = _localQueue.Values
                .Where(item => !priority.HasValue || item.Priority == priority.Value)
                .Take(maxCount - items.Count)
                .ToList();

            items.AddRange(localItems);

            return items.Take(maxCount).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue items");
            return new List<NotificationQueueItem>();
        }
    }

    public async Task<bool> ClearQueueAsync(NotificationPriority? priority = null)
    {
        try
        {
            _logger.LogWarning("Clearing notification queue with priority filter: {Priority}", priority);

            // Clear local queue
            if (priority.HasValue)
            {
                var itemsToRemove = _localQueue.Where(kvp => kvp.Value.Priority == priority.Value).ToList();
                foreach (var item in itemsToRemove)
                {
                    _localQueue.TryRemove(item.Key, out _);
                }
            }
            else
            {
                _localQueue.Clear();
            }

            // Clear server queue
            var queryParams = priority.HasValue ? new Dictionary<string, object> { ["priority"] = priority.Value.ToString() } : null;
            await _apiService.DeleteAsync("/api/notifications/queue/clear", queryParams);

            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");

            _logger.LogWarning("Cleared notification queue");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear queue");
            return false;
        }
    }

    public async Task<bool> PauseQueueProcessingAsync()
    {
        try
        {
            _logger.LogInformation("Pausing queue processing");
            _isProcessingPaused = true;
            
            await _apiService.PostAsync("/api/notifications/queue/pause", new { });
            
            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");
            
            _logger.LogInformation("Queue processing paused");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause queue processing");
            return false;
        }
    }

    public async Task<bool> ResumeQueueProcessingAsync()
    {
        try
        {
            _logger.LogInformation("Resuming queue processing");
            _isProcessingPaused = false;
            
            await _apiService.PostAsync("/api/notifications/queue/resume", new { });
            
            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");
            
            _logger.LogInformation("Queue processing resumed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume queue processing");
            return false;
        }
    }

    public async Task<bool> IsQueueProcessingPausedAsync()
    {
        try
        {
            var serverStatus = await _apiService.GetAsync<bool>("/api/notifications/queue/status/paused");
            return _isProcessingPaused || serverStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if queue processing is paused");
            return _isProcessingPaused;
        }
    }

    public async Task<List<NotificationQueueItem>> GetDeadLetterQueueAsync()
    {
        try
        {
            var deadLetterItems = await _apiService.GetAsync<List<NotificationQueueItem>>("/api/notifications/queue/deadletter");
            return deadLetterItems ?? new List<NotificationQueueItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dead letter queue");
            return new List<NotificationQueueItem>();
        }
    }

    public async Task<bool> RequeueFromDeadLetterAsync(string queueItemId)
    {
        try
        {
            _logger.LogInformation("Requeuing item from dead letter queue: {QueueItemId}", queueItemId);
            
            await _apiService.PostAsync($"/api/notifications/queue/deadletter/{queueItemId}/requeue", new { });
            
            // Clear stats cache
            await _cacheService.RemoveAsync("notification-queue-stats");
            
            _logger.LogInformation("Requeued item from dead letter queue: {QueueItemId}", queueItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to requeue item from dead letter queue: {QueueItemId}", queueItemId);
            return false;
        }
    }

    public async Task<bool> ClearDeadLetterQueueAsync()
    {
        try
        {
            _logger.LogWarning("Clearing dead letter queue");
            
            await _apiService.DeleteAsync("/api/notifications/queue/deadletter/clear");
            
            _logger.LogWarning("Cleared dead letter queue");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear dead letter queue");
            return false;
        }
    }

    // Helper Methods
    private NotificationQueueItem? GetHighestPriorityLocalItem()
    {
        if (_localQueue.IsEmpty) return null;

        return _localQueue.Values
            .Where(item => item.ScheduledAt == null || item.ScheduledAt <= DateTime.UtcNow)
            .OrderByDescending(item => (int)item.Priority)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefault();
    }
}