using MauiApp.NotificationService.Data;
using MauiApp.Core.Entities;
using MauiApp.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MauiApp.NotificationService.Services;

public class NotificationProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationProcessingService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);

    public NotificationProcessingService(IServiceProvider serviceProvider, ILogger<NotificationProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Processing Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationQueue(stoppingToken);
                await ProcessScheduledNotifications(stoppingToken);
                await RetryFailedNotifications(stoppingToken);
                await CleanupExpiredNotifications(stoppingToken);
                await UpdateDeliveryStats(stoppingToken);
                
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification processing service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Notification Processing Service stopped");
    }

    private async Task ProcessNotificationQueue(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<Core.Interfaces.INotificationService>();

        try
        {
            var queueItems = await context.NotificationQueue
                .Where(q => !q.IsProcessed && (q.NextRetry == null || q.NextRetry <= DateTime.UtcNow))
                .OrderBy(q => q.Priority)
                .ThenBy(q => q.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (queueItems.Any())
            {
                _logger.LogInformation("Processing {Count} queued notifications", queueItems.Count);

                foreach (var item in queueItems)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ProcessQueueItem(item, context, notificationService, cancellationToken);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification queue");
        }
    }

    private async Task ProcessQueueItem(NotificationQueue item, NotificationDbContext context, Core.Interfaces.INotificationService notificationService, CancellationToken cancellationToken)
    {
        try
        {
            var success = await notificationService.SendNotificationAsync(item.NotificationId);

            if (success)
            {
                item.IsProcessed = true;
                item.ProcessedAt = DateTime.UtcNow;
                _logger.LogDebug("Successfully processed notification queue item {Id}", item.Id);
            }
            else
            {
                item.RetryCount++;
                item.ErrorMessage = "Failed to send notification";
                
                if (item.RetryCount >= 3)
                {
                    item.IsProcessed = true;
                    item.ProcessedAt = DateTime.UtcNow;
                    _logger.LogWarning("Marking notification queue item {Id} as processed after {RetryCount} failed attempts", item.Id, item.RetryCount);
                }
                else
                {
                    // Schedule retry with exponential backoff
                    var delayMinutes = Math.Pow(2, item.RetryCount) * 5; // 10, 20, 40 minutes
                    item.NextRetry = DateTime.UtcNow.AddMinutes(delayMinutes);
                    _logger.LogWarning("Scheduling retry for notification queue item {Id} in {DelayMinutes} minutes", item.Id, delayMinutes);
                }
            }
        }
        catch (Exception ex)
        {
            item.RetryCount++;
            item.ErrorMessage = ex.Message;
            
            if (item.RetryCount >= 3)
            {
                item.IsProcessed = true;
                item.ProcessedAt = DateTime.UtcNow;
            }
            else
            {
                var delayMinutes = Math.Pow(2, item.RetryCount) * 5;
                item.NextRetry = DateTime.UtcNow.AddMinutes(delayMinutes);
            }
            
            _logger.LogError(ex, "Error processing notification queue item {Id}", item.Id);
        }
    }

    private async Task ProcessScheduledNotifications(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        try
        {
            var scheduledNotifications = await context.Notifications
                .Where(n => n.IsScheduled && 
                           n.ScheduledAt <= DateTime.UtcNow && 
                           n.Status == NotificationStatus.Created)
                .Take(100)
                .ToListAsync(cancellationToken);

            if (scheduledNotifications.Any())
            {
                _logger.LogInformation("Processing {Count} scheduled notifications", scheduledNotifications.Count);

                foreach (var notification in scheduledNotifications)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Move to queue for processing
                    notification.IsScheduled = false;
                    notification.Status = NotificationStatus.Queued;

                    // Add to notification queue
                    var queueItem = new NotificationQueue
                    {
                        NotificationId = notification.Id,
                        Channel = NotificationChannel.Push, // Default channel
                        Priority = notification.Priority
                    };

                    context.NotificationQueue.Add(queueItem);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled notifications");
        }
    }

    private async Task RetryFailedNotifications(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        try
        {
            var retryTime = DateTime.UtcNow.AddMinutes(-30); // Retry notifications that failed more than 30 minutes ago
            
            var failedNotifications = await context.Notifications
                .Where(n => n.Status == NotificationStatus.Failed && 
                           n.SentAt < retryTime)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (failedNotifications.Any())
            {
                _logger.LogInformation("Retrying {Count} failed notifications", failedNotifications.Count);

                foreach (var notification in failedNotifications)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Reset status to queued for retry
                    notification.Status = NotificationStatus.Queued;

                    // Add back to queue
                    var queueItem = new NotificationQueue
                    {
                        NotificationId = notification.Id,
                        Channel = NotificationChannel.Push,
                        Priority = notification.Priority,
                        RetryCount = 1
                    };

                    context.NotificationQueue.Add(queueItem);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed notifications");
        }
    }

    private async Task CleanupExpiredNotifications(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        try
        {
            var now = DateTime.UtcNow;
            
            // Mark expired notifications
            var expiredNotifications = await context.Notifications
                .Where(n => n.ExpiresAt.HasValue && 
                           n.ExpiresAt < now && 
                           n.Status != NotificationStatus.Expired)
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (expiredNotifications.Any())
            {
                foreach (var notification in expiredNotifications)
                {
                    notification.Status = NotificationStatus.Expired;
                }

                _logger.LogInformation("Marked {Count} notifications as expired", expiredNotifications.Count);
            }

            // Clean up old notifications (older than 90 days)
            var cutoffDate = DateTime.UtcNow.AddDays(-90);
            var oldNotifications = await context.Notifications
                .Where(n => n.CreatedAt < cutoffDate && 
                           (n.Status == NotificationStatus.Read || n.Status == NotificationStatus.Expired))
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (oldNotifications.Any())
            {
                context.Notifications.RemoveRange(oldNotifications);
                _logger.LogInformation("Cleaned up {Count} old notifications", oldNotifications.Count);
            }

            // Clean up old queue items
            var oldQueueItems = await context.NotificationQueue
                .Where(q => q.IsProcessed && q.ProcessedAt < cutoffDate)
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (oldQueueItems.Any())
            {
                context.NotificationQueue.RemoveRange(oldQueueItems);
                _logger.LogInformation("Cleaned up {Count} old queue items", oldQueueItems.Count);
            }

            if (expiredNotifications.Any() || oldNotifications.Any() || oldQueueItems.Any())
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during notification cleanup");
        }
    }

    private async Task UpdateDeliveryStats(CancellationToken cancellationToken)
    {
        // Run stats update once per hour
        if (DateTime.UtcNow.Minute != 0)
            return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        try
        {
            var today = DateTime.UtcNow.Date;
            
            // Check if stats already exist for today
            var existingStats = await context.NotificationStats
                .AnyAsync(s => s.Date == today, cancellationToken);

            if (existingStats)
                return;

            // Calculate stats for today
            var todayNotifications = await context.Notifications
                .Where(n => n.CreatedAt.Date == today)
                .ToListAsync(cancellationToken);

            if (todayNotifications.Any())
            {
                var stats = new NotificationStats
                {
                    Date = today,
                    TotalSent = todayNotifications.Count(n => n.Status != NotificationStatus.Created),
                    TotalDelivered = todayNotifications.Count(n => n.Status == NotificationStatus.Delivered || n.Status == NotificationStatus.Read),
                    TotalRead = todayNotifications.Count(n => n.Status == NotificationStatus.Read),
                    TotalFailed = todayNotifications.Count(n => n.Status == NotificationStatus.Failed)
                };

                stats.DeliveryRate = stats.TotalSent > 0 ? (double)stats.TotalDelivered / stats.TotalSent : 0;
                stats.ReadRate = stats.TotalDelivered > 0 ? (double)stats.TotalRead / stats.TotalDelivered : 0;

                context.NotificationStats.Add(stats);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated notification stats for {Date}: Sent={Sent}, Delivered={Delivered}, Read={Read}, Failed={Failed}",
                    today, stats.TotalSent, stats.TotalDelivered, stats.TotalRead, stats.TotalFailed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating delivery stats");
        }
    }
}