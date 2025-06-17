using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
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
                LoadNotificationsAsync()
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
            // TODO: Replace with actual API calls
            await Task.Delay(1000); // Simulate API call

            // Simulate loading stats
            ActiveProjectsCount = "5";
            ProjectsTrendIcon = "📈";
            ProjectsTrendText = "+2 this month";

            var overdueCount = Random.Shared.Next(0, 8);
            OverdueTasksCount = overdueCount.ToString();
            OverdueTasksCardColor = overdueCount > 0 ? Colors.Red : Colors.Orange;

            CompletedTasksThisWeek = Random.Shared.Next(8, 25).ToString();
            UpcomingDeadlinesCount = Random.Shared.Next(2, 12).ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quick stats");
        }
    }

    private async Task LoadRecentActivitiesAsync()
    {
        try
        {
            // TODO: Replace with actual API call
            await Task.Delay(500); // Simulate API call

            var activities = new List<ActivityItem>
            {
                new() 
                { 
                    UserInitials = "JD", 
                    Description = "Completed task 'Update user interface'", 
                    ProjectName = "Mobile App", 
                    TimeAgo = "2 hours ago", 
                    ActivityIcon = "✅" 
                },
                new() 
                { 
                    UserInitials = "SM", 
                    Description = "Created new project milestone", 
                    ProjectName = "Web Portal", 
                    TimeAgo = "4 hours ago", 
                    ActivityIcon = "🎯" 
                },
                new() 
                { 
                    UserInitials = "AK", 
                    Description = "Uploaded design assets", 
                    ProjectName = "Mobile App", 
                    TimeAgo = "6 hours ago", 
                    ActivityIcon = "📁" 
                },
                new() 
                { 
                    UserInitials = "TR", 
                    Description = "Started working on 'API Integration'", 
                    ProjectName = "Backend Service", 
                    TimeAgo = "8 hours ago", 
                    ActivityIcon = "🔧" 
                },
                new() 
                { 
                    UserInitials = "ML", 
                    Description = "Added comment to task discussion", 
                    ProjectName = "Web Portal", 
                    TimeAgo = "1 day ago", 
                    ActivityIcon = "💬" 
                }
            };

            RecentActivities.Clear();
            foreach (var activity in activities)
            {
                RecentActivities.Add(activity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent activities");
        }
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