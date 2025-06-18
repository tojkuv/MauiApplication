using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.DTOs;

public class ProjectAnalyticsDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OverdueTasks { get; set; }
    public double CompletionRate { get; set; }
    public double AverageTaskCompletionTime { get; set; } // in days
    public int TotalTeamMembers { get; set; }
    public int ActiveTeamMembers { get; set; }
    public double TotalTimeLogged { get; set; } // in hours
    public DateTime ProjectStartDate { get; set; }
    public DateTime? ProjectEndDate { get; set; }
    public List<TaskPriorityBreakdown> TasksByPriority { get; set; } = new();
    public List<TaskStatusBreakdown> TasksByStatus { get; set; } = new();
    public List<DailyProgressData> DailyProgress { get; set; } = new();
}

public class UserProductivityDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int TasksCompleted { get; set; }
    public int TasksInProgress { get; set; }
    public int TasksOverdue { get; set; }
    public double TotalTimeLogged { get; set; } // in hours
    public double AverageTaskCompletionTime { get; set; } // in days
    public double ProductivityScore { get; set; } // calculated metric
    public List<ProjectContribution> ProjectContributions { get; set; } = new();
    public List<DailyActivity> DailyActivities { get; set; } = new();
}

public class TeamAnalyticsDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public double TeamProductivityScore { get; set; }
    public double AverageTasksPerMember { get; set; }
    public double TotalTeamTimeLogged { get; set; }
    public List<UserProductivityDto> MemberPerformance { get; set; } = new();
    public List<TeamVelocityData> VelocityTrend { get; set; } = new();
    public TeamCollaborationMetrics CollaborationMetrics { get; set; } = new();
}

public class TimeTrackingAnalyticsDto
{
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalHours { get; set; }
    public double BillableHours { get; set; }
    public double NonBillableHours { get; set; }
    public List<ProjectTimeBreakdown> ProjectBreakdown { get; set; } = new();
    public List<DailyTimeEntry> DailyEntries { get; set; } = new();
    public List<TaskTimeBreakdown> TaskBreakdown { get; set; } = new();
}

public class BusinessIntelligenceDto
{
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public OverviewMetrics Overview { get; set; } = new();
    public List<ProjectAnalyticsDto> TopProjects { get; set; } = new();
    public List<UserProductivityDto> TopPerformers { get; set; } = new();
    public TrendAnalysis Trends { get; set; } = new();
    public List<RecommendationItem> Recommendations { get; set; } = new();
}

public class DashboardDto
{
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public QuickStats QuickStats { get; set; } = new();
    public List<ChartData> Charts { get; set; } = new();
    public List<RecentActivity> RecentActivities { get; set; } = new();
    public List<UpcomingDeadline> UpcomingDeadlines { get; set; } = new();
    public PerformanceIndicators Performance { get; set; } = new();
}

// Supporting Classes
public class TaskPriorityBreakdown
{
    public string Priority { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class TaskStatusBreakdown
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DailyProgressData
{
    public DateTime Date { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksCreated { get; set; }
    public double TimeLogged { get; set; }
}

public class ProjectContribution
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TasksCompleted { get; set; }
    public double TimeContributed { get; set; }
    public double ContributionPercentage { get; set; }
}

public class DailyActivity
{
    public DateTime Date { get; set; }
    public int TasksCompleted { get; set; }
    public double HoursWorked { get; set; }
    public int CommentsAdded { get; set; }
    public int FilesUploaded { get; set; }
}

public class TeamVelocityData
{
    public DateTime WeekStartDate { get; set; }
    public int TasksCompleted { get; set; }
    public int StoryPointsCompleted { get; set; }
    public double AverageCompletionTime { get; set; }
}

public class TeamCollaborationMetrics
{
    public int TotalMessages { get; set; }
    public int TotalComments { get; set; }
    public int FilesShared { get; set; }
    public double AverageResponseTime { get; set; } // in hours
    public List<CollaborationPair> TopCollaborators { get; set; } = new();
}

public class CollaborationPair
{
    public string User1Name { get; set; } = string.Empty;
    public string User2Name { get; set; } = string.Empty;
    public int InteractionCount { get; set; }
}

public class ProjectTimeBreakdown
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public double Hours { get; set; }
    public double Percentage { get; set; }
}

public class DailyTimeEntry
{
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public double BillableHours { get; set; }
}

public class TaskTimeBreakdown
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public double Hours { get; set; }
    public double Percentage { get; set; }
}

public class OverviewMetrics
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public double OverallCompletionRate { get; set; }
    public double TotalHoursLogged { get; set; }
}

public class TrendAnalysis
{
    public double TaskCompletionTrend { get; set; }
    public double ProductivityTrend { get; set; }
    public double CollaborationTrend { get; set; }
    public double TimeEfficiencyTrend { get; set; }
    public List<TrendDataPoint> CompletionRateTrend { get; set; } = new();
    public List<TrendDataPoint> ProductivityTrend_Data { get; set; } = new();
}

public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public double Change { get; set; }
}

