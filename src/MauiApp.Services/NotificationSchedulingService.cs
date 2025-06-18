using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class NotificationSchedulingService : INotificationSchedulingService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly INotificationDeliveryService _deliveryService;
    private readonly ILogger<NotificationSchedulingService> _logger;

    public NotificationSchedulingService(
        IApiService apiService,
        ICacheService cacheService,
        INotificationDeliveryService deliveryService,
        ILogger<NotificationSchedulingService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    public async Task<ScheduledNotificationDto> ScheduleNotificationAsync(ScheduleNotificationRequestDto request)
    {
        try
        {
            _logger.LogInformation("Scheduling notification for {ScheduledAt}", request.ScheduledAt);

            // Validate scheduling time
            if (request.ScheduledAt <= DateTime.UtcNow)
            {
                throw new ArgumentException("Scheduled time must be in the future");
            }

            var scheduledNotification = await _apiService.PostAsync<ScheduledNotificationDto>("/api/notifications/schedule", request);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("scheduled-notifications-*");
            
            _logger.LogInformation("Scheduled notification: {ScheduledNotificationId}", scheduledNotification?.Id);
            return scheduledNotification ?? new ScheduledNotificationDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule notification");
            throw;
        }
    }

    public async Task<List<ScheduledNotificationDto>> ScheduleBulkNotificationAsync(List<ScheduleNotificationRequestDto> requests)
    {
        try
        {
            _logger.LogInformation("Scheduling {Count} bulk notifications", requests.Count);

            // Validate all requests
            var invalidRequests = requests.Where(r => r.ScheduledAt <= DateTime.UtcNow).ToList();
            if (invalidRequests.Any())
            {
                throw new ArgumentException($"{invalidRequests.Count} notifications have invalid scheduling times");
            }

            var scheduledNotifications = await _apiService.PostAsync<List<ScheduledNotificationDto>>("/api/notifications/schedule/bulk", requests);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("scheduled-notifications-*");
            
            _logger.LogInformation("Scheduled {Count} bulk notifications", scheduledNotifications?.Count ?? 0);
            return scheduledNotifications ?? new List<ScheduledNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule bulk notifications");
            throw;
        }
    }

    public async Task<bool> CancelScheduledNotificationAsync(Guid scheduledNotificationId)
    {
        try
        {
            _logger.LogInformation("Canceling scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            
            await _apiService.DeleteAsync($"/api/notifications/schedule/{scheduledNotificationId}");
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("scheduled-notifications-*");
            
            _logger.LogInformation("Canceled scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            return false;
        }
    }

    public async Task<bool> UpdateScheduledNotificationAsync(Guid scheduledNotificationId, ScheduledNotificationDto notification)
    {
        try
        {
            _logger.LogInformation("Updating scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            
            // Validate scheduling time
            if (notification.ScheduledAt <= DateTime.UtcNow)
            {
                throw new ArgumentException("Scheduled time must be in the future");
            }

            await _apiService.PutAsync($"/api/notifications/schedule/{scheduledNotificationId}", notification);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("scheduled-notifications-*");
            
            _logger.LogInformation("Updated scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled notification: {ScheduledNotificationId}", scheduledNotificationId);
            return false;
        }
    }

    public async Task<RecurringNotificationDto> CreateRecurringNotificationAsync(CreateRecurringNotificationRequestDto request)
    {
        try
        {
            _logger.LogInformation("Creating recurring notification: {Name}", request.Name);

            // Validate cron expression
            if (!IsValidCronExpression(request.CronExpression))
            {
                throw new ArgumentException("Invalid cron expression");
            }

            var recurringNotification = await _apiService.PostAsync<RecurringNotificationDto>("/api/notifications/recurring", request);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("recurring-notifications-*");
            
            _logger.LogInformation("Created recurring notification: {RecurringNotificationId}", recurringNotification?.Id);
            return recurringNotification ?? new RecurringNotificationDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring notification: {Name}", request.Name);
            throw;
        }
    }

    public async Task<List<RecurringNotificationDto>> GetRecurringNotificationsAsync(bool? isActive = null)
    {
        try
        {
            var cacheKey = $"recurring-notifications-{isActive}";
            var cached = await _cacheService.GetAsync<List<RecurringNotificationDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>();
            if (isActive.HasValue) queryParams["isActive"] = isActive.Value;

            var notifications = await _apiService.GetAsync<List<RecurringNotificationDto>>("/api/notifications/recurring", queryParams);
            
            if (notifications != null)
            {
                await _cacheService.SetAsync(cacheKey, notifications, TimeSpan.FromMinutes(10));
            }

            return notifications ?? new List<RecurringNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recurring notifications");
            return new List<RecurringNotificationDto>();
        }
    }

    public async Task<bool> UpdateRecurringNotificationAsync(Guid recurringNotificationId, RecurringNotificationDto notification)
    {
        try
        {
            _logger.LogInformation("Updating recurring notification: {RecurringNotificationId}", recurringNotificationId);
            
            // Validate cron expression
            if (!IsValidCronExpression(notification.CronExpression))
            {
                throw new ArgumentException("Invalid cron expression");
            }

            await _apiService.PutAsync($"/api/notifications/recurring/{recurringNotificationId}", notification);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("recurring-notifications-*");
            
            _logger.LogInformation("Updated recurring notification: {RecurringNotificationId}", recurringNotificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update recurring notification: {RecurringNotificationId}", recurringNotificationId);
            return false;
        }
    }

    public async Task<bool> DeleteRecurringNotificationAsync(Guid recurringNotificationId)
    {
        try
        {
            _logger.LogInformation("Deleting recurring notification: {RecurringNotificationId}", recurringNotificationId);
            
            await _apiService.DeleteAsync($"/api/notifications/recurring/{recurringNotificationId}");
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("recurring-notifications-*");
            
            _logger.LogInformation("Deleted recurring notification: {RecurringNotificationId}", recurringNotificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete recurring notification: {RecurringNotificationId}", recurringNotificationId);
            return false;
        }
    }

    public async Task<List<ScheduledNotificationDto>> GetScheduledNotificationsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var cacheKey = $"scheduled-notifications-{startDate}-{endDate}";
            var cached = await _cacheService.GetAsync<List<ScheduledNotificationDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>();
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var notifications = await _apiService.GetAsync<List<ScheduledNotificationDto>>("/api/notifications/scheduled", queryParams);
            
            if (notifications != null)
            {
                await _cacheService.SetAsync(cacheKey, notifications, TimeSpan.FromMinutes(5));
            }

            return notifications ?? new List<ScheduledNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scheduled notifications");
            return new List<ScheduledNotificationDto>();
        }
    }

    public async Task<List<ScheduledNotificationDto>> GetPendingNotificationsAsync()
    {
        try
        {
            var cacheKey = "pending-notifications";
            var cached = await _cacheService.GetAsync<List<ScheduledNotificationDto>>(cacheKey);
            if (cached != null) return cached;

            var notifications = await _apiService.GetAsync<List<ScheduledNotificationDto>>("/api/notifications/scheduled/pending");
            
            if (notifications != null)
            {
                await _cacheService.SetAsync(cacheKey, notifications, TimeSpan.FromMinutes(1));
            }

            return notifications ?? new List<ScheduledNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending notifications");
            return new List<ScheduledNotificationDto>();
        }
    }

    public async Task ProcessPendingNotificationsAsync()
    {
        try
        {
            _logger.LogDebug("Processing pending notifications");
            
            var pendingNotifications = await GetPendingNotificationsAsync();
            var now = DateTime.UtcNow;

            var tasks = pendingNotifications
                .Where(n => n.ScheduledAt <= now && n.Status == NotificationScheduleStatus.Pending)
                .Select(async notification =>
                {
                    try
                    {
                        await _deliveryService.SendScheduledNotificationAsync(notification.Id);
                        _logger.LogDebug("Processed scheduled notification: {NotificationId}", notification.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process scheduled notification: {NotificationId}", notification.Id);
                    }
                });

            await Task.WhenAll(tasks);
            
            // Clear pending notifications cache
            await _cacheService.RemoveAsync("pending-notifications");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending notifications");
        }
    }

    public async Task<SchedulingAnalyticsDto> GetSchedulingAnalyticsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var cacheKey = $"scheduling-analytics-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
            var cached = await _cacheService.GetAsync<SchedulingAnalyticsDto>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var analytics = await _apiService.GetAsync<SchedulingAnalyticsDto>("/api/notifications/analytics/scheduling", queryParams);
            
            if (analytics != null)
            {
                await _cacheService.SetAsync(cacheKey, analytics, TimeSpan.FromMinutes(15));
            }

            return analytics ?? new SchedulingAnalyticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scheduling analytics");
            return new SchedulingAnalyticsDto();
        }
    }

    // Helper Methods
    private static bool IsValidCronExpression(string cronExpression)
    {
        try
        {
            // Basic validation - in a real application, you'd use a proper cron parser
            if (string.IsNullOrWhiteSpace(cronExpression)) return false;
            
            var parts = cronExpression.Split(' ');
            return parts.Length >= 5 && parts.Length <= 6; // Basic cron has 5-6 parts
        }
        catch
        {
            return false;
        }
    }
}