using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface IAnalyticsService
{
    // Project Analytics
    Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(Guid projectId, AnalyticsRequest request);
    Task<IEnumerable<ProjectAnalyticsDto>> GetMultipleProjectAnalyticsAsync(List<Guid> projectIds, AnalyticsRequest request);
    Task<ProjectAnalyticsDto> GetProjectAnalyticsByDateRangeAsync(Guid projectId, DateTime startDate, DateTime endDate);

    // User Analytics
    Task<UserProductivityDto> GetUserProductivityAsync(Guid userId, AnalyticsRequest request);
    Task<IEnumerable<UserProductivityDto>> GetTeamProductivityAsync(Guid projectId, AnalyticsRequest request);
    Task<UserProductivityDto> GetUserAnalyticsByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate);

    // Team Analytics
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid projectId, AnalyticsRequest request);
    Task<IEnumerable<TeamAnalyticsDto>> GetMultipleTeamAnalyticsAsync(List<Guid> projectIds, AnalyticsRequest request);

    // Time Tracking Analytics
    Task<TimeTrackingAnalyticsDto> GetTimeTrackingAnalyticsAsync(AnalyticsRequest request);
    Task<TimeTrackingAnalyticsDto> GetUserTimeTrackingAsync(Guid userId, AnalyticsRequest request);
    Task<TimeTrackingAnalyticsDto> GetProjectTimeTrackingAsync(Guid projectId, AnalyticsRequest request);

    // Business Intelligence
    Task<BusinessIntelligenceDto> GenerateBusinessIntelligenceReportAsync(AnalyticsRequest request, Guid userId);
    Task<BusinessIntelligenceDto> GetExecutiveDashboardAsync(AnalyticsRequest request, Guid userId);

    // Dashboard Data
    Task<DashboardDto> GetUserDashboardAsync(Guid userId);
    Task<DashboardDto> GetProjectDashboardAsync(Guid projectId, Guid userId);
    Task<DashboardDto> GetManagerDashboardAsync(Guid userId, List<Guid> projectIds);

    // Report Generation
    Task<string> GenerateReportAsync(ReportGenerationRequest request, Guid userId);
    Task<byte[]> ExportReportAsync(ReportGenerationRequest request, Guid userId);
    Task<bool> ScheduleReportAsync(ReportGenerationRequest request, string schedule, Guid userId);

    // Trend Analysis
    Task<List<TrendDataPoint>> GetTaskCompletionTrendAsync(Guid? projectId, DateTime startDate, DateTime endDate);
    Task<List<TrendDataPoint>> GetProductivityTrendAsync(Guid? userId, DateTime startDate, DateTime endDate);
    Task<List<TrendDataPoint>> GetCollaborationTrendAsync(Guid projectId, DateTime startDate, DateTime endDate);

    // Performance Metrics
    Task<PerformanceIndicators> CalculatePerformanceIndicatorsAsync(Guid? projectId, Guid? userId, DateTime startDate, DateTime endDate);
    Task<List<RecommendationItem>> GenerateRecommendationsAsync(Guid userId);

    // Real-time Analytics
    Task<QuickStats> GetQuickStatsAsync(Guid userId);
    Task<List<RecentActivity>> GetRecentActivitiesAsync(Guid userId, int count = 10);
    Task<List<UpcomingDeadline>> GetUpcomingDeadlinesAsync(Guid userId, int days = 7);

    // Custom Analytics
    Task<object> ExecuteCustomQueryAsync(string query, Dictionary<string, object> parameters, Guid userId);
    Task<ChartData> GenerateCustomChartAsync(string chartType, string dataQuery, Guid userId);

    // Cache Management
    Task RefreshAnalyticsCacheAsync(Guid? projectId = null, Guid? userId = null);
    Task<bool> IsCacheValidAsync(string cacheKey);

    // Data Aggregation
    Task ProcessDailyAggregationAsync(DateTime date);
    Task ProcessWeeklyAggregationAsync(DateTime weekStart);
    Task ProcessMonthlyAggregationAsync(DateTime monthStart);
}