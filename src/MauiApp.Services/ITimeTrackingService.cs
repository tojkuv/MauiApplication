using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface ITimeTrackingService
{
    // Time Entry Management
    Task<TimeEntryDto> StartTimeEntryAsync(Guid taskId, string? description = null);
    Task<TimeEntryDto> StopTimeEntryAsync(Guid timeEntryId);
    Task<TimeEntryDto> CreateTimeEntryAsync(CreateTimeEntryRequestDto request);
    Task<TimeEntryDto> UpdateTimeEntryAsync(Guid timeEntryId, TimeEntryDto timeEntry);
    Task<bool> DeleteTimeEntryAsync(Guid timeEntryId);
    
    // Time Entry Queries
    Task<TimeEntryDto?> GetTimeEntryAsync(Guid timeEntryId);
    Task<List<TimeEntryDto>> GetTimeEntriesAsync(TimeEntryFilterDto filter);
    Task<TimeEntryDto?> GetActiveTimeEntryAsync();
    Task<List<TimeEntryDto>> GetTodaysTimeEntriesAsync();
    Task<List<TimeEntryDto>> GetWeekTimeEntriesAsync(DateTime weekStart);
    Task<List<TimeEntryDto>> GetMonthTimeEntriesAsync(DateTime month);
    
    // Time Tracking Analytics
    Task<TimeTrackingStatsDto> GetTimeStatsAsync(TimeStatsFilterDto filter);
    Task<List<DailyTimeStatsDto>> GetDailyTimeStatsAsync(DateTime startDate, DateTime endDate);
    Task<List<ProjectTimeStatsDto>> GetProjectTimeStatsAsync(DateTime startDate, DateTime endDate);
    Task<List<TaskTimeStatsDto>> GetTaskTimeStatsAsync(Guid projectId, DateTime startDate, DateTime endDate);
    
    // Productivity Analytics
    Task<ProductivityReportDto> GetProductivityReportAsync(DateTime startDate, DateTime endDate);
    Task<List<ProductivityTrendDto>> GetProductivityTrendAsync(DateTime startDate, DateTime endDate);
    Task<WorkPatternAnalysisDto> AnalyzeWorkPatternsAsync(DateTime startDate, DateTime endDate);
    
    // Timer Management
    Task<bool> PauseActiveTimerAsync();
    Task<bool> ResumeActiveTimerAsync();
    Task<bool> IsTimerActiveAsync();
    Task<TimeSpan> GetActiveTimerDurationAsync();
    
    // Time Goals and Targets
    Task<TimeGoalDto> CreateTimeGoalAsync(CreateTimeGoalRequestDto request);
    Task<List<TimeGoalDto>> GetTimeGoalsAsync(bool activeOnly = true);
    Task<TimeGoalProgressDto> GetTimeGoalProgressAsync(Guid goalId);
    Task<bool> UpdateTimeGoalAsync(Guid goalId, TimeGoalDto goal);
    Task<bool> DeleteTimeGoalAsync(Guid goalId);
    
    // Time Reporting
    Task<TimesheetReportDto> GenerateTimesheetReportAsync(TimeReportFilterDto filter);
    Task<byte[]> ExportTimesheetAsync(TimeReportFilterDto filter, ExportFormat format);
    Task<ProjectTimeReportDto> GenerateProjectTimeReportAsync(Guid projectId, DateTime startDate, DateTime endDate);
    
    // Time Tracking Settings
    Task<TimeTrackingSettingsDto> GetSettingsAsync();
    Task<TimeTrackingSettingsDto> UpdateSettingsAsync(TimeTrackingSettingsDto settings);
    
    // Reminders and Notifications
    Task<bool> SetTimeTrackingReminderAsync(TimeSpan interval);
    Task<bool> EnableIdleTimeDetectionAsync(bool enabled);
    Task<List<IdleTimeDetectionDto>> GetIdleTimePeriodsAsync(DateTime date);
    
    // Events
    event EventHandler<TimeEntryStartedEventArgs>? TimeEntryStarted;
    event EventHandler<TimeEntryStoppedEventArgs>? TimeEntryStopped;
    event EventHandler<TimeEntryUpdatedEventArgs>? TimeEntryUpdated;
    event EventHandler<TimeGoalAchievedEventArgs>? TimeGoalAchieved;
    event EventHandler<IdleTimeDetectedEventArgs>? IdleTimeDetected;
}

