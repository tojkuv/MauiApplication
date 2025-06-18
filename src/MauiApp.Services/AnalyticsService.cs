using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MauiApp.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IApiService _apiService;
    private readonly IAnalyticsCacheService _cacheService;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly Dictionary<string, List<Action<object>>> _subscriptions = new();

    public AnalyticsService(
        IApiService apiService,
        IAnalyticsCacheService cacheService,
        ILogger<AnalyticsService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardAnalyticsAsync(Guid? userId = null, Guid? projectId = null)
    {
        try
        {
            var cacheKey = CacheKey.ForDashboard(userId, projectId);
            var cached = await _cacheService.GetAsync<DashboardDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Dashboard analytics retrieved from cache");
                return cached;
            }

            var queryParams = new Dictionary<string, object>();
            if (userId.HasValue) queryParams["userId"] = userId.Value;
            if (projectId.HasValue) queryParams["projectId"] = projectId.Value;

            var dashboard = await _apiService.GetAsync<DashboardDto>("/api/analytics/dashboard", queryParams);
            
            if (dashboard != null)
            {
                await _cacheService.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Dashboard analytics retrieved for user {UserId}, project {ProjectId}", userId, projectId);
            }

            return dashboard ?? new DashboardDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard analytics for user {UserId}, project {ProjectId}", userId, projectId);
            return new DashboardDto();
        }
    }

    public async Task<AdvancedDashboardDto> GetAdvancedDashboardAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var dashboard = await _apiService.GetAsync<AdvancedDashboardDto>("/api/analytics/advanced-dashboard", queryParams);
            
            _logger.LogInformation("Advanced dashboard analytics retrieved for period {StartDate} to {EndDate}", startDate, endDate);
            return dashboard ?? new AdvancedDashboardDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get advanced dashboard analytics");
            return new AdvancedDashboardDto();
        }
    }

    public async Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var cacheKey = $"{CacheKey.ForProject(projectId)}_{CacheKey.ForTimeRange(startDate ?? DateTime.Today.AddDays(-30), endDate ?? DateTime.Today)}";
            var cached = await _cacheService.GetAsync<ProjectAnalyticsDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var queryParams = new Dictionary<string, object> { ["projectId"] = projectId };
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var analytics = await _apiService.GetAsync<ProjectAnalyticsDto>("/api/analytics/project", queryParams);
            
            if (analytics != null)
            {
                await _cacheService.SetAsync(cacheKey, analytics, TimeSpan.FromMinutes(15));
                _logger.LogInformation("Project analytics retrieved for project {ProjectId}", projectId);
            }

            return analytics ?? new ProjectAnalyticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project analytics for project {ProjectId}", projectId);
            return new ProjectAnalyticsDto();
        }
    }

    public async Task<List<ProjectAnalyticsDto>> GetProjectsAnalyticsAsync(List<Guid>? projectIds = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (projectIds?.Any() == true) queryParams["projectIds"] = projectIds;
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var analytics = await _apiService.PostAsync<List<ProjectAnalyticsDto>>("/api/analytics/projects", queryParams);
            
            _logger.LogInformation("Multiple projects analytics retrieved for {ProjectCount} projects", projectIds?.Count ?? 0);
            return analytics ?? new List<ProjectAnalyticsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects analytics");
            return new List<ProjectAnalyticsDto>();
        }
    }

    public async Task<UserProductivityDto> GetUserProductivityAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var cacheKey = $"{CacheKey.ForUser(userId)}_{CacheKey.ForTimeRange(startDate ?? DateTime.Today.AddDays(-30), endDate ?? DateTime.Today)}";
            var cached = await _cacheService.GetAsync<UserProductivityDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var queryParams = new Dictionary<string, object> { ["userId"] = userId };
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var productivity = await _apiService.GetAsync<UserProductivityDto>("/api/analytics/user", queryParams);
            
            if (productivity != null)
            {
                await _cacheService.SetAsync(cacheKey, productivity, TimeSpan.FromMinutes(10));
                _logger.LogInformation("User productivity retrieved for user {UserId}", userId);
            }

            return productivity ?? new UserProductivityDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user productivity for user {UserId}", userId);
            return new UserProductivityDto();
        }
    }

    public async Task<List<UserProductivityDto>> GetUsersProductivityAsync(List<Guid>? userIds = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (userIds?.Any() == true) queryParams["userIds"] = userIds;
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var productivity = await _apiService.PostAsync<List<UserProductivityDto>>("/api/analytics/users", queryParams);
            
            _logger.LogInformation("Multiple users productivity retrieved for {UserCount} users", userIds?.Count ?? 0);
            return productivity ?? new List<UserProductivityDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users productivity");
            return new List<UserProductivityDto>();
        }
    }

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object> { ["projectId"] = projectId };
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var analytics = await _apiService.GetAsync<TeamAnalyticsDto>("/api/analytics/team", queryParams);
            
            _logger.LogInformation("Team analytics retrieved for project {ProjectId}", projectId);
            return analytics ?? new TeamAnalyticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get team analytics for project {ProjectId}", projectId);
            return new TeamAnalyticsDto();
        }
    }

    public async Task<List<TeamAnalyticsDto>> GetTeamsAnalyticsAsync(List<Guid>? projectIds = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (projectIds?.Any() == true) queryParams["projectIds"] = projectIds;
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var analytics = await _apiService.PostAsync<List<TeamAnalyticsDto>>("/api/analytics/teams", queryParams);
            
            _logger.LogInformation("Multiple teams analytics retrieved for {ProjectCount} projects", projectIds?.Count ?? 0);
            return analytics ?? new List<TeamAnalyticsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get teams analytics");
            return new List<TeamAnalyticsDto>();
        }
    }

    public async Task<TimeTrackingAnalyticsDto> GetTimeTrackingAnalyticsAsync(Guid? projectId = null, Guid? userId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (projectId.HasValue) queryParams["projectId"] = projectId.Value;
            if (userId.HasValue) queryParams["userId"] = userId.Value;
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var analytics = await _apiService.GetAsync<TimeTrackingAnalyticsDto>("/api/analytics/time-tracking", queryParams);
            
            _logger.LogInformation("Time tracking analytics retrieved for project {ProjectId}, user {UserId}", projectId, userId);
            return analytics ?? new TimeTrackingAnalyticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get time tracking analytics");
            return new TimeTrackingAnalyticsDto();
        }
    }

    public async Task<BusinessIntelligenceDto> GetBusinessIntelligenceAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var cacheKey = $"{CacheKey.BusinessIntelligence}_{CacheKey.ForTimeRange(startDate, endDate)}";
            var cached = await _cacheService.GetAsync<BusinessIntelligenceDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var bi = await _apiService.GetAsync<BusinessIntelligenceDto>("/api/analytics/business-intelligence", queryParams);
            
            if (bi != null)
            {
                await _cacheService.SetAsync(cacheKey, bi, TimeSpan.FromHours(1));
                _logger.LogInformation("Business intelligence retrieved for period {StartDate} to {EndDate}", startDate, endDate);
            }

            return bi ?? new BusinessIntelligenceDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get business intelligence");
            return new BusinessIntelligenceDto();
        }
    }

    public async Task<List<ChartData>> GetChartDataAsync(string chartType, AnalyticsRequest request)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["chartType"] = chartType,
                ["request"] = request
            };

            var chartData = await _apiService.PostAsync<List<ChartData>>("/api/analytics/charts", queryParams);
            
            _logger.LogInformation("Chart data retrieved for type {ChartType}", chartType);
            return chartData ?? new List<ChartData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chart data for type {ChartType}", chartType);
            return new List<ChartData>();
        }
    }

    public async Task<ChartData> GetCustomChartAsync(string dataSource, Dictionary<string, object> parameters)
    {
        try
        {
            var request = new
            {
                DataSource = dataSource,
                Parameters = parameters
            };

            var chartData = await _apiService.PostAsync<ChartData>("/api/analytics/custom-chart", request);
            
            _logger.LogInformation("Custom chart data retrieved for data source {DataSource}", dataSource);
            return chartData ?? new ChartData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get custom chart data for data source {DataSource}", dataSource);
            return new ChartData();
        }
    }

    public async Task<CustomReportDto> GenerateCustomReportAsync(ReportGenerationRequest request)
    {
        try
        {
            var report = await _apiService.PostAsync<CustomReportDto>("/api/analytics/reports/generate", request);
            
            _logger.LogInformation("Custom report generated: {ReportType}", request.ReportType);
            return report ?? new CustomReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate custom report");
            return new CustomReportDto();
        }
    }

    public async Task<List<ReportTemplateDto>> GetReportTemplatesAsync()
    {
        try
        {
            var templates = await _apiService.GetAsync<List<ReportTemplateDto>>("/api/analytics/report-templates");
            
            _logger.LogInformation("Report templates retrieved: {Count} templates", templates?.Count ?? 0);
            return templates ?? new List<ReportTemplateDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get report templates");
            return new List<ReportTemplateDto>();
        }
    }

    public async Task<ReportTemplateDto> CreateReportTemplateAsync(ReportTemplateDto template)
    {
        try
        {
            var createdTemplate = await _apiService.PostAsync<ReportTemplateDto>("/api/analytics/report-templates", template);
            
            _logger.LogInformation("Report template created: {TemplateName}", template.Name);
            return createdTemplate ?? new ReportTemplateDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create report template");
            return new ReportTemplateDto();
        }
    }

    public async Task<CustomReportDto> GenerateReportFromTemplateAsync(Guid templateId, Dictionary<string, object> parameters)
    {
        try
        {
            var request = new
            {
                TemplateId = templateId,
                Parameters = parameters
            };

            var report = await _apiService.PostAsync<CustomReportDto>("/api/analytics/reports/from-template", request);
            
            _logger.LogInformation("Report generated from template {TemplateId}", templateId);
            return report ?? new CustomReportDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report from template {TemplateId}", templateId);
            return new CustomReportDto();
        }
    }

    public async Task<byte[]> ExportReportAsync(Guid reportId, string format)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["reportId"] = reportId,
                ["format"] = format
            };

            var data = await _apiService.GetAsync<byte[]>("/api/analytics/reports/export", queryParams);
            
            _logger.LogInformation("Report exported: {ReportId} in format {Format}", reportId, format);
            return data ?? Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export report {ReportId}", reportId);
            return Array.Empty<byte>();
        }
    }

    public async Task<string> ExportDashboardAsync(string format, Guid? userId = null, Guid? projectId = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object> { ["format"] = format };
            if (userId.HasValue) queryParams["userId"] = userId.Value;
            if (projectId.HasValue) queryParams["projectId"] = projectId.Value;

            var exportData = await _apiService.GetAsync<string>("/api/analytics/dashboard/export", queryParams);
            
            _logger.LogInformation("Dashboard exported in format {Format}", format);
            return exportData ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export dashboard");
            return string.Empty;
        }
    }

    public async Task<object> GetRealTimeMetricsAsync(string metricType)
    {
        try
        {
            var metrics = await _apiService.GetAsync<object>($"/api/analytics/realtime/{metricType}");
            
            _logger.LogDebug("Real-time metrics retrieved for type {MetricType}", metricType);
            return metrics ?? new object();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get real-time metrics for type {MetricType}", metricType);
            return new object();
        }
    }

    public Task SubscribeToMetricUpdatesAsync(string metricType, Action<object> callback)
    {
        if (!_subscriptions.ContainsKey(metricType))
        {
            _subscriptions[metricType] = new List<Action<object>>();
        }
        
        _subscriptions[metricType].Add(callback);
        _logger.LogInformation("Subscribed to metric updates for type {MetricType}", metricType);
        
        return Task.CompletedTask;
    }

    public Task UnsubscribeFromMetricUpdatesAsync(string metricType)
    {
        if (_subscriptions.ContainsKey(metricType))
        {
            _subscriptions.Remove(metricType);
            _logger.LogInformation("Unsubscribed from metric updates for type {MetricType}", metricType);
        }
        
        return Task.CompletedTask;
    }

    public async Task<List<PredictiveInsightDto>> GetPredictiveInsightsAsync(Guid? projectId = null, Guid? userId = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (projectId.HasValue) queryParams["projectId"] = projectId.Value;
            if (userId.HasValue) queryParams["userId"] = userId.Value;

            var insights = await _apiService.GetAsync<List<PredictiveInsightDto>>("/api/analytics/predictive/insights", queryParams);
            
            _logger.LogInformation("Predictive insights retrieved for project {ProjectId}, user {UserId}", projectId, userId);
            return insights ?? new List<PredictiveInsightDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get predictive insights");
            return new List<PredictiveInsightDto>();
        }
    }

    public async Task<ProjectForecastDto> GetProjectForecastAsync(Guid projectId)
    {
        try
        {
            var forecast = await _apiService.GetAsync<ProjectForecastDto>($"/api/analytics/predictive/project-forecast/{projectId}");
            
            _logger.LogInformation("Project forecast retrieved for project {ProjectId}", projectId);
            return forecast ?? new ProjectForecastDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project forecast for project {ProjectId}", projectId);
            return new ProjectForecastDto();
        }
    }

    public async Task<List<AnomalyDetectionDto>> GetAnomaliesAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var queryParams = new Dictionary<string, object>();
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var anomalies = await _apiService.GetAsync<List<AnomalyDetectionDto>>("/api/analytics/anomalies", queryParams);
            
            _logger.LogInformation("Anomalies retrieved for period {StartDate} to {EndDate}", startDate, endDate);
            return anomalies ?? new List<AnomalyDetectionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get anomalies");
            return new List<AnomalyDetectionDto>();
        }
    }

    public async Task<PerformanceMetricsDto> GetPerformanceMetricsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var cacheKey = $"{CacheKey.PerformanceMetrics}_{CacheKey.ForTimeRange(startDate ?? DateTime.Today, endDate ?? DateTime.Today)}";
            var cached = await _cacheService.GetAsync<PerformanceMetricsDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var queryParams = new Dictionary<string, object>();
            if (startDate.HasValue) queryParams["startDate"] = startDate.Value;
            if (endDate.HasValue) queryParams["endDate"] = endDate.Value;

            var metrics = await _apiService.GetAsync<PerformanceMetricsDto>("/api/analytics/performance", queryParams);
            
            if (metrics != null)
            {
                await _cacheService.SetAsync(cacheKey, metrics, TimeSpan.FromMinutes(2));
                _logger.LogInformation("Performance metrics retrieved");
            }

            return metrics ?? new PerformanceMetricsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance metrics");
            return new PerformanceMetricsDto();
        }
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync()
    {
        try
        {
            var cacheKey = CacheKey.SystemHealth;
            var cached = await _cacheService.GetAsync<SystemHealthDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var health = await _apiService.GetAsync<SystemHealthDto>("/api/analytics/system-health");
            
            if (health != null)
            {
                await _cacheService.SetAsync(cacheKey, health, TimeSpan.FromMinutes(1));
                _logger.LogInformation("System health retrieved");
            }

            return health ?? new SystemHealthDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system health");
            return new SystemHealthDto();
        }
    }

    public async Task<List<AlertDto>> GetActiveAlertsAsync()
    {
        try
        {
            var alerts = await _apiService.GetAsync<List<AlertDto>>("/api/analytics/alerts/active");
            
            _logger.LogInformation("Active alerts retrieved: {Count} alerts", alerts?.Count ?? 0);
            return alerts ?? new List<AlertDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active alerts");
            return new List<AlertDto>();
        }
    }

    public async Task<AlertDto> CreateAlertAsync(string type, string message, string severity, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var alertRequest = new
            {
                Type = type,
                Message = message,
                Severity = severity,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            var alert = await _apiService.PostAsync<AlertDto>("/api/analytics/alerts", alertRequest);
            
            _logger.LogInformation("Alert created: {Type} - {Message}", type, message);
            return alert ?? new AlertDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert");
            return new AlertDto();
        }
    }

    public async Task MarkAlertAsReadAsync(Guid alertId)
    {
        try
        {
            await _apiService.PutAsync($"/api/analytics/alerts/{alertId}/read", new { });
            _logger.LogInformation("Alert marked as read: {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark alert as read: {AlertId}", alertId);
        }
    }

    public async Task DismissAlertAsync(Guid alertId)
    {
        try
        {
            await _apiService.DeleteAsync($"/api/analytics/alerts/{alertId}");
            _logger.LogInformation("Alert dismissed: {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss alert: {AlertId}", alertId);
        }
    }

    public async Task InvalidateAnalyticsCacheAsync(string cacheKey)
    {
        try
        {
            await _cacheService.RemoveAsync(cacheKey);
            _logger.LogInformation("Analytics cache invalidated: {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate analytics cache: {CacheKey}", cacheKey);
        }
    }

    public async Task WarmUpCacheAsync()
    {
        try
        {
            var warmUpKeys = new List<string>
            {
                CacheKey.Dashboard,
                CacheKey.SystemHealth,
                CacheKey.PerformanceMetrics
            };

            await _cacheService.WarmUpAsync(warmUpKeys);
            _logger.LogInformation("Analytics cache warmed up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up analytics cache");
        }
    }

    public async Task<TimeSpan> GetCacheExpiryAsync(string cacheKey)
    {
        try
        {
            var ttl = await _cacheService.GetTtlAsync(cacheKey);
            return ttl ?? TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache expiry for key: {CacheKey}", cacheKey);
            return TimeSpan.Zero;
        }
    }
}