using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface IAnalyticsService
{
    // Dashboard Analytics
    Task<DashboardDto> GetDashboardAnalyticsAsync(Guid? userId = null, Guid? projectId = null);
    Task<AdvancedDashboardDto> GetAdvancedDashboardAsync(DateTime? startDate = null, DateTime? endDate = null);
    
    // Project Analytics
    Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<ProjectAnalyticsDto>> GetProjectsAnalyticsAsync(List<Guid>? projectIds = null, DateTime? startDate = null, DateTime? endDate = null);
    
    // User Analytics
    Task<UserProductivityDto> GetUserProductivityAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<UserProductivityDto>> GetUsersProductivityAsync(List<Guid>? userIds = null, DateTime? startDate = null, DateTime? endDate = null);
    
    // Team Analytics
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<TeamAnalyticsDto>> GetTeamsAnalyticsAsync(List<Guid>? projectIds = null, DateTime? startDate = null, DateTime? endDate = null);
    
    // Time Tracking Analytics
    Task<TimeTrackingAnalyticsDto> GetTimeTrackingAnalyticsAsync(Guid? projectId = null, Guid? userId = null, DateTime? startDate = null, DateTime? endDate = null);
    
    // Business Intelligence
    Task<BusinessIntelligenceDto> GetBusinessIntelligenceAsync(DateTime startDate, DateTime endDate);
    
    // Chart Data
    Task<List<ChartData>> GetChartDataAsync(string chartType, AnalyticsRequest request);
    Task<ChartData> GetCustomChartAsync(string dataSource, Dictionary<string, object> parameters);
    
    // Custom Reports
    Task<CustomReportDto> GenerateCustomReportAsync(ReportGenerationRequest request);
    Task<List<ReportTemplateDto>> GetReportTemplatesAsync();
    Task<ReportTemplateDto> CreateReportTemplateAsync(ReportTemplateDto template);
    Task<CustomReportDto> GenerateReportFromTemplateAsync(Guid templateId, Dictionary<string, object> parameters);
    
    // Export Functionality
    Task<byte[]> ExportReportAsync(Guid reportId, string format);
    Task<string> ExportDashboardAsync(string format, Guid? userId = null, Guid? projectId = null);
    
    // Real-time Analytics
    Task<object> GetRealTimeMetricsAsync(string metricType);
    Task SubscribeToMetricUpdatesAsync(string metricType, Action<object> callback);
    Task UnsubscribeFromMetricUpdatesAsync(string metricType);
    
    // Predictive Analytics
    Task<List<PredictiveInsightDto>> GetPredictiveInsightsAsync(Guid? projectId = null, Guid? userId = null);
    Task<ProjectForecastDto> GetProjectForecastAsync(Guid projectId);
    Task<List<AnomalyDetectionDto>> GetAnomaliesAsync(DateTime? startDate = null, DateTime? endDate = null);
    
    // Performance Analytics
    Task<PerformanceMetricsDto> GetPerformanceMetricsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<SystemHealthDto> GetSystemHealthAsync();
    
    // Alerts and Notifications
    Task<List<AlertDto>> GetActiveAlertsAsync();
    Task<AlertDto> CreateAlertAsync(string type, string message, string severity, Dictionary<string, object>? metadata = null);
    Task MarkAlertAsReadAsync(Guid alertId);
    Task DismissAlertAsync(Guid alertId);
    
    // Caching and Performance
    Task InvalidateAnalyticsCacheAsync(string cacheKey);
    Task WarmUpCacheAsync();
    Task<TimeSpan> GetCacheExpiryAsync(string cacheKey);
}

public interface IReportingService
{
    // Report Generation
    Task<CustomReportDto> CreateReportAsync(string name, string description, ReportType type, DateTime startDate, DateTime endDate);
    Task<List<CustomReportDto>> GetUserReportsAsync(Guid userId);
    Task<CustomReportDto> GetReportAsync(Guid reportId);
    Task<bool> DeleteReportAsync(Guid reportId);
    
    // Report Scheduling
    Task<ScheduledReportDto> CreateScheduledReportAsync(CreateScheduledReportDto request);
    Task<List<ScheduledReportDto>> GetScheduledReportsAsync(Guid userId);
    Task<bool> UpdateScheduledReportAsync(Guid reportId, ScheduledReportDto updatedReport);
    Task<bool> DeleteScheduledReportAsync(Guid reportId);
    Task<bool> EnableScheduledReportAsync(Guid reportId, bool enabled);
    
