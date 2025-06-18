using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using MauiApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly IApiService _apiService;
    private readonly ILogger<DashboardViewModel> _logger;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string welcomeMessage = "Welcome back!";

    [ObservableProperty]
    private string currentDateTime = DateTime.Now.ToString("MMMM dd, yyyy");

    [ObservableProperty]
    private int notificationCount;

    [ObservableProperty]
    private bool hasNotifications;

    // Quick Stats Properties
    [ObservableProperty]
    private string activeProjectsCount = "0";

    [ObservableProperty]
    private string projectsTrendIcon = "📈";

    [ObservableProperty]
    private string projectsTrendText = "No change";

    [ObservableProperty]
    private string overdueTasksCount = "0";

    [ObservableProperty]
    private Color overdueTasksCardColor = Colors.Orange;

    [ObservableProperty]
    private string completedTasksThisWeek = "0";

    [ObservableProperty]
    private string upcomingDeadlinesCount = "0";

    [ObservableProperty]
    private ObservableCollection<ActivityItem> recentActivities = new();

    // Enhanced Dashboard Properties
    [ObservableProperty]
    private string totalProjectsCount = "0";

    [ObservableProperty]
    private string myActiveTasksCount = "0";

    [ObservableProperty]
    private string hoursLoggedThisWeek = "0.0";

    [ObservableProperty]
    private string teamMessagesCount = "0";

    [ObservableProperty]
    private string filesUploadedCount = "0";

    [ObservableProperty]
    private double productivityScore = 0.0;

    [ObservableProperty]
    private string productivityTrend = "stable";

    [ObservableProperty]
    private double completionRate = 0.0;

    [ObservableProperty]
    private ObservableCollection<UpcomingDeadline> upcomingDeadlines = new();

    [ObservableProperty]
    private ObservableCollection<ChartData> performanceCharts = new();

    [ObservableProperty]
    private string lastSyncTime = "Never";

    [ObservableProperty]
    private bool isOfflineMode = false;

    public DashboardViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        IApiService apiService,
        ILogger<DashboardViewModel> logger)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _apiService = apiService;
        _logger = logger;

        // Initialize timer for date/time updates
        StartDateTimeTimer();
        
        // Load initial data
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadData()
    {
        try
        {
            IsRefreshing = true;
            _logger.LogInformation("Loading dashboard data");

            await Task.WhenAll(
                LoadUserWelcomeMessageAsync(),
                LoadQuickStatsAsync(),
                LoadRecentActivitiesAsync(),
                LoadUpcomingDeadlinesAsync(),
                LoadPerformanceChartsAsync(),
                LoadNotificationsAsync(),
                CheckSyncStatusAsync()
            );

            _logger.LogInformation("Dashboard data loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            await Shell.Current.DisplayAlert("Error", "Unable to load dashboard data. Please try again.", "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        try
        {
            var searchQuery = await Shell.Current.DisplayPromptAsync(
                "Search", 
                "What are you looking for?", 
                "Search", 
                "Cancel", 
                "Projects, tasks, files...");

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                // TODO: Implement search functionality
                await Shell.Current.DisplayAlert("Search", $"Searching for: {searchQuery}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search");
        }
    }

    [RelayCommand]
    private async Task Notifications()
    {
        try
        {
            // TODO: Navigate to notifications page
            await Shell.Current.DisplayAlert("Notifications", "You have no new notifications", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening notifications");
        }
    }

    [RelayCommand]
    private async Task NavigateToProjects()
    {
        try
        {
            await _navigationService.NavigateToAsync("projects/list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to projects");
        }
    }

    [RelayCommand]
    private async Task NavigateToOverdueTasks()
    {
        try
        {
            await _navigationService.NavigateToAsync("tasks/list?filter=overdue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to overdue tasks");
        }
    }

    [RelayCommand]
    private async Task NavigateToMyTasks()
    {
        try
        {
            await _navigationService.NavigateToAsync("dashboard/mytasks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to my tasks");
        }
    }

    [RelayCommand]
    private async Task NavigateToCalendar()
    {
        try
        {
            // TODO: Navigate to calendar view
            await Shell.Current.DisplayAlert("Calendar", "Calendar view coming soon!", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to calendar");
        }
    }

    [RelayCommand]
    private async Task ViewAllActivities()
    {
        try
        {
            await _navigationService.NavigateToAsync("dashboard/activity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to activities");
        }
    }

    [RelayCommand]
    private async Task ShowQuickActions()
    {
        try
        {
            var action = await Shell.Current.DisplayActionSheet(
                "Quick Actions",
                "Cancel",
                null,
                "Create Project",
                "Add Task",
                "Start Timer",
                "Send Message",
                "Upload File");

            switch (action)
            {
                case "Create Project":
                    await _navigationService.NavigateToAsync("projects/create");
                    break;
                case "Add Task":
                    await _navigationService.NavigateToAsync("tasks/create");
                    break;
                case "Start Timer":
                    await _navigationService.NavigateToAsync("timetracking/timer");
                    break;
                case "Send Message":
                    await _navigationService.NavigateToAsync("collaboration/chat");
                    break;
                case "Upload File":
                    await _navigationService.NavigateToAsync("files/browser");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing quick actions");
        }
    }

    private async Task LoadUserWelcomeMessageAsync()
    {
        try
        {
            var userInfo = await _authenticationService.GetCurrentUserAsync();
            
            if (userInfo != null)
            {
                var firstName = !string.IsNullOrEmpty(userInfo.FirstName) 
                    ? userInfo.FirstName 
                    : userInfo.Name?.Split(' ').FirstOrDefault() ?? "User";
                
                var timeOfDay = DateTime.Now.Hour switch
                {
                    < 12 => "Good morning",
                    < 17 => "Good afternoon",
                    _ => "Good evening"
                };

                WelcomeMessage = $"{timeOfDay}, {firstName}!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user welcome message");
            WelcomeMessage = "Welcome back!";
        }
    }

    private async Task LoadQuickStatsAsync()
    {
        try
        {
            _logger.LogInformation("Loading comprehensive dashboard statistics");

            // Load comprehensive stats from Analytics Service
            var quickStats = await _apiService.GetAsync<QuickStats>("/api/analytics/users/me/quick-stats");
            var projectStats = await _apiService.GetAsync<ProjectStatsDto>("/api/projects/stats");
            var productivity = await _apiService.GetAsync<UserProductivityDto>(
                $"/api/analytics/users/me/productivity?startDate={DateTime.Now.AddDays(-30):yyyy-MM-dd}&endDate={DateTime.Now:yyyy-MM-dd}");

            if (quickStats != null)
            {
                MyActiveTasksCount = quickStats.MyActiveTasks.ToString();
                OverdueTasksCount = quickStats.MyOverdueTasks.ToString();
                CompletedTasksThisWeek = quickStats.MyCompletedThisWeek.ToString();
                HoursLoggedThisWeek = quickStats.MyHoursThisWeek.ToString("F1");
                TeamMessagesCount = quickStats.TeamMessages.ToString();
                UpcomingDeadlinesCount = quickStats.UpcomingDeadlines.ToString();

                // Set overdue card color based on count
                OverdueTasksCardColor = quickStats.MyOverdueTasks > 0 ? Colors.Red : Colors.Green;
            }

            if (projectStats != null)
            {
                TotalProjectsCount = projectStats.TotalProjects.ToString();
                ActiveProjectsCount = projectStats.ActiveProjects.ToString();
                
                // Calculate trend
                var monthlyIncrease = projectStats.ActiveProjects - projectStats.CompletedProjects;
                if (monthlyIncrease > 0)
                {
                    ProjectsTrendIcon = "📈";
                    ProjectsTrendText = $"+{monthlyIncrease} this month";
                }
                else if (monthlyIncrease < 0)
                {
                    ProjectsTrendIcon = "📉";
                    ProjectsTrendText = $"{monthlyIncrease} this month";
                }
                else
                {
                    ProjectsTrendIcon = "➖";
                    ProjectsTrendText = "No change";
                }
            }

            if (productivity != null)
            {
                ProductivityScore = productivity.ProductivityScore;
                CompletionRate = (double)productivity.TasksCompleted / 
                    Math.Max(1, productivity.TasksCompleted + productivity.TasksInProgress + productivity.TasksOverdue) * 100;
                
                // Determine productivity trend
                ProductivityTrend = productivity.ProductivityScore switch
                {
                    >= 80 => "excellent",
                    >= 60 => "good", 
                    >= 40 => "average",
                    _ => "needs improvement"
                };
            }

            // Load file statistics
            try
            {
                var fileStats = await _apiService.GetAsync<dynamic>("/api/files/users/me/stats");
                if (fileStats != null)
                {
                    FilesUploadedCount = fileStats.GetProperty("totalFiles").GetInt32().ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load file statistics");
                FilesUploadedCount = "N/A";
            }

            _logger.LogInformation("Dashboard statistics loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comprehensive dashboard statistics");
            
            // Fallback to basic stats
            ActiveProjectsCount = "--";
            MyActiveTasksCount = "--";
            OverdueTasksCount = "--";
            CompletedTasksThisWeek = "--";
            HoursLoggedThisWeek = "--";
            TeamMessagesCount = "--";
            FilesUploadedCount = "--";
            UpcomingDeadlinesCount = "--";
        }
    }

    private async Task LoadRecentActivitiesAsync()
    {
        try
        {
            _logger.LogInformation("Loading recent activities from Analytics Service");
            
            var activities = await _apiService.GetAsync<List<RecentActivity>>("/api/analytics/users/me/recent-activities?count=10");
            
            RecentActivities.Clear();
            
            if (activities != null && activities.Any())
            {
                foreach (var activity in activities)
                {
                    var activityItem = new ActivityItem
                    {
                        UserInitials = GetInitials(activity.UserName),
                        Description = activity.Description,
                        ProjectName = GetProjectNameFromEntityId(activity.EntityId),
                        TimeAgo = GetTimeAgo(activity.Timestamp),
                        ActivityIcon = GetActivityIcon(activity.Type)
                    };
                    
                    RecentActivities.Add(activityItem);
                }
            }
            else
            {
                // Add a placeholder if no activities
                RecentActivities.Add(new ActivityItem
                {
                    UserInitials = "ℹ️",
                    Description = "No recent activities found",
                    ProjectName = "Start working to see activities here",
                    TimeAgo = "Just now",
                    ActivityIcon = "📝"
                });
            }

            _logger.LogInformation("Loaded {Count} recent activities", RecentActivities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent activities from API");
            
            // Fallback to placeholder message
            RecentActivities.Clear();
            RecentActivities.Add(new ActivityItem
            {
                UserInitials = "⚠️",
                Description = "Unable to load recent activities",
                ProjectName = "Check your connection",
                TimeAgo = "Just now",
                ActivityIcon = "🔄"
            });
        }
    }

    private async Task LoadUpcomingDeadlinesAsync()
    {
        try
        {
            _logger.LogInformation("Loading upcoming deadlines");
            
            var deadlines = await _apiService.GetAsync<List<UpcomingDeadline>>("/api/analytics/users/me/upcoming-deadlines?days=14");
            
            UpcomingDeadlines.Clear();
            
            if (deadlines != null && deadlines.Any())
            {
                foreach (var deadline in deadlines.Take(5)) // Show only top 5
                {
                    UpcomingDeadlines.Add(deadline);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading upcoming deadlines");
        }
    }

    private async Task LoadPerformanceChartsAsync()
    {
        try
        {
            _logger.LogInformation("Loading performance charts data");
            
            // Task completion trend chart
            var completionChart = new ChartData
            {
                Type = "line",
                Title = "Task Completion Trend (Last 30 Days)",
                Labels = Enumerable.Range(0, 30)
                    .Select(i => DateTime.Now.AddDays(-29 + i).ToString("MM/dd"))
                    .ToList(),
                Datasets = new List<ChartDataset>
                {
                    new()
                    {
                        Label = "Tasks Completed",
                        Data = GenerateRandomData(30, 0, 8), // TODO: Replace with real data
                        BackgroundColor = "rgba(54, 162, 235, 0.2)",
                        BorderColor = "rgba(54, 162, 235, 1)"
                    }
                }
            };

            // Productivity score pie chart
            var productivityChart = new ChartData
            {
                Type = "doughnut",
                Title = "Current Productivity Breakdown",
                Labels = new List<string> { "Completed", "In Progress", "Pending", "Overdue" },
                Datasets = new List<ChartDataset>
                {
                    new()
                    {
                        Label = "Tasks",
                        Data = new List<double> { CompletionRate, 25, 15, double.Parse(OverdueTasksCount) },
                        BackgroundColor = "rgba(75, 192, 192, 0.6)",
                        BorderColor = "rgba(75, 192, 192, 1)"
                    }
                }
            };

            PerformanceCharts.Clear();
            PerformanceCharts.Add(completionChart);
            PerformanceCharts.Add(productivityChart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading performance charts");
        }
    }

    private async Task CheckSyncStatusAsync()
    {
        try
        {
            var syncStatus = await _apiService.GetAsync<dynamic>("/api/sync/status");
            if (syncStatus != null)
            {
                var lastSync = syncStatus.GetProperty("lastSyncTime").GetDateTime();
                LastSyncTime = GetTimeAgo(lastSync);
                IsOfflineMode = !syncStatus.GetProperty("isOnline").GetBoolean();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check sync status");
            LastSyncTime = "Unknown";
            IsOfflineMode = true;
        }
    }

    private string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "??";
        
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        
        return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
    }

    private string GetProjectNameFromEntityId(Guid entityId)
    {
        // TODO: Implement project name lookup
        return "Project";
    }

    private string GetTimeAgo(DateTime timestamp)
    {
        var timeSpan = DateTime.Now - timestamp;
        
        return timeSpan.TotalDays switch
        {
            >= 365 => $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago",
            >= 30 => $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago",
            >= 7 => $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) != 1 ? "s" : "")} ago",
            >= 1 => $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago",
            >= 1.0/24 => $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago",
            >= 1.0/(24*60) => $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago",
            _ => "Just now"
        };
    }

    private string GetActivityIcon(string activityType)
    {
        return activityType.ToLower() switch
        {
            "task_completed" => "✅",
            "task_created" => "📝",
            "task_updated" => "✏️",
            "project_created" => "🚀",
            "milestone_completed" => "🎯",
            "file_uploaded" => "📁",
            "comment_added" => "💬",
            "time_logged" => "⏰",
            "member_added" => "👥",
            _ => "🔔"
        };
    }

    private List<double> GenerateRandomData(int count, double min, double max)
    {
        var random = new Random();
        return Enumerable.Range(0, count)
            .Select(_ => min + (random.NextDouble() * (max - min)))
            .ToList();
    }

    private async Task LoadNotificationsAsync()
    {
        try
        {
            // TODO: Replace with actual API call
            await Task.Delay(300); // Simulate API call

            NotificationCount = Random.Shared.Next(0, 15);
            HasNotifications = NotificationCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notifications");
            NotificationCount = 0;
            HasNotifications = false;
        }
    }

    private void StartDateTimeTimer()
    {
        var timer = new System.Timers.Timer(TimeSpan.FromMinutes(1));
        timer.Elapsed += (s, e) =>
        {
            CurrentDateTime = DateTime.Now.ToString("MMMM dd, yyyy");
        };
        timer.Start();
    }
}

public class ActivityItem
{
    public string UserInitials { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string ActivityIcon { get; set; } = string.Empty;
}