// Supporting DTOs and Models
public class TimeEntryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsBillable { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? TotalAmount { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CreateTimeEntryRequestDto
{
    public Guid TaskId { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsBillable { get; set; } = true;
    public decimal? HourlyRate { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class TimeEntryFilterDto
{
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsBillable { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Tags { get; set; }
    public int PageSize { get; set; } = 50;
    public int PageNumber { get; set; } = 1;
}

public class TimeTrackingStatsDto
{
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public TimeSpan NonBillableTime { get; set; }
    public int TotalEntries { get; set; }
    public int ProjectsWorkedOn { get; set; }
    public int TasksWorkedOn { get; set; }
    public decimal TotalEarnings { get; set; }
    public TimeSpan AverageEntryDuration { get; set; }
    public TimeSpan LongestEntry { get; set; }
    public TimeSpan ShortestEntry { get; set; }
    public Dictionary<Guid, TimeSpan> TimeByProject { get; set; } = new();
    public Dictionary<string, TimeSpan> TimeByTag { get; set; } = new();
    public List<TimeEntryDto> RecentEntries { get; set; } = new();
}

public class TimeStatsFilterDto
{
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IncludeBillableOnly { get; set; } = false;
}

public class DailyTimeStatsDto
{
    public DateTime Date { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public int ProjectsWorkedOn { get; set; }
    public TimeSpan GoalTime { get; set; }
    public double GoalCompletionPercentage { get; set; }
    public List<TimeEntryDto> Entries { get; set; } = new();
}

public class ProjectTimeStatsDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public TimeSpan AverageSessionTime { get; set; }
    public DateTime? LastWorkedOn { get; set; }
    public int TasksWorkedOn { get; set; }
    public Dictionary<Guid, TimeSpan> TimeByTask { get; set; } = new();
}

public class TaskTimeStatsDto
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public DateTime? LastWorkedOn { get; set; }
    public TaskStatus TaskStatus { get; set; }
    public TimeSpan EstimatedTime { get; set; }
    public double CompletionPercentage { get; set; }
}

public class ProductivityReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan TotalWorkTime { get; set; }
    public TimeSpan ProductiveTime { get; set; }
    public double ProductivityScore { get; set; }
    public TimeSpan AverageSessionLength { get; set; }
    public int TotalSessions { get; set; }
    public TimeSpan LongestSession { get; set; }
    public List<ProductiveHourDto> MostProductiveHours { get; set; } = new();
    public List<ProductiveDayDto> MostProductiveDays { get; set; } = new();
    public Dictionary<string, TimeSpan> ProductivityByCategory { get; set; } = new();
    public List<ProductivityInsightDto> Insights { get; set; } = new();
}

public class ProductiveHourDto
{
    public int Hour { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double ProductivityScore { get; set; }
    public int SessionCount { get; set; }
}

public class ProductiveDayDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double ProductivityScore { get; set; }
    public int SessionCount { get; set; }
}

public class ProductivityTrendDto
{
    public DateTime Date { get; set; }
    public double ProductivityScore { get; set; }
    public TimeSpan WorkTime { get; set; }
    public int SessionCount { get; set; }
    public TimeSpan AverageSessionLength { get; set; }
}

public class ProductivityInsightDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProductivityInsightType Type { get; set; }
    public ProductivityInsightSeverity Severity { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

public class WorkPatternAnalysisDto
{
    public TimeSpan AverageStartTime { get; set; }
    public TimeSpan AverageEndTime { get; set; }
    public TimeSpan AverageWorkDuration { get; set; }
    public DayOfWeek MostProductiveDay { get; set; }
    public int MostProductiveHour { get; set; }
    public List<WorkSessionDto> TypicalWorkSessions { get; set; } = new();
    public Dictionary<DayOfWeek, TimeSpan> WorkPatternByDay { get; set; } = new();
    public Dictionary<int, TimeSpan> WorkPatternByHour { get; set; } = new();
    public List<string> PatternInsights { get; set; } = new();
}

public class WorkSessionDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int BreakCount { get; set; }
    public TimeSpan TotalBreakTime { get; set; }
    public double ProductivityScore { get; set; }
}

public class TimeGoalDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeGoalType Type { get; set; }
    public TimeSpan TargetTime { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeGoalPeriod Period { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTimeGoalRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeGoalType Type { get; set; }
    public TimeSpan TargetTime { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeGoalPeriod Period { get; set; }
}

public class TimeGoalProgressDto
{
    public Guid GoalId { get; set; }
    public TimeGoalDto Goal { get; set; } = new();
    public TimeSpan CurrentTime { get; set; }
    public double ProgressPercentage { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public bool IsAchieved { get; set; }
    public DateTime? AchievedAt { get; set; }
    public List<DailyProgressDto> DailyProgress { get; set; } = new();
    public TimeSpan DailyAverageRequired { get; set; }
    public bool IsOnTrack { get; set; }
}

public class DailyProgressDto
{
    public DateTime Date { get; set; }
    public TimeSpan TimeLogged { get; set; }
    public TimeSpan TargetTime { get; set; }
    public double CompletionPercentage { get; set; }
}

public class TimesheetReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public decimal TotalEarnings { get; set; }
    public List<TimesheetEntryDto> Entries { get; set; } = new();
    public Dictionary<Guid, ProjectTimeSummaryDto> ProjectSummaries { get; set; } = new();
    public List<TimesheetDayDto> DailySummaries { get; set; } = new();
}

public class TimesheetEntryDto
{
    public DateTime Date { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string TaskTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsBillable { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? Amount { get; set; }
}

public class ProjectTimeSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public decimal TotalEarnings { get; set; }
}

public class TimesheetDayDto
{
    public DateTime Date { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public decimal TotalEarnings { get; set; }
    public int EntryCount { get; set; }
}

public class TimeReportFilterDto
{
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool BillableOnly { get; set; } = false;
    public bool IncludeEmptyDays { get; set; } = true;
    public TimeReportGroupBy GroupBy { get; set; } = TimeReportGroupBy.Day;
}

public class ProjectTimeReportDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public decimal TotalEarnings { get; set; }
    public List<UserTimeReportDto> UserSummaries { get; set; } = new();
    public List<TaskTimeStatsDto> TaskSummaries { get; set; } = new();
    public List<DailyTimeStatsDto> DailySummaries { get; set; } = new();
}

public class UserTimeReportDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
    public TimeSpan BillableTime { get; set; }
    public decimal TotalEarnings { get; set; }
    public int EntryCount { get; set; }
}

public class TimeTrackingSettingsDto
{
    public Guid UserId { get; set; }
    public bool AutoStartTimer { get; set; } = false;
    public bool RemindToTrackTime { get; set; } = true;
    public TimeSpan ReminderInterval { get; set; } = TimeSpan.FromMinutes(30);
    public bool DetectIdleTime { get; set; } = true;
    public TimeSpan IdleTimeThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public bool RoundTimeEntries { get; set; } = false;
    public TimeRoundingMode RoundingMode { get; set; } = TimeRoundingMode.Nearest15Minutes;
    public bool RequireDescription { get; set; } = false;
    public bool DefaultToBillable { get; set; } = true;
    public decimal? DefaultHourlyRate { get; set; }
    public TimeGoalPeriod DefaultGoalPeriod { get; set; } = TimeGoalPeriod.Weekly;
    public TimeSpan DefaultDailyGoal { get; set; } = TimeSpan.FromHours(8);
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public class IdleTimeDetectionDto
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public Guid? ActiveTimeEntryId { get; set; }
    public IdleTimeAction Action { get; set; } = IdleTimeAction.Pending;
    public DateTime DetectedAt { get; set; }
}

// Event Args
public class TimeEntryStartedEventArgs : EventArgs
{
    public TimeEntryDto TimeEntry { get; set; } = new();
}

public class TimeEntryStoppedEventArgs : EventArgs
{
    public TimeEntryDto TimeEntry { get; set; } = new();
}

public class TimeEntryUpdatedEventArgs : EventArgs
{
    public TimeEntryDto TimeEntry { get; set; } = new();
}

public class TimeGoalAchievedEventArgs : EventArgs
{
    public TimeGoalDto Goal { get; set; } = new();
    public TimeGoalProgressDto Progress { get; set; } = new();
}

public class IdleTimeDetectedEventArgs : EventArgs
{
    public IdleTimeDetectionDto IdleTime { get; set; } = new();
}

// Enums
public enum TimeGoalType
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Project = 4,
    Task = 5,
    Custom = 6
}

public enum TimeGoalPeriod
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5,
    Custom = 6
}

public enum ProductivityInsightType
{
    Observation = 1,
    Recommendation = 2,
    Warning = 3,
    Achievement = 4
}

public enum ProductivityInsightSeverity
{
    Info = 1,
    Low = 2,
    Medium = 3,
    High = 4
}

public enum ExportFormat
{
    Csv = 1,
    Excel = 2,
    Pdf = 3,
    Json = 4
}

public enum TimeReportGroupBy
{
    Day = 1,
    Week = 2,
    Month = 3,
    Project = 4,
    Task = 5,
    User = 6
}

public enum TimeRoundingMode
{
    None = 0,
    Nearest5Minutes = 1,
    Nearest15Minutes = 2,
    Nearest30Minutes = 3,
    NearestHour = 4,
    Up5Minutes = 5,
    Up15Minutes = 6,
    Up30Minutes = 7,
    UpHour = 8
}

public enum IdleTimeAction
{
    Pending = 1,
    DiscardTime = 2,
    KeepTime = 3,
    Ignored = 4
}