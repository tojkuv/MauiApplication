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