public class RecommendationItem
{
    public string Type { get; set; } = string.Empty; // "productivity", "deadline", "resource"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium"; // low, medium, high
    public string ActionUrl { get; set; } = string.Empty;
}

public class QuickStats
{
    public int MyActiveTasks { get; set; }
    public int MyOverdueTasks { get; set; }
    public int MyCompletedThisWeek { get; set; }
    public double MyHoursThisWeek { get; set; }
    public int TeamMessages { get; set; }
    public int UpcomingDeadlines { get; set; }
}

public class ChartData
{
    public string Type { get; set; } = string.Empty; // "bar", "line", "pie", "doughnut"
    public string Title { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<ChartDataset> Datasets { get; set; } = new();
}

public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public List<double> Data { get; set; } = new();
    public string BackgroundColor { get; set; } = string.Empty;
    public string BorderColor { get; set; } = string.Empty;
}

public class RecentActivity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
}

public class UpcomingDeadline
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
}

public class PerformanceIndicators
{
    public double TaskCompletionRate { get; set; }
    public double OnTimeDeliveryRate { get; set; }
    public double TeamProductivityScore { get; set; }
    public double CollaborationIndex { get; set; }
    public double QualityScore { get; set; }
    public string PerformanceTrend { get; set; } = "stable"; // improving, stable, declining
}

// Request Models
public class AnalyticsRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string Granularity { get; set; } = "daily"; // daily, weekly, monthly
}

public class ReportGenerationRequest
{
    [Required]
    public string ReportType { get; set; } = string.Empty; // "project", "user", "team", "time"
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public List<Guid> ProjectIds { get; set; } = new();
    public List<Guid> UserIds { get; set; } = new();
    public string Format { get; set; } = "json"; // json, csv, pdf
    public bool IncludeCharts { get; set; } = true;
    public List<string> Metrics { get; set; } = new();
}

// Advanced Analytics DTOs
public class AdvancedDashboardDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public AdvancedProjectStatsDto ProjectStats { get; set; } = new();
    public AdvancedTaskStatsDto TaskStats { get; set; } = new();
    public AdvancedUserStatsDto UserStats { get; set; } = new();
    public PerformanceMetricsDto Performance { get; set; } = new();
    public List<PredictiveInsightDto> PredictiveInsights { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
    public SystemHealthDto SystemHealth { get; set; } = new();
}

public class AdvancedProjectStatsDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int DelayedProjects { get; set; }
    public double AverageProjectDuration { get; set; } // in days
    public double ProjectSuccessRate { get; set; }
    public List<ProjectRiskAssessmentDto> RiskAssessments { get; set; } = new();
    public List<ProjectForecastDto> Forecasts { get; set; } = new();
    public List<BurndownDataDto> BurndownData { get; set; } = new();
}

public class AdvancedTaskStatsDto
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public double TaskVelocity { get; set; } // tasks per day
    public double AverageCycleTime { get; set; } // hours
    public double TaskThroughput { get; set; } // tasks per week
    public List<TaskComplexityDto> ComplexityAnalysis { get; set; } = new();
    public List<BottleneckAnalysisDto> Bottlenecks { get; set; } = new();
    public List<TaskFlowDataDto> FlowMetrics { get; set; } = new();
}

public class AdvancedUserStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public double UserEngagementScore { get; set; }
    public List<SkillAnalysisDto> SkillAnalysis { get; set; } = new();
    public List<WorkloadAnalysisDto> WorkloadAnalysis { get; set; } = new();
    public List<CollaborationPatternDto> CollaborationPatterns { get; set; } = new();
}

public class PerformanceMetricsDto
{
    public double SystemUptime { get; set; }
    public double ResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public long TotalRequests { get; set; }
    public double ThroughputPerSecond { get; set; }
    public List<ApiEndpointMetricDto> EndpointMetrics { get; set; } = new();
    public ResourceUtilizationDto ResourceUtilization { get; set; } = new();
}

public class PredictiveInsightDto
{
    public string Type { get; set; } = string.Empty; // "deadline", "capacity", "risk"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; } // 0.0 to 1.0
    public DateTime PredictedDate { get; set; }
    public string Severity { get; set; } = "medium"; // low, medium, high, critical
    public List<string> Recommendations { get; set; } = new();
}

public class AlertDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SystemHealthDto
{
    public double OverallHealth { get; set; } // 0.0 to 1.0
    public DatabaseHealthDto Database { get; set; } = new();
    public ApiHealthDto Api { get; set; } = new();
    public StorageHealthDto Storage { get; set; } = new();
    public List<ServiceHealthDto> Services { get; set; } = new();
}

public class DatabaseHealthDto
{
    public double Health { get; set; }
    public double ConnectionPoolUsage { get; set; }
    public double QueryLatency { get; set; }
    public long ActiveConnections { get; set; }
    public long TotalQueries { get; set; }
}

public class ApiHealthDto
{
    public double Health { get; set; }
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public long RequestsPerMinute { get; set; }
    public int ActiveSessions { get; set; }
}

public class StorageHealthDto
{
    public double Health { get; set; }
    public double UsagePercentage { get; set; }
    public long TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public double ReadLatency { get; set; }
    public double WriteLatency { get; set; }
}

