using Microsoft.EntityFrameworkCore;
using MauiApp.AnalyticsService.Data;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Entities;
using Newtonsoft.Json;

namespace MauiApp.AnalyticsService.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AnalyticsDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(AnalyticsDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(Guid projectId, AnalyticsRequest request)
    {
        try
        {
            var cacheKey = $"project_analytics_{projectId}_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}";
            
            // Check cache first
            var cached = await GetFromCacheAsync<ProjectAnalyticsDto>(cacheKey);
            if (cached != null) return cached;

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new ArgumentException("Project not found");

            var tasks = await _context.Tasks
                .Where(t => t.ProjectId == projectId && 
                           t.CreatedAt >= request.StartDate && 
                           t.CreatedAt <= request.EndDate)
                .ToListAsync();

            var timeEntries = await _context.TimeEntries
                .Include(t => t.Task)
                .Where(t => t.Task.ProjectId == projectId &&
                           t.StartTime >= request.StartDate &&
                           t.StartTime <= request.EndDate)
                .ToListAsync();

            var result = new ProjectAnalyticsDto
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done),
                InProgressTasks = tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t => t.DueDate < DateTime.UtcNow && t.Status != MauiApp.Core.Entities.TaskStatus.Done),
                CompletionRate = tasks.Count > 0 ? (double)tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done) / tasks.Count * 100 : 0,
                AverageTaskCompletionTime = CalculateAverageCompletionTime(tasks),
                TotalTeamMembers = project.Members?.Count ?? 0,
                ActiveTeamMembers = await GetActiveTeamMembersCount(projectId, request.StartDate, request.EndDate),
                TotalTimeLogged = timeEntries.Sum(t => t.DurationMinutes / 60.0),
                ProjectStartDate = project.StartDate,
                ProjectEndDate = project.EndDate,
                TasksByPriority = GetTaskPriorityBreakdown(tasks),
                TasksByStatus = GetTaskStatusBreakdown(tasks),
                DailyProgress = await GetDailyProgressData(projectId, request.StartDate, request.EndDate)
            };

            // Cache result
            await SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(15));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project analytics for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<IEnumerable<ProjectAnalyticsDto>> GetMultipleProjectAnalyticsAsync(List<Guid> projectIds, AnalyticsRequest request)
    {
        var results = new List<ProjectAnalyticsDto>();
        
        foreach (var projectId in projectIds)
        {
            try
            {
                var analytics = await GetProjectAnalyticsAsync(projectId, request);
                results.Add(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get analytics for project {ProjectId}", projectId);
            }
        }

        return results;
    }

    public async Task<ProjectAnalyticsDto> GetProjectAnalyticsByDateRangeAsync(Guid projectId, DateTime startDate, DateTime endDate)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            ProjectId = projectId
        };

        return await GetProjectAnalyticsAsync(projectId, request);
    }

    public async Task<UserProductivityDto> GetUserProductivityAsync(Guid userId, AnalyticsRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var tasks = await _context.Tasks
                .Where(t => t.AssigneeId == userId &&
                           t.CreatedAt >= request.StartDate &&
                           t.CreatedAt <= request.EndDate)
                .ToListAsync();

            var timeEntries = await _context.TimeEntries
                .Where(t => t.UserId == userId &&
                           t.StartTime >= request.StartDate &&
                           t.StartTime <= request.EndDate)
                .ToListAsync();

            return new UserProductivityDto
            {
                UserId = userId,
                UserName = $"{user.FirstName} {user.LastName}",
                UserEmail = user.Email,
                TasksCompleted = tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done),
                TasksInProgress = tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.InProgress),
                TasksOverdue = tasks.Count(t => t.DueDate < DateTime.UtcNow && t.Status != MauiApp.Core.Entities.TaskStatus.Done),
                TotalTimeLogged = timeEntries.Sum(t => t.DurationMinutes / 60.0),
                AverageTaskCompletionTime = CalculateAverageCompletionTime(tasks),
                ProductivityScore = CalculateProductivityScore(tasks, timeEntries),
                ProjectContributions = await GetProjectContributions(userId, request.StartDate, request.EndDate),
                DailyActivities = await GetDailyActivities(userId, request.StartDate, request.EndDate)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user productivity for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<UserProductivityDto>> GetTeamProductivityAsync(Guid projectId, AnalyticsRequest request)
    {
        var members = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => pm.UserId)
            .ToListAsync();

        var results = new List<UserProductivityDto>();
        
        foreach (var userId in members)
        {
            try
            {
                var productivity = await GetUserProductivityAsync(userId, request);
                results.Add(productivity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get productivity for user {UserId}", userId);
            }
        }

        return results;
    }

    public async Task<UserProductivityDto> GetUserAnalyticsByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            UserId = userId
        };

        return await GetUserProductivityAsync(userId, request);
    }

    public async Task<QuickStats> GetQuickStatsAsync(Guid userId)
    {
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var weekEnd = weekStart.AddDays(7);

        var myTasks = await _context.Tasks
            .Where(t => t.AssigneeId == userId)
            .ToListAsync();

        var weeklyTimeEntries = await _context.TimeEntries
            .Where(t => t.UserId == userId && t.StartTime >= weekStart && t.StartTime < weekEnd)
            .ToListAsync();

        var upcomingDeadlines = await _context.Tasks
            .Where(t => t.AssigneeId == userId && 
                       t.DueDate >= DateTime.UtcNow && 
                       t.DueDate <= DateTime.UtcNow.AddDays(7) &&
                       t.Status != MauiApp.Core.Entities.TaskStatus.Done)
            .CountAsync();

        return new QuickStats
        {
            MyActiveTasks = myTasks.Count(t => t.Status != MauiApp.Core.Entities.TaskStatus.Done),
            MyOverdueTasks = myTasks.Count(t => t.DueDate < DateTime.UtcNow && t.Status != MauiApp.Core.Entities.TaskStatus.Done),
            MyCompletedThisWeek = myTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done && t.UpdatedAt >= weekStart),
            MyHoursThisWeek = weeklyTimeEntries.Sum(t => t.DurationMinutes / 60.0),
            TeamMessages = 0, // TODO: Implement once chat messages are available
            UpcomingDeadlines = upcomingDeadlines
        };
    }

    public async Task<List<RecentActivity>> GetRecentActivitiesAsync(Guid userId, int count = 10)
    {
        return await _context.UserActivityLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .Select(a => new RecentActivity
            {
                Id = a.Id,
                Type = a.ActivityType,
                Description = a.Description,
                UserName = _context.Users.Where(u => u.Id == a.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "",
                Timestamp = a.Timestamp,
                EntityType = a.EntityType,
                EntityId = a.EntityId ?? Guid.Empty
            })
            .ToListAsync();
    }

    public async Task<List<UpcomingDeadline>> GetUpcomingDeadlinesAsync(Guid userId, int days = 7)
    {
        var endDate = DateTime.UtcNow.AddDays(days);

        return await _context.Tasks
            .Where(t => t.AssigneeId == userId &&
                       t.DueDate >= DateTime.UtcNow &&
                       t.DueDate <= endDate &&
                       t.Status != MauiApp.Core.Entities.TaskStatus.Done)
            .Select(t => new UpcomingDeadline
            {
                TaskId = t.Id,
                TaskTitle = t.Title,
                ProjectName = _context.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.Name).FirstOrDefault() ?? "",
                DueDate = t.DueDate ?? DateTime.MaxValue,
                DaysRemaining = (int)(t.DueDate!.Value.Date - DateTime.UtcNow.Date).TotalDays,
                Priority = t.Priority.ToString(),
                AssigneeName = _context.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? ""
            })
            .OrderBy(u => u.DueDate)
            .ToListAsync();
    }

    // Additional interface methods with basic implementations
    public Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid projectId, AnalyticsRequest request) => throw new NotImplementedException();
    public Task<IEnumerable<TeamAnalyticsDto>> GetMultipleTeamAnalyticsAsync(List<Guid> projectIds, AnalyticsRequest request) => throw new NotImplementedException();
    public Task<TimeTrackingAnalyticsDto> GetTimeTrackingAnalyticsAsync(AnalyticsRequest request) => throw new NotImplementedException();
    public Task<TimeTrackingAnalyticsDto> GetUserTimeTrackingAsync(Guid userId, AnalyticsRequest request) => throw new NotImplementedException();
    public Task<TimeTrackingAnalyticsDto> GetProjectTimeTrackingAsync(Guid projectId, AnalyticsRequest request) => throw new NotImplementedException();
    public Task<BusinessIntelligenceDto> GenerateBusinessIntelligenceReportAsync(AnalyticsRequest request, Guid userId) => throw new NotImplementedException();
    public Task<BusinessIntelligenceDto> GetExecutiveDashboardAsync(AnalyticsRequest request, Guid userId) => throw new NotImplementedException();
    public Task<DashboardDto> GetUserDashboardAsync(Guid userId) => throw new NotImplementedException();
    public Task<DashboardDto> GetProjectDashboardAsync(Guid projectId, Guid userId) => throw new NotImplementedException();
    public Task<DashboardDto> GetManagerDashboardAsync(Guid userId, List<Guid> projectIds) => throw new NotImplementedException();
    public Task<string> GenerateReportAsync(ReportGenerationRequest request, Guid userId) => throw new NotImplementedException();
    public Task<byte[]> ExportReportAsync(ReportGenerationRequest request, Guid userId) => throw new NotImplementedException();
    public Task<bool> ScheduleReportAsync(ReportGenerationRequest request, string schedule, Guid userId) => throw new NotImplementedException();
    public Task<List<TrendDataPoint>> GetTaskCompletionTrendAsync(Guid? projectId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<List<TrendDataPoint>> GetProductivityTrendAsync(Guid? userId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<List<TrendDataPoint>> GetCollaborationTrendAsync(Guid projectId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<PerformanceIndicators> CalculatePerformanceIndicatorsAsync(Guid? projectId, Guid? userId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<List<RecommendationItem>> GenerateRecommendationsAsync(Guid userId) => throw new NotImplementedException();
    public Task<object> ExecuteCustomQueryAsync(string query, Dictionary<string, object> parameters, Guid userId) => throw new NotImplementedException();
    public Task<ChartData> GenerateCustomChartAsync(string chartType, string dataQuery, Guid userId) => throw new NotImplementedException();

    // Cache management
    public async Task RefreshAnalyticsCacheAsync(Guid? projectId = null, Guid? userId = null)
    {
        var cacheItems = _context.AnalyticsCache.AsQueryable();
        
        if (projectId.HasValue)
            cacheItems = cacheItems.Where(c => c.EntityId == projectId.Value);
        if (userId.HasValue)
            cacheItems = cacheItems.Where(c => c.EntityId == userId.Value);

        var itemsToRemove = await cacheItems.ToListAsync();
        _context.AnalyticsCache.RemoveRange(itemsToRemove);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsCacheValidAsync(string cacheKey)
    {
        var cacheItem = await _context.AnalyticsCache
            .FirstOrDefaultAsync(c => c.CacheKey == cacheKey);
        
        return cacheItem != null && cacheItem.ExpiresAt > DateTime.UtcNow;
    }

    // Data aggregation
    public async Task ProcessDailyAggregationAsync(DateTime date)
    {
        // Process daily metrics aggregation
        await ProcessTaskMetrics(date, "daily");
        await ProcessProductivityMetrics(date, "daily");
        await ProcessTimeMetrics(date, "daily");
    }

    public async Task ProcessWeeklyAggregationAsync(DateTime weekStart)
    {
        await ProcessTaskMetrics(weekStart, "weekly");
        await ProcessProductivityMetrics(weekStart, "weekly");
        await ProcessTimeMetrics(weekStart, "weekly");
    }

    public async Task ProcessMonthlyAggregationAsync(DateTime monthStart)
    {
        await ProcessTaskMetrics(monthStart, "monthly");
        await ProcessProductivityMetrics(monthStart, "monthly");
        await ProcessTimeMetrics(monthStart, "monthly");
    }

    // Helper methods
    private async Task<T?> GetFromCacheAsync<T>(string cacheKey) where T : class
    {
        var cacheItem = await _context.AnalyticsCache
            .FirstOrDefaultAsync(c => c.CacheKey == cacheKey && c.ExpiresAt > DateTime.UtcNow);
        
        if (cacheItem != null)
        {
            return JsonConvert.DeserializeObject<T>(cacheItem.Data);
        }
        
        return null;
    }

    private async Task SetCacheAsync<T>(string cacheKey, T data, TimeSpan expiration)
    {
        var existingCache = await _context.AnalyticsCache
            .FirstOrDefaultAsync(c => c.CacheKey == cacheKey);

        var cacheItem = existingCache ?? new AnalyticsCache { CacheKey = cacheKey };
        
        cacheItem.Data = JsonConvert.SerializeObject(data);
        cacheItem.DataType = typeof(T).Name;
        cacheItem.ExpiresAt = DateTime.UtcNow.Add(expiration);
        cacheItem.CreatedAt = DateTime.UtcNow;

        if (existingCache == null)
            _context.AnalyticsCache.Add(cacheItem);

        await _context.SaveChangesAsync();
    }

    private double CalculateAverageCompletionTime(List<ProjectTask> tasks)
    {
        var completedTasks = tasks.Where(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done).ToList();
        if (!completedTasks.Any()) return 0;

        return completedTasks.Average(t => (t.UpdatedAt - t.CreatedAt).TotalDays);
    }

    private async Task<int> GetActiveTeamMembersCount(Guid projectId, DateTime startDate, DateTime endDate)
    {
        return await _context.UserActivityLogs
            .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
            .Join(_context.ProjectMembers.Where(pm => pm.ProjectId == projectId),
                  a => a.UserId,
                  pm => pm.UserId,
                  (a, pm) => a.UserId)
            .Distinct()
            .CountAsync();
    }

    private List<TaskPriorityBreakdown> GetTaskPriorityBreakdown(List<ProjectTask> tasks)
    {
        var total = tasks.Count;
        if (total == 0) return new List<TaskPriorityBreakdown>();

        return tasks.GroupBy(t => t.Priority.ToString())
            .Select(g => new TaskPriorityBreakdown
            {
                Priority = g.Key,
                Count = g.Count(),
                Percentage = (double)g.Count() / total * 100
            }).ToList();
    }

    private List<TaskStatusBreakdown> GetTaskStatusBreakdown(List<ProjectTask> tasks)
    {
        var total = tasks.Count;
        if (total == 0) return new List<TaskStatusBreakdown>();

        return tasks.GroupBy(t => t.Status.ToString())
            .Select(g => new TaskStatusBreakdown
            {
                Status = g.Key,
                Count = g.Count(),
                Percentage = (double)g.Count() / total * 100
            }).ToList();
    }

    private async Task<List<DailyProgressData>> GetDailyProgressData(Guid projectId, DateTime startDate, DateTime endDate)
    {
        var dailyMetrics = await _context.DailyMetrics
            .Where(dm => dm.EntityId == projectId &&
                        dm.EntityType == "project" &&
                        dm.Date >= startDate &&
                        dm.Date <= endDate)
            .ToListAsync();

        return dailyMetrics.GroupBy(dm => dm.Date.Date)
            .Select(g => new DailyProgressData
            {
                Date = g.Key,
                TasksCompleted = (int)(g.FirstOrDefault(m => m.MetricName == "tasks_completed")?.Value ?? 0),
                TasksCreated = (int)(g.FirstOrDefault(m => m.MetricName == "tasks_created")?.Value ?? 0),
                TimeLogged = g.FirstOrDefault(m => m.MetricName == "time_logged")?.Value ?? 0
            })
            .OrderBy(d => d.Date)
            .ToList();
    }

    private double CalculateProductivityScore(List<ProjectTask> tasks, List<TimeEntry> timeEntries)
    {
        // Simple productivity calculation - can be enhanced
        var completedTasks = tasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done);
        var totalHours = timeEntries.Sum(t => t.DurationMinutes / 60.0);
        
        if (totalHours == 0) return 0;
        return totalHours > 0 ? (completedTasks / totalHours) * 10 : 0; // Scale to 0-100
    }

    private async Task<List<ProjectContribution>> GetProjectContributions(Guid userId, DateTime startDate, DateTime endDate)
    {
        var contributions = await _context.Tasks
            .Where(t => t.AssigneeId == userId &&
                       t.CreatedAt >= startDate &&
                       t.CreatedAt <= endDate)
            .GroupBy(t => t.ProjectId)
            .ToListAsync();

        var results = new List<ProjectContribution>();
        
        foreach (var group in contributions)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == group.Key);
            var timeContributed = await _context.TimeEntries
                .Include(t => t.Task)
                .Where(t => t.UserId == userId && t.Task.ProjectId == group.Key &&
                           t.StartTime >= startDate && t.StartTime <= endDate)
                .SumAsync(t => t.DurationMinutes / 60.0);

            results.Add(new ProjectContribution
            {
                ProjectId = group.Key,
                ProjectName = project?.Name ?? "Unknown",
                TasksCompleted = group.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done),
                TimeContributed = timeContributed,
                ContributionPercentage = 0 // TODO: Calculate based on total project work
            });
        }

        return results;
    }

    private async Task<List<DailyActivity>> GetDailyActivities(Guid userId, DateTime startDate, DateTime endDate)
    {
        var activities = await _context.UserActivityLogs
            .Where(a => a.UserId == userId &&
                       a.Timestamp >= startDate &&
                       a.Timestamp <= endDate)
            .GroupBy(a => a.Timestamp.Date)
            .ToListAsync();

        return activities.Select(g => new DailyActivity
        {
            Date = g.Key,
            TasksCompleted = g.Count(a => a.ActivityType == "task_completed"),
            HoursWorked = 0, // TODO: Calculate from time entries
            CommentsAdded = g.Count(a => a.ActivityType == "comment_added"),
            FilesUploaded = g.Count(a => a.ActivityType == "file_uploaded")
        }).OrderBy(d => d.Date).ToList();
    }

    private async Task ProcessTaskMetrics(DateTime date, string period)
    {
        // Implementation for processing task metrics
        // This would aggregate task completion data, etc.
        await Task.CompletedTask;
    }

    private async Task ProcessProductivityMetrics(DateTime date, string period)
    {
        // Implementation for processing productivity metrics
        await Task.CompletedTask;
    }

    private async Task ProcessTimeMetrics(DateTime date, string period)
    {
        // Implementation for processing time tracking metrics
        await Task.CompletedTask;
    }
}