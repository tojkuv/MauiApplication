using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MauiApp.Services;

public class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly IApiService _apiService;
    private readonly INotificationTemplateService _templateService;
    private readonly INotificationPreferencesService _preferencesService;
    private readonly INotificationQueueService _queueService;
    private readonly ILogger<NotificationDeliveryService> _logger;
    
    private readonly Dictionary<NotificationChannel, INotificationProvider> _providers;

    public NotificationDeliveryService(
        IApiService apiService,
        INotificationTemplateService templateService,
        INotificationPreferencesService preferencesService,
        INotificationQueueService queueService,
        ILogger<NotificationDeliveryService> logger,
        IEnumerable<INotificationProvider> providers)
    {
        _apiService = apiService;
        _templateService = templateService;
        _preferencesService = preferencesService;
        _queueService = queueService;
        _logger = logger;
        _providers = providers.ToDictionary(p => p.Channel, p => p);
    }

    public async Task<NotificationDeliveryResult> SendNotificationAsync(NotificationDto notification, List<string> recipients, NotificationChannel channel)
    {
        try
        {
            _logger.LogInformation("Sending notification {NotificationId} via {Channel} to {RecipientCount} recipients", 
                notification.Id, channel, recipients.Count);

            // Check if provider exists for channel
            if (!_providers.TryGetValue(channel, out var provider))
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"No provider configured for channel {channel}",
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = channel
                };
            }

            // Filter recipients based on preferences
            var filteredRecipients = await FilterRecipientsByPreferencesAsync(recipients, notification.Type, channel);
            
            if (!filteredRecipients.Any())
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = true,
                    Status = NotificationDeliveryStatus.Delivered,
                    Channel = channel,
                    Metadata = new Dictionary<string, object> { ["filtered_out"] = "all_recipients" }
                };
            }

            // Send via provider
            var result = await provider.SendAsync(notification, filteredRecipients);
            
            // Track delivery
            await TrackDeliveryEventAsync(result.DeliveryId, NotificationDeliveryEvent.Sent);
            
            _logger.LogInformation("Notification {NotificationId} sent via {Channel}: {Success}", 
                notification.Id, channel, result.IsSuccess);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification {NotificationId} via {Channel}", notification.Id, channel);
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = channel
            };
        }
    }

    public async Task<List<NotificationDeliveryResult>> SendBulkNotificationAsync(BulkNotificationRequestDto request)
    {
        try
        {
            _logger.LogInformation("Sending bulk notification to {UserCount} users via {Channels}", 
                request.UserIds?.Count ?? 0, string.Join(",", GetRequestedChannels(request)));

            var results = new List<NotificationDeliveryResult>();
            var notification = CreateNotificationFromRequest(request);

            // Determine target recipients
            var recipients = await GetRecipientsFromRequestAsync(request);

            // Send via each enabled channel
            if (request.SendPush && _providers.ContainsKey(NotificationChannel.Push))
            {
                var pushResult = await SendNotificationAsync(notification, recipients, NotificationChannel.Push);
                results.Add(pushResult);
            }

            if (request.SendEmail && _providers.ContainsKey(NotificationChannel.Email))
            {
                var emailResult = await SendNotificationAsync(notification, recipients, NotificationChannel.Email);
                results.Add(emailResult);
            }

            if (request.SendInApp && _providers.ContainsKey(NotificationChannel.InApp))
            {
                var inAppResult = await SendNotificationAsync(notification, recipients, NotificationChannel.InApp);
                results.Add(inAppResult);
            }

            _logger.LogInformation("Bulk notification sent via {ChannelCount} channels with {SuccessCount} successes", 
                results.Count, results.Count(r => r.IsSuccess));

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk notification");
            return new List<NotificationDeliveryResult>
            {
                new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Status = NotificationDeliveryStatus.Failed
                }
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendScheduledNotificationAsync(Guid scheduledNotificationId)
    {
        try
        {
            var scheduledNotification = await _apiService.GetAsync<ScheduledNotificationDto>($"/api/notifications/scheduled/{scheduledNotificationId}");
            if (scheduledNotification == null)
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Scheduled notification not found",
                    Status = NotificationDeliveryStatus.Failed
                };
            }

            // Create notification from template if specified
            NotificationDto notification;
            if (scheduledNotification.ReportConfig.TemplateId != null)
            {
                var content = await _templateService.RenderNotificationAsync(
                    scheduledNotification.ReportConfig.TemplateId.Value,
                    scheduledNotification.ReportConfig.Parameters ?? new Dictionary<string, object>(),
                    NotificationChannel.InApp);
                
                notification = new NotificationDto
                {
                    Title = content.Title,
                    Message = content.Body,
                    Type = NotificationType.System,
                    Priority = NotificationPriority.Normal
                };
            }
            else
            {
                notification = new NotificationDto
                {
                    Title = "Scheduled Notification",
                    Message = "This is a scheduled notification",
                    Type = NotificationType.System,
                    Priority = NotificationPriority.Normal
                };
            }

            return await SendNotificationAsync(notification, scheduledNotification.EmailRecipients, NotificationChannel.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send scheduled notification {ScheduledNotificationId}", scheduledNotificationId);
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendPushNotificationAsync(PushNotificationDto pushNotification)
    {
        try
        {
            if (!_providers.TryGetValue(NotificationChannel.Push, out var provider))
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Push notification provider not configured",
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = NotificationChannel.Push
                };
            }

            var notification = new NotificationDto
            {
                Title = pushNotification.Title,
                Message = pushNotification.Body,
                Type = NotificationType.General,
                Priority = pushNotification.Priority,
                Data = pushNotification.Data,
                ImageUrl = pushNotification.ImageUrl
            };

            return await provider.SendAsync(notification, pushNotification.DeviceTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification");
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = NotificationChannel.Push
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendEmailNotificationAsync(EmailNotificationDto emailNotification)
    {
        try
        {
            if (!_providers.TryGetValue(NotificationChannel.Email, out var provider))
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Email notification provider not configured",
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = NotificationChannel.Email
                };
            }

            var notification = new NotificationDto
            {
                Title = emailNotification.Subject,
                Message = emailNotification.TextBody,
                Type = NotificationType.General,
                Priority = emailNotification.Priority,
                Data = new Dictionary<string, string>
                {
                    ["html_body"] = emailNotification.HtmlBody,
                    ["from_address"] = emailNotification.FromAddress,
                    ["from_name"] = emailNotification.FromName
                }
            };

            var allRecipients = emailNotification.ToAddresses
                .Concat(emailNotification.CcAddresses)
                .Concat(emailNotification.BccAddresses)
                .ToList();

            return await provider.SendAsync(notification, allRecipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = NotificationChannel.Email
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendSmsNotificationAsync(SmsNotificationDto smsNotification)
    {
        try
        {
            if (!_providers.TryGetValue(NotificationChannel.SMS, out var provider))
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "SMS notification provider not configured",
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = NotificationChannel.SMS
                };
            }

            var notification = new NotificationDto
            {
                Title = "SMS Notification",
                Message = smsNotification.Message,
                Type = NotificationType.General,
                Priority = smsNotification.Priority,
                Data = smsNotification.ProviderOptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "")
            };

            if (!string.IsNullOrEmpty(smsNotification.FromNumber))
            {
                notification.Data["from_number"] = smsNotification.FromNumber;
            }

            notification.Data["is_unicode"] = smsNotification.IsUnicode.ToString();

            return await provider.SendAsync(notification, smsNotification.PhoneNumbers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS notification");
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = NotificationChannel.SMS
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendInAppNotificationAsync(InAppNotificationDto inAppNotification)
    {
        try
        {
            if (!_providers.TryGetValue(NotificationChannel.InApp, out var provider))
            {
                return new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = "In-app notification provider not configured",
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = NotificationChannel.InApp
                };
            }

            var notification = new NotificationDto
            {
                Title = inAppNotification.Title,
                Message = inAppNotification.Message,
                Type = NotificationType.General,
                Priority = inAppNotification.Priority,
                Data = inAppNotification.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? ""),
                ActionUrl = inAppNotification.ActionUrl,
                ImageUrl = inAppNotification.ImageUrl,
                ExpiresAt = inAppNotification.ExpiresAt
            };

            var userIdStrings = inAppNotification.UserIds.Select(id => id.ToString()).ToList();
            return await provider.SendAsync(notification, userIdStrings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send in-app notification");
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = NotificationChannel.InApp
            };
        }
    }

    public async Task<NotificationDeliveryResult> SendFromTemplateAsync(Guid templateId, Dictionary<string, object> templateData, List<string> recipients, NotificationChannel channel)
    {
        try
        {
            var content = await _templateService.RenderNotificationAsync(templateId, templateData, channel);
            
            var notification = new NotificationDto
            {
                Title = content.Title,
                Message = content.Body,
                Type = NotificationType.General,
                Priority = NotificationPriority.Normal,
                Data = content.ChannelSpecificData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "")
            };

            return await SendNotificationAsync(notification, recipients, channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification from template {TemplateId}", templateId);
            return new NotificationDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Status = NotificationDeliveryStatus.Failed,
                Channel = channel
            };
        }
    }

    public async Task<List<NotificationDeliveryResult>> SendBulkFromTemplateAsync(Guid templateId, List<TemplateRecipientData> recipientData, NotificationChannel channel)
    {
        try
        {
            var results = new List<NotificationDeliveryResult>();
            
            foreach (var batch in recipientData.Chunk(100)) // Process in batches
            {
                var batchTasks = batch.Select(async data =>
                {
                    try
                    {
                        var content = await _templateService.RenderNotificationAsync(templateId, data.TemplateData, channel, data.Culture ?? "en-US");
                        
                        var notification = new NotificationDto
                        {
                            Title = content.Title,
                            Message = content.Body,
                            Type = NotificationType.General,
                            Priority = NotificationPriority.Normal,
                            Data = content.ChannelSpecificData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "")
                        };

                        return await SendNotificationAsync(notification, new List<string> { data.Recipient }, channel);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification to {Recipient} from template {TemplateId}", data.Recipient, templateId);
                        return new NotificationDeliveryResult
                        {
                            IsSuccess = false,
                            ErrorMessage = ex.Message,
                            Status = NotificationDeliveryStatus.Failed,
                            Channel = channel
                        };
                    }
                });

                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk notifications from template {TemplateId}", templateId);
            return new List<NotificationDeliveryResult>
            {
                new NotificationDeliveryResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Status = NotificationDeliveryStatus.Failed,
                    Channel = channel
                }
            };
        }
    }

    public async Task<NotificationDeliveryStatus> GetDeliveryStatusAsync(Guid deliveryId)
    {
        try
        {
            var delivery = await _apiService.GetAsync<NotificationDeliveryDto>($"/api/notifications/deliveries/{deliveryId}");
            return delivery?.Status ?? NotificationDeliveryStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery status for {DeliveryId}", deliveryId);
            return NotificationDeliveryStatus.Failed;
        }
    }

    public async Task<List<NotificationDeliveryDto>> GetDeliveryHistoryAsync(Guid notificationId)
    {
        try
        {
            var history = await _apiService.GetAsync<List<NotificationDeliveryDto>>($"/api/notifications/{notificationId}/deliveries");
            return history ?? new List<NotificationDeliveryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery history for notification {NotificationId}", notificationId);
            return new List<NotificationDeliveryDto>();
        }
    }

    public async Task<bool> CancelScheduledDeliveryAsync(Guid deliveryId)
    {
        try
        {
            await _apiService.DeleteAsync($"/api/notifications/deliveries/{deliveryId}/cancel");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel scheduled delivery {DeliveryId}", deliveryId);
            return false;
        }
    }

    public async Task<bool> RetryFailedDeliveryAsync(Guid deliveryId)
    {
        try
        {
            await _apiService.PostAsync($"/api/notifications/deliveries/{deliveryId}/retry", new { });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry delivery {DeliveryId}", deliveryId);
            return false;
        }
    }

    public async Task TrackDeliveryEventAsync(Guid deliveryId, NotificationDeliveryEvent deliveryEvent, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var trackingData = new
            {
                DeliveryId = deliveryId,
                Event = deliveryEvent.ToString(),
                Timestamp = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            await _apiService.PostAsync("/api/notifications/deliveries/track", trackingData);
            _logger.LogDebug("Delivery event tracked: {DeliveryId} - {Event}", deliveryId, deliveryEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track delivery event for {DeliveryId}: {Event}", deliveryId, deliveryEvent);
        }
    }

    public async Task<NotificationDeliveryAnalyticsDto> GetDeliveryAnalyticsAsync(DateTime startDate, DateTime endDate, NotificationChannel? channel = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            if (channel.HasValue)
            {
                queryParams["channel"] = channel.Value.ToString();
            }

            var analytics = await _apiService.GetAsync<NotificationDeliveryAnalyticsDto>("/api/notifications/analytics/delivery", queryParams);
            return analytics ?? new NotificationDeliveryAnalyticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery analytics");
            return new NotificationDeliveryAnalyticsDto();
        }
    }

    public async Task<List<DeliveryProviderDto>> GetAvailableProvidersAsync(NotificationChannel channel)
    {
        try
        {
            var providers = await _apiService.GetAsync<List<DeliveryProviderDto>>($"/api/notifications/providers?channel={channel}");
            return providers ?? new List<DeliveryProviderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available providers for channel {Channel}", channel);
            return new List<DeliveryProviderDto>();
        }
    }

    public async Task<bool> TestProviderConnectionAsync(NotificationChannel channel, string providerId)
    {
        try
        {
            var result = await _apiService.PostAsync<bool>($"/api/notifications/providers/{providerId}/test", new { Channel = channel });
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test provider connection: {ProviderId}", providerId);
            return false;
        }
    }

    public async Task<DeliveryProviderHealthDto> GetProviderHealthAsync(string providerId)
    {
        try
        {
            var health = await _apiService.GetAsync<DeliveryProviderHealthDto>($"/api/notifications/providers/{providerId}/health");
            return health ?? new DeliveryProviderHealthDto { ProviderId = providerId, IsHealthy = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get provider health: {ProviderId}", providerId);
            return new DeliveryProviderHealthDto { ProviderId = providerId, IsHealthy = false };
        }
    }

    // Helper Methods
    private async Task<List<string>> FilterRecipientsByPreferencesAsync(List<string> recipients, NotificationType type, NotificationChannel channel)
    {
        try
        {
            var filteredRecipients = new List<string>();
            
            foreach (var recipient in recipients)
            {
                // For email/phone, use as-is. For user IDs, check preferences.
                if (Guid.TryParse(recipient, out var userId))
                {
                    var canSend = await _preferencesService.CanSendNotificationAsync(userId, type, channel);
                    if (canSend)
                    {
                        filteredRecipients.Add(recipient);
                    }
                }
                else
                {
                    // Direct email/phone number - add as-is
                    filteredRecipients.Add(recipient);
                }
            }

            return filteredRecipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to filter recipients by preferences");
            return recipients; // Return original list if filtering fails
        }
    }

    private static List<NotificationChannel> GetRequestedChannels(BulkNotificationRequestDto request)
    {
        var channels = new List<NotificationChannel>();
        if (request.SendPush) channels.Add(NotificationChannel.Push);
        if (request.SendEmail) channels.Add(NotificationChannel.Email);
        if (request.SendInApp) channels.Add(NotificationChannel.InApp);
        return channels;
    }

    private static NotificationDto CreateNotificationFromRequest(BulkNotificationRequestDto request)
    {
        return new NotificationDto
        {
            Title = request.Title,
            Message = request.Message,
            Type = request.Type,
            Priority = request.Priority,
            Data = request.Data,
            ActionUrl = request.ActionUrl,
            ImageUrl = request.ImageUrl,
            ExpiresAt = request.ExpiresAt
        };
    }

    private async Task<List<string>> GetRecipientsFromRequestAsync(BulkNotificationRequestDto request)
    {
        var recipients = new List<string>();

        if (request.UserIds?.Any() == true)
        {
            recipients.AddRange(request.UserIds.Select(id => id.ToString()));
        }

        if (request.AllUsers)
        {
            // In a real implementation, this would fetch all active users
            var allUsers = await _apiService.GetAsync<List<Guid>>("/api/users/all-ids");
            if (allUsers != null)
            {
                recipients.AddRange(allUsers.Select(id => id.ToString()));
            }
        }

        return recipients.Distinct().ToList();
    }
}

// Provider Interface
public interface INotificationProvider
{
    NotificationChannel Channel { get; }
    Task<NotificationDeliveryResult> SendAsync(NotificationDto notification, List<string> recipients);
    Task<bool> TestConnectionAsync();
    Task<DeliveryProviderHealthDto> GetHealthAsync();
}