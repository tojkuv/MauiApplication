using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Dashboard;

public partial class ActivityViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IApiService _apiService;
    private readonly ILogger<ActivityViewModel> _logger;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string activitySummary = "Loading activities...";

    [ObservableProperty]
    private string currentFilter = "all";

    [ObservableProperty]
    private bool hasMoreActivities = true;

    [ObservableProperty]
    private ObservableCollection<ActivityDetailItem> allActivities = new();

    [ObservableProperty]
    private ObservableCollection<ActivityDetailItem> filteredActivities = new();

    // Filter chip colors
    [ObservableProperty]
    private Color allActivitiesChipColor = Colors.Gray;

    [ObservableProperty]
    private Color tasksChipColor = Colors.Gray;

    [ObservableProperty]
    private Color projectsChipColor = Colors.Gray;

    [ObservableProperty]
    private Color filesChipColor = Colors.Gray;

    [ObservableProperty]
    private Color collaborationChipColor = Colors.Gray;

    private int _currentPage = 1;
    private const int PageSize = 20;

    public ActivityViewModel(
        INavigationService navigationService,
        IApiService apiService,
        ILogger<ActivityViewModel> logger)
    {
        _navigationService = navigationService;
        _apiService = apiService;
        _logger = logger;

        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task LoadActivities()
    {
        try
        {
            IsRefreshing = true;
            _logger.LogInformation("Loading activity feed");

            // Reset pagination
            _currentPage = 1;
            
            // TODO: Replace with actual API call
            await Task.Delay(1000); // Simulate API call

            var activities = await GenerateSampleActivitiesAsync(PageSize);
            
            AllActivities.Clear();
            foreach (var activity in activities)
            {
                AllActivities.Add(activity);
            }

            ApplyCurrentFilter();
            UpdateActivitySummary();
            HasMoreActivities = activities.Count == PageSize;

            _logger.LogInformation($"Loaded {AllActivities.Count} activities");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activities");
            await Shell.Current.DisplayAlert("Error", "Unable to load activities. Please try again.", "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        try
        {
            if (!HasMoreActivities) return;

            _logger.LogInformation("Loading more activities");
            
            _currentPage++;
            
            // TODO: Replace with actual API call
            await Task.Delay(500); // Simulate API call

            var moreActivities = await GenerateSampleActivitiesAsync(PageSize, _currentPage);
            
            foreach (var activity in moreActivities)
            {
                AllActivities.Add(activity);
            }

            ApplyCurrentFilter();
            UpdateActivitySummary();
            HasMoreActivities = moreActivities.Count == PageSize;

            _logger.LogInformation($"Loaded {moreActivities.Count} more activities");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading more activities");
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadActivitiesAsync();
    }

    [RelayCommand]
    private async Task ShowFilterOptions()
    {
        var action = await Shell.Current.DisplayActionSheet(
            "Filter Activities",
            "Cancel",
            null,
            "All Activities",
            "Tasks Only",
            "Projects Only",
            "Files Only",
            "Chat Messages",
            "Today's Activity",
            "This Week");

        switch (action)
        {
            case "All Activities":
                await FilterAllActivitiesAsync();
                break;
            case "Tasks Only":
                await FilterTaskActivitiesAsync();
                break;
            case "Projects Only":
                await FilterProjectActivitiesAsync();
                break;
            case "Files Only":
                await FilterFileActivitiesAsync();
                break;
            case "Chat Messages":
                await FilterChatActivitiesAsync();
                break;
            case "Today's Activity":
                CurrentFilter = "today";
                ApplyCurrentFilter();
                break;
            case "This Week":
                CurrentFilter = "week";
                ApplyCurrentFilter();
                break;
        }
    }

    [RelayCommand]
    private async Task FilterAllActivities()
    {
        CurrentFilter = "all";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterTaskActivities()
    {
        CurrentFilter = "tasks";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterProjectActivities()
    {
        CurrentFilter = "projects";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterFileActivities()
    {
        CurrentFilter = "files";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterChatActivities()
    {
        CurrentFilter = "chat";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task ViewActivityDetails(ActivityDetailItem activity)
    {
        try
        {
            if (activity == null) return;

            // Navigate based on activity type
            switch (activity.ActivityType)
            {
                case "task":
                    await _navigationService.NavigateToAsync($"tasks/detail?taskId={activity.RelatedId}");
                    break;
                case "project":
                    await _navigationService.NavigateToAsync($"projects/detail?projectId={activity.RelatedId}");
                    break;
                case "file":
                    await _navigationService.NavigateToAsync($"files/detail?fileId={activity.RelatedId}");
                    break;
                case "chat":
                    await _navigationService.NavigateToAsync($"collaboration/chat?channelId={activity.RelatedId}");
                    break;
                default:
                    await Shell.Current.DisplayAlert("Activity", activity.Description, "OK");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing activity details");
        }
    }

    private void ApplyCurrentFilter()
    {
        FilteredActivities.Clear();

        var filtered = CurrentFilter switch
        {
            "tasks" => AllActivities.Where(a => a.ActivityType == "task"),
            "projects" => AllActivities.Where(a => a.ActivityType == "project"),
            "files" => AllActivities.Where(a => a.ActivityType == "file"),
            "chat" => AllActivities.Where(a => a.ActivityType == "chat"),
            "today" => AllActivities.Where(a => a.ActivityTime.Date == DateTime.Today),
            "week" => AllActivities.Where(a => a.ActivityTime >= DateTime.Today.AddDays(-7)),
            _ => AllActivities
        };

        foreach (var activity in filtered.OrderByDescending(a => a.ActivityTime))
        {
            FilteredActivities.Add(activity);
        }
    }

    private void UpdateFilterColors()
    {
        // Reset all colors
        AllActivitiesChipColor = Colors.Gray;
        TasksChipColor = Colors.Gray;
        ProjectsChipColor = Colors.Gray;
        FilesChipColor = Colors.Gray;
        CollaborationChipColor = Colors.Gray;

        // Set active filter color
        var activeColor = Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#2196F3") : Color.FromArgb("#1976D2");

        switch (CurrentFilter)
        {
            case "all":
                AllActivitiesChipColor = activeColor;
                break;
            case "tasks":
                TasksChipColor = activeColor;
                break;
            case "projects":
                ProjectsChipColor = activeColor;
                break;
            case "files":
                FilesChipColor = activeColor;
                break;
            case "chat":
                CollaborationChipColor = activeColor;
                break;
        }
    }

    private void UpdateActivitySummary()
    {
        var totalActivities = AllActivities.Count;
        var todayActivities = AllActivities.Count(a => a.ActivityTime.Date == DateTime.Today);
        var weekActivities = AllActivities.Count(a => a.ActivityTime >= DateTime.Today.AddDays(-7));

        ActivitySummary = $"{totalActivities} total • {todayActivities} today • {weekActivities} this week";
    }

    private async Task<List<ActivityDetailItem>> GenerateSampleActivitiesAsync(int count, int page = 1)
    {
        await Task.Delay(100); // Simulate API delay

        var activities = new List<ActivityDetailItem>();
        var random = new Random();
        
        var users = new[]
        {
            new { Name = "John Doe", Initials = "JD", Color = Colors.Blue },
            new { Name = "Sarah Miller", Initials = "SM", Color = Colors.Green },
            new { Name = "Alex Kim", Initials = "AK", Color = Colors.Purple },
            new { Name = "Tom Rodriguez", Initials = "TR", Color = Colors.Orange },
            new { Name = "Maria Lopez", Initials = "ML", Color = Colors.Red },
            new { Name = "David Chen", Initials = "DC", Color = Colors.Teal },
            new { Name = "Emma Wilson", Initials = "EW", Color = Colors.Pink }
        };

        var projects = new[]
        {
            new { Name = "Mobile App", Color = Colors.Blue },
            new { Name = "Web Portal", Color = Colors.Green },
            new { Name = "Backend API", Color = Colors.Purple },
            new { Name = "Dashboard", Color = Colors.Orange },
            new { Name = "Analytics", Color = Colors.Red }
        };

        var activityTemplates = new[]
        {
            new { Type = "task", Icon = "✅", Action = "completed", Objects = new[] { "task", "bug fix", "feature", "review" } },
            new { Type = "task", Icon = "🔧", Action = "started working on", Objects = new[] { "task", "bug fix", "feature", "review" } },
            new { Type = "project", Icon = "🎯", Action = "created", Objects = new[] { "milestone", "project", "sprint" } },
            new { Type = "project", Icon = "📊", Action = "updated", Objects = new[] { "project status", "timeline", "budget" } },
            new { Type = "file", Icon = "📁", Action = "uploaded", Objects = new[] { "design assets", "documents", "images", "presentations" } },
            new { Type = "file", Icon = "✏️", Action = "edited", Objects = new[] { "document", "spreadsheet", "presentation" } },
            new { Type = "chat", Icon = "💬", Action = "commented on", Objects = new[] { "task discussion", "project update", "file review" } },
            new { Type = "chat", Icon = "📞", Action = "joined", Objects = new[] { "meeting", "call", "standup" } }
        };

        for (int i = 0; i < count; i++)
        {
            var user = users[random.Next(users.Length)];
            var project = projects[random.Next(projects.Length)];
            var template = activityTemplates[random.Next(activityTemplates.Length)];
            var obj = template.Objects[random.Next(template.Objects.Length)];

            var hoursAgo = random.Next(1, 168); // Up to 7 days ago
            var activityTime = DateTime.Now.AddHours(-hoursAgo);

            var activity = new ActivityDetailItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                UserName = user.Name,
                UserInitials = user.Initials,
                UserAvatarBackgroundColor = user.Color,
                ActivityType = template.Type,
                ActivityIcon = template.Icon,
                ActivityTime = activityTime,
                Description = $"{template.Action} {obj}",
                ProjectName = project.Name,
                ProjectColor = project.Color,
                RelatedId = Guid.NewGuid(),
                HasProject = true,
                HasContextInfo = random.Next(2) == 0,
                ContextInfo = random.Next(2) == 0 ? "High priority" : "Due soon",
                HasActions = template.Type != "chat"
            };

            activity.TimeAgo = GetTimeAgoText(activityTime);
            activities.Add(activity);
        }

        return activities.OrderByDescending(a => a.ActivityTime).ToList();
    }

    private string GetTimeAgoText(DateTime activityTime)
    {
        var timeSpan = DateTime.Now - activityTime;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)}w ago";

        return activityTime.ToString("MMM dd");
    }
}

public class ActivityDetailItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserInitials { get; set; } = string.Empty;
    public Color UserAvatarBackgroundColor { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string ActivityIcon { get; set; } = string.Empty;
    public DateTime ActivityTime { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public Color ProjectColor { get; set; }
    public Guid RelatedId { get; set; }
    public bool HasProject { get; set; }
    public bool HasContextInfo { get; set; }
    public string ContextInfo { get; set; } = string.Empty;
    public bool HasActions { get; set; }
}