using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MauiApp.Services;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<NotificationTemplateService> _logger;

    public NotificationTemplateService(
        IApiService apiService,
        ICacheService cacheService,
        ILogger<NotificationTemplateService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequestDto request)
    {
        try
        {
            _logger.LogInformation("Creating notification template: {TemplateName}", request.Name);

            // Validate template before creation
            var template = new NotificationTemplateDto
            {
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Type = request.Type,
                TitleTemplate = request.TitleTemplate,
                BodyTemplate = request.BodyTemplate,
                HtmlTemplate = request.HtmlTemplate,
                SubjectTemplate = request.SubjectTemplate,
                ChannelSettings = request.ChannelSettings,
                Variables = request.Variables,
                IsActive = request.IsActive
            };

            if (!await ValidateTemplateAsync(template))
            {
                throw new ArgumentException("Template validation failed");
            }

            var createdTemplate = await _apiService.PostAsync<NotificationTemplateDto>("/api/notifications/templates", request);
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("notification-templates-*");
            
            _logger.LogInformation("Created notification template: {TemplateId}", createdTemplate?.Id);
            return createdTemplate ?? new NotificationTemplateDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification template: {TemplateName}", request.Name);
            throw;
        }
    }

    public async Task<NotificationTemplateDto> GetTemplateAsync(Guid templateId)
    {
        try
        {
            var cacheKey = $"notification-template-{templateId}";
            var cached = await _cacheService.GetAsync<NotificationTemplateDto>(cacheKey);
            if (cached != null) return cached;

            var template = await _apiService.GetAsync<NotificationTemplateDto>($"/api/notifications/templates/{templateId}");
            
            if (template != null)
            {
                await _cacheService.SetAsync(cacheKey, template, TimeSpan.FromMinutes(30));
            }

            return template ?? new NotificationTemplateDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification template: {TemplateId}", templateId);
            return new NotificationTemplateDto();
        }
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync(NotificationType? type = null, bool? isActive = null)
    {
        try
        {
            var cacheKey = $"notification-templates-{type}-{isActive}";
            var cached = await _cacheService.GetAsync<List<NotificationTemplateDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>();
            if (type.HasValue) queryParams["type"] = type.Value.ToString();
            if (isActive.HasValue) queryParams["isActive"] = isActive.Value;

            var templates = await _apiService.GetAsync<List<NotificationTemplateDto>>("/api/notifications/templates", queryParams);
            
            if (templates != null)
            {
                await _cacheService.SetAsync(cacheKey, templates, TimeSpan.FromMinutes(15));
            }

            return templates ?? new List<NotificationTemplateDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification templates");
            return new List<NotificationTemplateDto>();
        }
    }

    public async Task<NotificationTemplateDto> UpdateTemplateAsync(Guid templateId, NotificationTemplateDto template)
    {
        try
        {
            _logger.LogInformation("Updating notification template: {TemplateId}", templateId);

            if (!await ValidateTemplateAsync(template))
            {
                throw new ArgumentException("Template validation failed");
            }

            var updatedTemplate = await _apiService.PutAsync<NotificationTemplateDto>($"/api/notifications/templates/{templateId}", template);
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-template-{templateId}");
            await _cacheService.RemoveByPatternAsync("notification-templates-*");
            
            _logger.LogInformation("Updated notification template: {TemplateId}", templateId);
            return updatedTemplate ?? new NotificationTemplateDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<bool> DeleteTemplateAsync(Guid templateId)
    {
        try
        {
            _logger.LogInformation("Deleting notification template: {TemplateId}", templateId);
            
            await _apiService.DeleteAsync($"/api/notifications/templates/{templateId}");
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-template-{templateId}");
            await _cacheService.RemoveByPatternAsync("notification-templates-*");
            
            _logger.LogInformation("Deleted notification template: {TemplateId}", templateId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete notification template: {TemplateId}", templateId);
            return false;
        }
    }

    public async Task<bool> ActivateTemplateAsync(Guid templateId, bool isActive)
    {
        try
        {
            _logger.LogInformation("Setting template {TemplateId} active status to: {IsActive}", templateId, isActive);
            
            await _apiService.PatchAsync($"/api/notifications/templates/{templateId}/activate", new { IsActive = isActive });
            
            // Clear cache
            await _cacheService.RemoveAsync($"notification-template-{templateId}");
            await _cacheService.RemoveByPatternAsync("notification-templates-*");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set template active status: {TemplateId}", templateId);
            return false;
        }
    }

    public async Task<string> RenderTemplateAsync(Guid templateId, Dictionary<string, object> data, string culture = "en-US")
    {
        try
        {
            var template = await GetTemplateAsync(templateId);
            if (template == null || string.IsNullOrEmpty(template.BodyTemplate))
            {
                throw new ArgumentException($"Template {templateId} not found or has no body template");
            }

            return RenderTemplateContent(template.BodyTemplate, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<NotificationContentDto> RenderNotificationAsync(Guid templateId, Dictionary<string, object> data, NotificationChannel channel, string culture = "en-US")
    {
        try
        {
            var template = await GetTemplateAsync(templateId);
            if (template == null)
            {
                throw new ArgumentException($"Template {templateId} not found");
            }

            var content = new NotificationContentDto
            {
                Title = RenderTemplateContent(template.TitleTemplate ?? "", data),
                Body = RenderTemplateContent(template.BodyTemplate ?? "", data),
                Subject = RenderTemplateContent(template.SubjectTemplate ?? "", data)
            };

            // Render HTML body if available
            if (!string.IsNullOrEmpty(template.HtmlTemplate))
            {
                content.HtmlBody = RenderTemplateContent(template.HtmlTemplate, data);
            }

            // Apply channel-specific settings
            if (template.ChannelSettings.TryGetValue(channel.ToString(), out var channelConfig))
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(channelConfig.ToString() ?? "{}");
                content.ChannelSpecificData = config ?? new Dictionary<string, object>();
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render notification from template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<bool> ValidateTemplateAsync(NotificationTemplateDto template)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrEmpty(template.Name) || string.IsNullOrEmpty(template.BodyTemplate))
            {
                return false;
            }

            // Validate template syntax
            var testData = template.Variables.ToDictionary(v => v.Name, v => GetDefaultValueForType(v.Type));
            
            try
            {
                RenderTemplateContent(template.BodyTemplate, testData);
                if (!string.IsNullOrEmpty(template.TitleTemplate))
                {
                    RenderTemplateContent(template.TitleTemplate, testData);
                }
                if (!string.IsNullOrEmpty(template.HtmlTemplate))
                {
                    RenderTemplateContent(template.HtmlTemplate, testData);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate template: {TemplateName}", template.Name);
            return false;
        }
    }

    public async Task<List<string>> GetTemplateCategoriesAsync()
    {
        try
        {
            var cacheKey = "notification-template-categories";
            var cached = await _cacheService.GetAsync<List<string>>(cacheKey);
            if (cached != null) return cached;

            var categories = await _apiService.GetAsync<List<string>>("/api/notifications/templates/categories");
            
            if (categories != null)
            {
                await _cacheService.SetAsync(cacheKey, categories, TimeSpan.FromHours(1));
            }

            return categories ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template categories");
            return new List<string>();
        }
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesByCategoryAsync(string category)
    {
        try
        {
            var cacheKey = $"notification-templates-category-{category}";
            var cached = await _cacheService.GetAsync<List<NotificationTemplateDto>>(cacheKey);
            if (cached != null) return cached;

            var templates = await _apiService.GetAsync<List<NotificationTemplateDto>>($"/api/notifications/templates/category/{category}");
            
            if (templates != null)
            {
                await _cacheService.SetAsync(cacheKey, templates, TimeSpan.FromMinutes(15));
            }

            return templates ?? new List<NotificationTemplateDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get templates by category: {Category}", category);
            return new List<NotificationTemplateDto>();
        }
    }

    public async Task<TemplateUsageAnalyticsDto> GetTemplateUsageAsync(Guid templateId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var analytics = await _apiService.GetAsync<TemplateUsageAnalyticsDto>($"/api/notifications/templates/{templateId}/analytics", queryParams);
            return analytics ?? new TemplateUsageAnalyticsDto { TemplateId = templateId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template usage analytics: {TemplateId}", templateId);
            return new TemplateUsageAnalyticsDto { TemplateId = templateId };
        }
    }

    public async Task<List<TemplatePerformanceDto>> GetTopPerformingTemplatesAsync(int count = 10)
    {
        try
        {
            var cacheKey = $"top-performing-templates-{count}";
            var cached = await _cacheService.GetAsync<List<TemplatePerformanceDto>>(cacheKey);
            if (cached != null) return cached;

            var templates = await _apiService.GetAsync<List<TemplatePerformanceDto>>($"/api/notifications/templates/performance/top?count={count}");
            
            if (templates != null)
            {
                await _cacheService.SetAsync(cacheKey, templates, TimeSpan.FromMinutes(10));
            }

            return templates ?? new List<TemplatePerformanceDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top performing templates");
            return new List<TemplatePerformanceDto>();
        }
    }

    // Helper Methods
    private static string RenderTemplateContent(string template, Dictionary<string, object> data)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var result = template;
        
        // Simple template variable replacement using {{variable}} syntax
        var variableRegex = new Regex(@"\{\{(\w+)\}\}", RegexOptions.IgnoreCase);
        var matches = variableRegex.Matches(template);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            if (data.TryGetValue(variableName, out var value))
            {
                result = result.Replace(match.Value, value?.ToString() ?? "");
            }
        }

        return result;
    }

    private static object GetDefaultValueForType(string type)
    {
        return type.ToLower() switch
        {
            "string" => "Sample Text",
            "number" => 42,
            "boolean" => true,
            "date" => DateTime.Now,
            _ => "Default Value"
        };
    }
}