public class ServiceHealthDto
{
    public string ServiceName { get; set; } = string.Empty;
    public double Health { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastHealthCheck { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class ProjectRiskAssessmentDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public double RiskScore { get; set; } // 0.0 to 1.0
    public List<RiskFactorDto> RiskFactors { get; set; } = new();
    public List<MitigationDto> Mitigations { get; set; } = new();
}

public class RiskFactorDto
{
    public string Factor { get; set; } = string.Empty;
    public double Impact { get; set; }
    public double Probability { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class MitigationDto
{
    public string Strategy { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
}

public class ProjectForecastDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime PredictedCompletionDate { get; set; }
    public double ConfidenceLevel { get; set; }
    public double EstimatedEffort { get; set; }
    public List<MilestoneProjectionDto> MilestoneProjections { get; set; } = new();
}

public class MilestoneProjectionDto
{
    public string MilestoneName { get; set; } = string.Empty;
    public DateTime PredictedDate { get; set; }
    public double Confidence { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BurndownDataDto
{
    public DateTime Date { get; set; }
    public int RemainingWork { get; set; }
    public int IdealRemaining { get; set; }
    public int ActualCompleted { get; set; }
    public double Velocity { get; set; }
}

public class TaskComplexityDto
{
    public string ComplexityLevel { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public double AverageCompletionTime { get; set; }
    public double SuccessRate { get; set; }
}

public class BottleneckAnalysisDto
{
    public string BottleneckType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Impact { get; set; }
    public int AffectedTasks { get; set; }
    public List<string> Suggestions { get; set; } = new();
}

public class TaskFlowDataDto
{
    public string Stage { get; set; } = string.Empty;
    public int TasksEntered { get; set; }
    public int TasksExited { get; set; }
    public double AverageWaitTime { get; set; }
    public double WipLimit { get; set; }
}

public class SkillAnalysisDto
{
    public string Skill { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public double AverageLevel { get; set; }
    public int DemandScore { get; set; }
    public int SupplyScore { get; set; }
    public List<SkillGapDto> Gaps { get; set; } = new();
}

public class SkillGapDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public double CurrentLevel { get; set; }
    public double RequiredLevel { get; set; }
    public double GapSize { get; set; }
}

public class WorkloadAnalysisDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public double CurrentCapacity { get; set; } // 0.0 to 1.0
    public double OptimalCapacity { get; set; } = 0.8;
    public int AssignedTasks { get; set; }
    public TimeSpan EstimatedWorkload { get; set; }
    public string WorkloadStatus { get; set; } = "balanced"; // underutilized, balanced, overloaded
}

public class CollaborationPatternDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int CollaborationScore { get; set; }
    public List<string> PreferredCollaborators { get; set; } = new();
    public List<string> CommunicationPatterns { get; set; } = new();
    public double NetworkCentrality { get; set; }
}

public class ApiEndpointMetricDto
{
    public string Endpoint { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public double P95ResponseTime { get; set; }
    public double ThroughputPerSecond { get; set; }
}

public class ResourceUtilizationDto
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double StorageUsage { get; set; }
    public double NetworkUtilization { get; set; }
    public List<ResourceTrendDto> Trends { get; set; } = new();
}

public class ResourceTrendDto
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double StorageUsage { get; set; }
}

// Custom Report Builder DTOs
public class ReportTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<ReportFieldDto> Fields { get; set; } = new();
    public List<ReportVisualizationDto> Visualizations { get; set; } = new();
    public bool IsPublic { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReportFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsSortable { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
}

public class ReportVisualizationDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public class CustomReportDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public object Data { get; set; } = new();
    public List<ChartData> Charts { get; set; } = new();
    public string Format { get; set; } = "json";
}

// AI/ML Analytics DTOs
public class AiInsightsDto
{
    public List<PatternDetectionDto> Patterns { get; set; } = new();
    public List<AnomalyDetectionDto> Anomalies { get; set; } = new();
    public List<PredictionDto> Predictions { get; set; } = new();
    public List<RecommendationDto> Recommendations { get; set; } = new();
    public double ModelAccuracy { get; set; }
    public DateTime LastTraining { get; set; }
}

public class PatternDetectionDto
{
    public string PatternType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<DataPointDto> DataPoints { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class AnomalyDetectionDto
{
    public string AnomalyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public object AffectedEntity { get; set; } = new();
    public List<string> PossibleCauses { get; set; } = new();
}

public class PredictionDto
{
    public string PredictionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object PredictedValue { get; set; } = new();
    public double Confidence { get; set; }
    public DateTime PredictionDate { get; set; }
    public TimeSpan Horizon { get; set; }
}

public class RecommendationDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Impact { get; set; }
    public double Effort { get; set; }
    public string Priority { get; set; } = "medium";
    public List<string> Actions { get; set; } = new();
}

public class DataPointDto
{
    public DateTime Timestamp { get; set; }
    public object Value { get; set; } = new();
    public Dictionary<string, object> Attributes { get; set; } = new();
}