    // Report Templates
    Task<List<ReportTemplateDto>> GetPublicTemplatesAsync();
    Task<List<ReportTemplateDto>> GetUserTemplatesAsync(Guid userId);
    Task<ReportTemplateDto> CreateTemplateAsync(ReportTemplateDto template);
    Task<bool> UpdateTemplateAsync(Guid templateId, ReportTemplateDto template);
    Task<bool> DeleteTemplateAsync(Guid templateId);
    Task<bool> ShareTemplateAsync(Guid templateId, bool isPublic);
    
    // Data Export
    Task<byte[]> ExportToExcelAsync(object data, string reportName);
    Task<byte[]> ExportToPdfAsync(object data, string reportName, bool includeCharts = true);
    Task<string> ExportToCsvAsync(object data);
    Task<string> ExportToJsonAsync(object data);
    
    // Visualization
    Task<List<ChartDataDto>> GenerateChartsAsync(object data, List<ReportVisualizationDto> visualizations);
    Task<ChartDataDto> CreateChartAsync(string chartType, object data, ChartOptionsDto? options = null);
    
    // Report Sharing
    Task<string> GenerateShareableLinkAsync(Guid reportId, TimeSpan? expiresIn = null);
    Task<CustomReportDto> GetSharedReportAsync(string shareToken);
    Task<bool> RevokeShareLinkAsync(Guid reportId);
    
    // Email Reports
    Task SendReportByEmailAsync(Guid reportId, List<string> recipients, string? message = null);
    Task SendScheduledReportsAsync();
}

public interface IAnalyticsCacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
    Task<bool> ExistsAsync(string key);
    Task<TimeSpan?> GetTtlAsync(string key);
    Task RefreshAsync(string key);
    Task WarmUpAsync(List<string> keys);
}

public interface IMetricsCollectionService
{
    // Real-time Metrics Collection
    Task RecordMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null);
    Task RecordEventAsync(string eventName, Dictionary<string, object>? properties = null);
    Task IncrementCounterAsync(string counterName, Dictionary<string, string>? tags = null);
    Task RecordTimingAsync(string operationName, TimeSpan duration, Dictionary<string, string>? tags = null);
    
    // Batch Metrics
    Task RecordMetricsBatchAsync(List<MetricDataPoint> metrics);
    Task FlushPendingMetricsAsync();
    
    // System Metrics
    Task RecordSystemMetricsAsync();
    Task RecordPerformanceMetricsAsync(string endpoint, TimeSpan responseTime, bool success);
    Task RecordUserActivityAsync(Guid userId, string activity, Dictionary<string, object>? metadata = null);
    
    // Custom Metrics
    Task<string> CreateCustomMetricAsync(string name, string description, string unit, string aggregationType);
    Task<List<string>> GetAvailableMetricsAsync();
    Task<Dictionary<string, object>> GetMetricDefinitionAsync(string metricName);
}

// Supporting Classes
public class MetricDataPoint
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Tags { get; set; } = new();
    public string Unit { get; set; } = string.Empty;
}

public class CacheKey
{
    public const string Dashboard = "analytics:dashboard";
    public const string ProjectAnalytics = "analytics:project";
    public const string UserProductivity = "analytics:user";
    public const string TeamAnalytics = "analytics:team";
    public const string TimeTracking = "analytics:time";
    public const string BusinessIntelligence = "analytics:bi";
    public const string SystemHealth = "analytics:health";
    public const string PerformanceMetrics = "analytics:performance";
    public const string PredictiveInsights = "analytics:predictions";
    
    public static string ForProject(Guid projectId) => $"{ProjectAnalytics}:{projectId}";
    public static string ForUser(Guid userId) => $"{UserProductivity}:{userId}";
    public static string ForTimeRange(DateTime start, DateTime end) => $"{start:yyyyMMdd}_{end:yyyyMMdd}";
    public static string ForDashboard(Guid? userId, Guid? projectId) => 
        $"{Dashboard}:{userId ?? Guid.Empty}:{projectId ?? Guid.Empty}";
}