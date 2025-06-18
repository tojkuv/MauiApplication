using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Timers;

namespace MauiApp.Services;

public class TimeTrackingService : ITimeTrackingService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TimeTrackingService> _logger;
    private readonly System.Timers.Timer _idleTimer;
    private readonly System.Timers.Timer _reminderTimer;
    
    private TimeEntryDto? _activeTimeEntry;
    private DateTime _lastActivityTime;
    private TimeTrackingSettingsDto? _settings;

    public TimeTrackingService(
        IApiService apiService,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        ILogger<TimeTrackingService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _logger = logger;
        _lastActivityTime = DateTime.UtcNow;
        
        // Initialize timers
        _idleTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        _idleTimer.Elapsed += OnIdleTimerElapsed;
        _idleTimer.Start();
        
        _reminderTimer = new System.Timers.Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        _reminderTimer.Elapsed += OnReminderTimerElapsed;
        _reminderTimer.Start();
        
        // Load settings
        _ = Task.Run(LoadSettingsAsync);
    }

    public event EventHandler<TimeEntryStartedEventArgs>? TimeEntryStarted;
    public event EventHandler<TimeEntryStoppedEventArgs>? TimeEntryStopped;
    public event EventHandler<TimeEntryUpdatedEventArgs>? TimeEntryUpdated;
    public event EventHandler<TimeGoalAchievedEventArgs>? TimeGoalAchieved;
    public event EventHandler<IdleTimeDetectedEventArgs>? IdleTimeDetected;

    public async Task<TimeEntryDto> StartTimeEntryAsync(Guid taskId, string? description = null)
    {
        try
        {
            _logger.LogInformation("Starting time entry for task: {TaskId}", taskId);

            // Stop any active time entry first
            if (_activeTimeEntry != null)
            {
                await StopTimeEntryAsync(_activeTimeEntry.Id);
            }

            var request = new CreateTimeEntryRequestDto
            {
                TaskId = taskId,
                Description = description,
                StartTime = DateTime.UtcNow,
                IsBillable = _settings?.DefaultToBillable ?? true,
                HourlyRate = _settings?.DefaultHourlyRate
            };

            var timeEntry = await _apiService.PostAsync<TimeEntryDto>("/api/timetracking/entries", request);
            if (timeEntry != null)
            {
                timeEntry.IsActive = true;
                _activeTimeEntry = timeEntry;
                
                // Cache active entry
                await _cacheService.SetAsync("active-time-entry", timeEntry, TimeSpan.FromDays(1));
                
                OnTimeEntryStarted(new TimeEntryStartedEventArgs { TimeEntry = timeEntry });
                _logger.LogInformation("Time entry started: {TimeEntryId}", timeEntry.Id);
            }

            return timeEntry ?? new TimeEntryDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting time entry for task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<TimeEntryDto> StopTimeEntryAsync(Guid timeEntryId)
    {
        try
        {
            _logger.LogInformation("Stopping time entry: {TimeEntryId}", timeEntryId);

            var timeEntry = await _apiService.PatchAsync<TimeEntryDto>($"/api/timetracking/entries/{timeEntryId}/stop", new { EndTime = DateTime.UtcNow });
            
            if (timeEntry != null)
            {
                timeEntry.IsActive = false;
                
                // Apply rounding if enabled
                if (_settings?.RoundTimeEntries == true)
                {
                    timeEntry.Duration = RoundTimeEntry(timeEntry.Duration, _settings.RoundingMode);
                    await UpdateTimeEntryAsync(timeEntry.Id, timeEntry);
                }

                // Clear active entry
                if (_activeTimeEntry?.Id == timeEntryId)
                {
                    _activeTimeEntry = null;
                    await _cacheService.RemoveAsync("active-time-entry");
                }

                OnTimeEntryStopped(new TimeEntryStoppedEventArgs { TimeEntry = timeEntry });
                
                // Check time goals
                await CheckTimeGoalsAsync();
                
                _logger.LogInformation("Time entry stopped: {TimeEntryId}, Duration: {Duration}", timeEntry.Id, timeEntry.Duration);
            }

            return timeEntry ?? new TimeEntryDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping time entry: {TimeEntryId}", timeEntryId);
            throw;
        }
    }

    public async Task<TimeEntryDto> CreateTimeEntryAsync(CreateTimeEntryRequestDto request)
    {
        try
        {
            _logger.LogInformation("Creating manual time entry for task: {TaskId}", request.TaskId);

            var timeEntry = await _apiService.PostAsync<TimeEntryDto>("/api/timetracking/entries", request);
            
            if (timeEntry != null)
            {
                // Clear cache
                await _cacheService.RemoveByPatternAsync("time-entries-*");
                
                OnTimeEntryUpdated(new TimeEntryUpdatedEventArgs { TimeEntry = timeEntry });
                _logger.LogInformation("Manual time entry created: {TimeEntryId}", timeEntry.Id);
            }

            return timeEntry ?? new TimeEntryDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating time entry for task: {TaskId}", request.TaskId);
            throw;
        }
    }

    public async Task<TimeEntryDto> UpdateTimeEntryAsync(Guid timeEntryId, TimeEntryDto timeEntry)
    {
        try
        {
            var updatedEntry = await _apiService.PutAsync<TimeEntryDto>($"/api/timetracking/entries/{timeEntryId}", timeEntry);
            
            if (updatedEntry != null)
            {
                // Update active entry if it's the same
                if (_activeTimeEntry?.Id == timeEntryId)
                {
                    _activeTimeEntry = updatedEntry;
                    await _cacheService.SetAsync("active-time-entry", updatedEntry, TimeSpan.FromDays(1));
                }

                // Clear cache
                await _cacheService.RemoveByPatternAsync("time-entries-*");
                
                OnTimeEntryUpdated(new TimeEntryUpdatedEventArgs { TimeEntry = updatedEntry });
                _logger.LogInformation("Time entry updated: {TimeEntryId}", timeEntryId);
            }

            return updatedEntry ?? new TimeEntryDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating time entry: {TimeEntryId}", timeEntryId);
            throw;
        }
    }

    public async Task<bool> DeleteTimeEntryAsync(Guid timeEntryId)
    {
        try
        {
            await _apiService.DeleteAsync($"/api/timetracking/entries/{timeEntryId}");
            
            // Clear cache
            await _cacheService.RemoveByPatternAsync("time-entries-*");
            
            // Clear active entry if it's the same
            if (_activeTimeEntry?.Id == timeEntryId)
            {
                _activeTimeEntry = null;
                await _cacheService.RemoveAsync("active-time-entry");
            }

            _logger.LogInformation("Time entry deleted: {TimeEntryId}", timeEntryId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting time entry: {TimeEntryId}", timeEntryId);
            return false;
        }
    }

    public async Task<TimeEntryDto?> GetTimeEntryAsync(Guid timeEntryId)
    {
        try
        {
            var cacheKey = $"time-entry-{timeEntryId}";
            var cached = await _cacheService.GetAsync<TimeEntryDto>(cacheKey);
            if (cached != null) return cached;

            var timeEntry = await _apiService.GetAsync<TimeEntryDto>($"/api/timetracking/entries/{timeEntryId}");
            
            if (timeEntry != null)
            {
                await _cacheService.SetAsync(cacheKey, timeEntry, TimeSpan.FromMinutes(30));
            }

            return timeEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time entry: {TimeEntryId}", timeEntryId);
            return null;
        }
    }

    public async Task<List<TimeEntryDto>> GetTimeEntriesAsync(TimeEntryFilterDto filter)
    {
        try
        {
            var cacheKey = $"time-entries-{filter.GetHashCode()}";
            var cached = await _cacheService.GetAsync<List<TimeEntryDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>
            {
                ["pageSize"] = filter.PageSize,
                ["pageNumber"] = filter.PageNumber
            };

            if (filter.UserId.HasValue) queryParams["userId"] = filter.UserId.Value;
            if (filter.ProjectId.HasValue) queryParams["projectId"] = filter.ProjectId.Value;
            if (filter.TaskId.HasValue) queryParams["taskId"] = filter.TaskId.Value;
            if (filter.StartDate.HasValue) queryParams["startDate"] = filter.StartDate.Value;
            if (filter.EndDate.HasValue) queryParams["endDate"] = filter.EndDate.Value;
            if (filter.IsBillable.HasValue) queryParams["isBillable"] = filter.IsBillable.Value;
            if (filter.IsActive.HasValue) queryParams["isActive"] = filter.IsActive.Value;

            var entries = await _apiService.GetAsync<List<TimeEntryDto>>("/api/timetracking/entries", queryParams);
            
            if (entries != null)
            {
                await _cacheService.SetAsync(cacheKey, entries, TimeSpan.FromMinutes(5));
            }

            return entries ?? new List<TimeEntryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time entries");
            return new List<TimeEntryDto>();
        }
    }

    public async Task<TimeEntryDto?> GetActiveTimeEntryAsync()
    {
        try
        {
            if (_activeTimeEntry != null) return _activeTimeEntry;

            // Check cache
            var cached = await _cacheService.GetAsync<TimeEntryDto>("active-time-entry");
            if (cached != null)
            {
                _activeTimeEntry = cached;
                return cached;
            }

            // Get from server
            var activeEntry = await _apiService.GetAsync<TimeEntryDto>("/api/timetracking/entries/active");
            if (activeEntry != null)
            {
                _activeTimeEntry = activeEntry;
                await _cacheService.SetAsync("active-time-entry", activeEntry, TimeSpan.FromDays(1));
            }

            return activeEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active time entry");
            return null;
        }
    }

    public async Task<List<TimeEntryDto>> GetTodaysTimeEntriesAsync()
    {
        var today = DateTime.Today;
        var filter = new TimeEntryFilterDto
        {
            StartDate = today,
            EndDate = today.AddDays(1).AddTicks(-1),
            PageSize = 100
        };
        
        return await GetTimeEntriesAsync(filter);
    }

    public async Task<List<TimeEntryDto>> GetWeekTimeEntriesAsync(DateTime weekStart)
    {
        var filter = new TimeEntryFilterDto
        {
            StartDate = weekStart,
            EndDate = weekStart.AddDays(7).AddTicks(-1),
            PageSize = 500
        };
        
        return await GetTimeEntriesAsync(filter);
    }

    public async Task<List<TimeEntryDto>> GetMonthTimeEntriesAsync(DateTime month)
    {
        var monthStart = new DateTime(month.Year, month.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
        
        var filter = new TimeEntryFilterDto
        {
            StartDate = monthStart,
            EndDate = monthEnd,
            PageSize = 1000
        };
        
        return await GetTimeEntriesAsync(filter);
    }

    public async Task<TimeTrackingStatsDto> GetTimeStatsAsync(TimeStatsFilterDto filter)
    {
        try
        {
            var cacheKey = $"time-stats-{filter.GetHashCode()}";
            var cached = await _cacheService.GetAsync<TimeTrackingStatsDto>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = filter.StartDate,
                ["endDate"] = filter.EndDate
            };

            if (filter.UserId.HasValue) queryParams["userId"] = filter.UserId.Value;
            if (filter.ProjectId.HasValue) queryParams["projectId"] = filter.ProjectId.Value;
            if (filter.IncludeBillableOnly) queryParams["billableOnly"] = true;

            var stats = await _apiService.GetAsync<TimeTrackingStatsDto>("/api/timetracking/stats", queryParams);
            
            if (stats != null)
            {
                await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(10));
            }

            return stats ?? new TimeTrackingStatsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time stats");
            return new TimeTrackingStatsDto();
        }
    }

    public async Task<List<DailyTimeStatsDto>> GetDailyTimeStatsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var cacheKey = $"daily-time-stats-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
            var cached = await _cacheService.GetAsync<List<DailyTimeStatsDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var stats = await _apiService.GetAsync<List<DailyTimeStatsDto>>("/api/timetracking/stats/daily", queryParams);
            
            if (stats != null)
            {
                await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(15));
            }

            return stats ?? new List<DailyTimeStatsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily time stats");
            return new List<DailyTimeStatsDto>();
        }
    }

    public async Task<List<ProjectTimeStatsDto>> GetProjectTimeStatsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var stats = await _apiService.GetAsync<List<ProjectTimeStatsDto>>("/api/timetracking/stats/projects", queryParams);
            return stats ?? new List<ProjectTimeStatsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project time stats");
            return new List<ProjectTimeStatsDto>();
        }
    }

    public async Task<List<TaskTimeStatsDto>> GetTaskTimeStatsAsync(Guid projectId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var stats = await _apiService.GetAsync<List<TaskTimeStatsDto>>("/api/timetracking/stats/tasks", queryParams);
            return stats ?? new List<TaskTimeStatsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task time stats");
            return new List<TaskTimeStatsDto>();
        }
    }

    public async Task<ProductivityReportDto> GetProductivityReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var report = await _apiService.GetAsync<ProductivityReportDto>("/api/timetracking/productivity", queryParams);
            return report ?? new ProductivityReportDto { StartDate = startDate, EndDate = endDate };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity report");
            return new ProductivityReportDto { StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<List<ProductivityTrendDto>> GetProductivityTrendAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var trend = await _apiService.GetAsync<List<ProductivityTrendDto>>("/api/timetracking/productivity/trend", queryParams);
            return trend ?? new List<ProductivityTrendDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity trend");
            return new List<ProductivityTrendDto>();
        }
    }

    public async Task<WorkPatternAnalysisDto> AnalyzeWorkPatternsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var analysis = await _apiService.GetAsync<WorkPatternAnalysisDto>("/api/timetracking/patterns", queryParams);
            return analysis ?? new WorkPatternAnalysisDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing work patterns");
            return new WorkPatternAnalysisDto();
        }
    }

    public async Task<bool> PauseActiveTimerAsync()
    {
        try
        {
            if (_activeTimeEntry == null) return false;

            _logger.LogInformation("Pausing active timer: {TimeEntryId}", _activeTimeEntry.Id);
            
            var result = await _apiService.PatchAsync($"/api/timetracking/entries/{_activeTimeEntry.Id}/pause", new { });
            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing active timer");
            return false;
        }
    }

    public async Task<bool> ResumeActiveTimerAsync()
    {
        try
        {
            if (_activeTimeEntry == null) return false;

            _logger.LogInformation("Resuming active timer: {TimeEntryId}", _activeTimeEntry.Id);
            
            var result = await _apiService.PatchAsync($"/api/timetracking/entries/{_activeTimeEntry.Id}/resume", new { });
            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming active timer");
            return false;
        }
    }

    public async Task<bool> IsTimerActiveAsync()
    {
        var activeEntry = await GetActiveTimeEntryAsync();
        return activeEntry != null;
    }

    public async Task<TimeSpan> GetActiveTimerDurationAsync()
    {
        var activeEntry = await GetActiveTimeEntryAsync();
        if (activeEntry == null) return TimeSpan.Zero;

        return DateTime.UtcNow - activeEntry.StartTime;
    }

    public async Task<TimeGoalDto> CreateTimeGoalAsync(CreateTimeGoalRequestDto request)
    {
        try
        {
            var goal = await _apiService.PostAsync<TimeGoalDto>("/api/timetracking/goals", request);
            
            if (goal != null)
            {
                await _cacheService.RemoveByPatternAsync("time-goals-*");
                _logger.LogInformation("Time goal created: {GoalId}", goal.Id);
            }

            return goal ?? new TimeGoalDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating time goal");
            throw;
        }
    }

    public async Task<List<TimeGoalDto>> GetTimeGoalsAsync(bool activeOnly = true)
    {
        try
        {
            var cacheKey = $"time-goals-{activeOnly}";
            var cached = await _cacheService.GetAsync<List<TimeGoalDto>>(cacheKey);
            if (cached != null) return cached;

            var queryParams = activeOnly ? new Dictionary<string, object> { ["activeOnly"] = true } : null;
            var goals = await _apiService.GetAsync<List<TimeGoalDto>>("/api/timetracking/goals", queryParams);
            
            if (goals != null)
            {
                await _cacheService.SetAsync(cacheKey, goals, TimeSpan.FromMinutes(15));
            }

            return goals ?? new List<TimeGoalDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time goals");
            return new List<TimeGoalDto>();
        }
    }

    public async Task<TimeGoalProgressDto> GetTimeGoalProgressAsync(Guid goalId)
    {
        try
        {
            var progress = await _apiService.GetAsync<TimeGoalProgressDto>($"/api/timetracking/goals/{goalId}/progress");
            return progress ?? new TimeGoalProgressDto { GoalId = goalId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time goal progress: {GoalId}", goalId);
            return new TimeGoalProgressDto { GoalId = goalId };
        }
    }

    public async Task<bool> UpdateTimeGoalAsync(Guid goalId, TimeGoalDto goal)
    {
        try
        {
            await _apiService.PutAsync($"/api/timetracking/goals/{goalId}", goal);
            await _cacheService.RemoveByPatternAsync("time-goals-*");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating time goal: {GoalId}", goalId);
            return false;
        }
    }

    public async Task<bool> DeleteTimeGoalAsync(Guid goalId)
    {
        try
        {
            await _apiService.DeleteAsync($"/api/timetracking/goals/{goalId}");
            await _cacheService.RemoveByPatternAsync("time-goals-*");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting time goal: {GoalId}", goalId);
            return false;
        }
    }

    public async Task<TimesheetReportDto> GenerateTimesheetReportAsync(TimeReportFilterDto filter)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = filter.StartDate,
                ["endDate"] = filter.EndDate,
                ["groupBy"] = filter.GroupBy.ToString(),
                ["billableOnly"] = filter.BillableOnly,
                ["includeEmptyDays"] = filter.IncludeEmptyDays
            };

            if (filter.UserId.HasValue) queryParams["userId"] = filter.UserId.Value;
            if (filter.ProjectId.HasValue) queryParams["projectId"] = filter.ProjectId.Value;

            var report = await _apiService.GetAsync<TimesheetReportDto>("/api/timetracking/reports/timesheet", queryParams);
            return report ?? new TimesheetReportDto { StartDate = filter.StartDate, EndDate = filter.EndDate };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating timesheet report");
            return new TimesheetReportDto { StartDate = filter.StartDate, EndDate = filter.EndDate };
        }
    }

    public async Task<byte[]> ExportTimesheetAsync(TimeReportFilterDto filter, ExportFormat format)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = filter.StartDate,
                ["endDate"] = filter.EndDate,
                ["format"] = format.ToString(),
                ["billableOnly"] = filter.BillableOnly
            };

            if (filter.UserId.HasValue) queryParams["userId"] = filter.UserId.Value;
            if (filter.ProjectId.HasValue) queryParams["projectId"] = filter.ProjectId.Value;

            var data = await _apiService.GetAsync<byte[]>("/api/timetracking/reports/export", queryParams);
            return data ?? Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting timesheet");
            return Array.Empty<byte>();
        }
    }

    public async Task<ProjectTimeReportDto> GenerateProjectTimeReportAsync(Guid projectId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var queryParams = new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            };

            var report = await _apiService.GetAsync<ProjectTimeReportDto>($"/api/timetracking/reports/project/{projectId}", queryParams);
            return report ?? new ProjectTimeReportDto { ProjectId = projectId, StartDate = startDate, EndDate = endDate };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating project time report: {ProjectId}", projectId);
            return new ProjectTimeReportDto { ProjectId = projectId, StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<TimeTrackingSettingsDto> GetSettingsAsync()
    {
        try
        {
            if (_settings != null) return _settings;

            var settings = await _apiService.GetAsync<TimeTrackingSettingsDto>("/api/timetracking/settings");
            _settings = settings ?? CreateDefaultSettings();
            
            return _settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time tracking settings");
            return _settings ?? CreateDefaultSettings();
        }
    }

    public async Task<TimeTrackingSettingsDto> UpdateSettingsAsync(TimeTrackingSettingsDto settings)
    {
        try
        {
            var updatedSettings = await _apiService.PutAsync<TimeTrackingSettingsDto>("/api/timetracking/settings", settings);
            _settings = updatedSettings ?? settings;
            
            // Update timer intervals based on new settings
            if (_settings.RemindToTrackTime)
            {
                _reminderTimer.Interval = _settings.ReminderInterval.TotalMilliseconds;
            }
            
            return _settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating time tracking settings");
            throw;
        }
    }

    public async Task<bool> SetTimeTrackingReminderAsync(TimeSpan interval)
    {
        try
        {
            _reminderTimer.Interval = interval.TotalMilliseconds;
            _logger.LogInformation("Time tracking reminder interval set to: {Interval}", interval);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting time tracking reminder");
            return false;
        }
    }

    public async Task<bool> EnableIdleTimeDetectionAsync(bool enabled)
    {
        try
        {
            if (enabled)
            {
                _idleTimer.Start();
            }
            else
            {
                _idleTimer.Stop();
            }
            
            _logger.LogInformation("Idle time detection: {Enabled}", enabled ? "Enabled" : "Disabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting idle time detection");
            return false;
        }
    }

    public async Task<List<IdleTimeDetectionDto>> GetIdleTimePeriodsAsync(DateTime date)
    {
        try
        {
            var queryParams = new Dictionary<string, object> { ["date"] = date };
            var periods = await _apiService.GetAsync<List<IdleTimeDetectionDto>>("/api/timetracking/idle", queryParams);
            return periods ?? new List<IdleTimeDetectionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting idle time periods");
            return new List<IdleTimeDetectionDto>();
        }
    }

    // Event handlers
    protected virtual void OnTimeEntryStarted(TimeEntryStartedEventArgs e)
    {
        TimeEntryStarted?.Invoke(this, e);
    }

    protected virtual void OnTimeEntryStopped(TimeEntryStoppedEventArgs e)
    {
        TimeEntryStopped?.Invoke(this, e);
    }

    protected virtual void OnTimeEntryUpdated(TimeEntryUpdatedEventArgs e)
    {
        TimeEntryUpdated?.Invoke(this, e);
    }

    protected virtual void OnTimeGoalAchieved(TimeGoalAchievedEventArgs e)
    {
        TimeGoalAchieved?.Invoke(this, e);
    }

    protected virtual void OnIdleTimeDetected(IdleTimeDetectedEventArgs e)
    {
        IdleTimeDetected?.Invoke(this, e);
    }

    // Private methods
    private async void OnIdleTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_settings?.DetectIdleTime != true || _activeTimeEntry == null) return;

        var idleTime = DateTime.UtcNow - _lastActivityTime;
        if (idleTime >= _settings.IdleTimeThreshold)
        {
            var idleDetection = new IdleTimeDetectionDto
            {
                StartTime = _lastActivityTime,
                EndTime = DateTime.UtcNow,
                Duration = idleTime,
                ActiveTimeEntryId = _activeTimeEntry.Id,
                DetectedAt = DateTime.UtcNow
            };

            OnIdleTimeDetected(new IdleTimeDetectedEventArgs { IdleTime = idleDetection });
        }
    }

    private async void OnReminderTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_settings?.RemindToTrackTime != true) return;

        var isActive = await IsTimerActiveAsync();
        if (!isActive)
        {
            // Send reminder notification (implementation would depend on notification system)
            _logger.LogInformation("Time tracking reminder triggered");
        }
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await GetSettingsAsync();
    }

    private TimeTrackingSettingsDto CreateDefaultSettings()
    {
        var currentUser = _currentUserService.GetCurrentUserAsync().Result;
        return new TimeTrackingSettingsDto
        {
            UserId = currentUser?.Id ?? Guid.Empty,
            AutoStartTimer = false,
            RemindToTrackTime = true,
            ReminderInterval = TimeSpan.FromMinutes(30),
            DetectIdleTime = true,
            IdleTimeThreshold = TimeSpan.FromMinutes(5),
            RoundTimeEntries = false,
            RoundingMode = TimeRoundingMode.Nearest15Minutes,
            RequireDescription = false,
            DefaultToBillable = true,
            DefaultGoalPeriod = TimeGoalPeriod.Weekly,
            DefaultDailyGoal = TimeSpan.FromHours(8)
        };
    }

    private TimeSpan RoundTimeEntry(TimeSpan duration, TimeRoundingMode mode)
    {
        return mode switch
        {
            TimeRoundingMode.Nearest5Minutes => TimeSpan.FromMinutes(Math.Round(duration.TotalMinutes / 5) * 5),
            TimeRoundingMode.Nearest15Minutes => TimeSpan.FromMinutes(Math.Round(duration.TotalMinutes / 15) * 15),
            TimeRoundingMode.Nearest30Minutes => TimeSpan.FromMinutes(Math.Round(duration.TotalMinutes / 30) * 30),
            TimeRoundingMode.NearestHour => TimeSpan.FromHours(Math.Round(duration.TotalHours)),
            TimeRoundingMode.Up5Minutes => TimeSpan.FromMinutes(Math.Ceiling(duration.TotalMinutes / 5) * 5),
            TimeRoundingMode.Up15Minutes => TimeSpan.FromMinutes(Math.Ceiling(duration.TotalMinutes / 15) * 15),
            TimeRoundingMode.Up30Minutes => TimeSpan.FromMinutes(Math.Ceiling(duration.TotalMinutes / 30) * 30),
            TimeRoundingMode.UpHour => TimeSpan.FromHours(Math.Ceiling(duration.TotalHours)),
            _ => duration
        };
    }

    private async Task CheckTimeGoalsAsync()
    {
        try
        {
            var goals = await GetTimeGoalsAsync(true);
            foreach (var goal in goals)
            {
                var progress = await GetTimeGoalProgressAsync(goal.Id);
                if (progress.IsAchieved && progress.AchievedAt == null)
                {
                    OnTimeGoalAchieved(new TimeGoalAchievedEventArgs { Goal = goal, Progress = progress });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking time goals");
        }
    }

    public void Dispose()
    {
        _idleTimer?.Dispose();
        _reminderTimer?.Dispose();
    }
}