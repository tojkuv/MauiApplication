using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Dashboard;

public partial class MyTasksViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IApiService _apiService;
    private readonly ILogger<MyTasksViewModel> _logger;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string tasksSummary = "Loading tasks...";

    [ObservableProperty]
    private string currentFilter = "all";

    [ObservableProperty]
    private ObservableCollection<TaskItem> allTasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskItem> filteredTasks = new();

    // Filter chip colors
    [ObservableProperty]
    private Color allTasksChipColor = Colors.Gray;

    [ObservableProperty]
    private Color inProgressChipColor = Colors.Gray;

    [ObservableProperty]
    private Color overdueChipColor = Colors.Gray;

    [ObservableProperty]
    private Color completedChipColor = Colors.Gray;

    public MyTasksViewModel(
        INavigationService navigationService,
        IApiService apiService,
        ILogger<MyTasksViewModel> logger)
    {
        _navigationService = navigationService;
        _apiService = apiService;
        _logger = logger;

        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task LoadTasks()
    {
        try
        {
            IsRefreshing = true;
            _logger.LogInformation("Loading user tasks");

            // TODO: Replace with actual API call
            await Task.Delay(1000); // Simulate API call

            var tasks = GenerateSampleTasks();
            
            AllTasks.Clear();
            foreach (var task in tasks)
            {
                AllTasks.Add(task);
            }

            ApplyCurrentFilter();
            UpdateTasksSummary();

            _logger.LogInformation($"Loaded {AllTasks.Count} tasks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tasks");
            await Shell.Current.DisplayAlert("Error", "Unable to load tasks. Please try again.", "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task ShowFilter()
    {
        var action = await Shell.Current.DisplayActionSheet(
            "Filter Tasks",
            "Cancel",
            null,
            "All Tasks",
            "In Progress",
            "Overdue",
            "Completed",
            "High Priority",
            "Due Today");

        switch (action)
        {
            case "All Tasks":
                await FilterAllTasksAsync();
                break;
            case "In Progress":
                await FilterInProgressAsync();
                break;
            case "Overdue":
                await FilterOverdueAsync();
                break;
            case "Completed":
                await FilterCompletedAsync();
                break;
            case "High Priority":
                CurrentFilter = "high-priority";
                ApplyCurrentFilter();
                break;
            case "Due Today":
                CurrentFilter = "due-today";
                ApplyCurrentFilter();
                break;
        }
    }

    [RelayCommand]
    private async Task FilterAllTasks()
    {
        CurrentFilter = "all";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterInProgress()
    {
        CurrentFilter = "in-progress";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterOverdue()
    {
        CurrentFilter = "overdue";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task FilterCompleted()
    {
        CurrentFilter = "completed";
        ApplyCurrentFilter();
        UpdateFilterColors();
    }

    [RelayCommand]
    private async Task StartTask(TaskItem task)
    {
        try
        {
            if (task == null) return;

            // TODO: Implement actual task start API call
            task.Status = "In Progress";
            task.StatusColor = Colors.Orange;
            task.CanStart = false;
            task.CanComplete = true;

            await Shell.Current.DisplayAlert("Task Started", $"Started working on: {task.Title}", "OK");
            
            ApplyCurrentFilter();
            UpdateTasksSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting task");
        }
    }

    [RelayCommand]
    private async Task CompleteTask(TaskItem task)
    {
        try
        {
            if (task == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Complete Task",
                $"Mark '{task.Title}' as completed?",
                "Complete",
                "Cancel");

            if (confirm)
            {
                // TODO: Implement actual task completion API call
                task.Status = "Completed";
                task.StatusColor = Colors.Green;
                task.CanStart = false;
                task.CanComplete = false;
                task.ProgressPercentage = 1.0;
                task.ProgressText = "100%";

                ApplyCurrentFilter();
                UpdateTasksSummary();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task");
        }
    }

    [RelayCommand]
    private async Task ViewTaskDetails(TaskItem task)
    {
        try
        {
            if (task == null) return;

            await _navigationService.NavigateToAsync($"tasks/detail?taskId={task.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to task details");
        }
    }

    [RelayCommand]
    private async Task AddTask()
    {
        try
        {
            await _navigationService.NavigateToAsync("tasks/create");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to create task");
        }
    }

    private void ApplyCurrentFilter()
    {
        FilteredTasks.Clear();

        var filtered = CurrentFilter switch
        {
            "in-progress" => AllTasks.Where(t => t.Status == "In Progress"),
            "overdue" => AllTasks.Where(t => t.IsOverdue),
            "completed" => AllTasks.Where(t => t.Status == "Completed"),
            "high-priority" => AllTasks.Where(t => t.Priority == "High"),
            "due-today" => AllTasks.Where(t => t.DueDate.Date == DateTime.Today),
            _ => AllTasks
        };

        foreach (var task in filtered.OrderBy(t => t.DueDate))
        {
            FilteredTasks.Add(task);
        }
    }

    private void UpdateFilterColors()
    {
        // Reset all colors
        AllTasksChipColor = Colors.Gray;
        InProgressChipColor = Colors.Gray;
        OverdueChipColor = Colors.Gray;
        CompletedChipColor = Colors.Gray;

        // Set active filter color
        switch (CurrentFilter)
        {
            case "all":
                AllTasksChipColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                    ? Color.FromArgb("#2196F3") : Color.FromArgb("#1976D2");
                break;
            case "in-progress":
                InProgressChipColor = Colors.Orange;
                break;
            case "overdue":
                OverdueChipColor = Colors.Red;
                break;
            case "completed":
                CompletedChipColor = Colors.Green;
                break;
        }
    }

    private void UpdateTasksSummary()
    {
        var totalTasks = AllTasks.Count;
        var completedTasks = AllTasks.Count(t => t.Status == "Completed");
        var inProgressTasks = AllTasks.Count(t => t.Status == "In Progress");
        var overdueTasks = AllTasks.Count(t => t.IsOverdue);

        TasksSummary = $"{totalTasks} total • {inProgressTasks} in progress • {overdueTasks} overdue";
    }

    private List<TaskItem> GenerateSampleTasks()
    {
        var random = new Random();
        var projects = new[] { "Mobile App", "Web Portal", "Backend API", "Dashboard", "Analytics" };
        var priorities = new[] { "Low", "Medium", "High" };
        var statuses = new[] { "To Do", "In Progress", "Review", "Completed" };

        var tasks = new List<TaskItem>();

        for (int i = 1; i <= 12; i++)
        {
            var dueDate = DateTime.Today.AddDays(random.Next(-5, 14));
            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];
            var progress = random.NextDouble();

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Task {i}: {GenerateTaskTitle()}",
                ProjectName = projects[random.Next(projects.Length)],
                Status = status,
                Priority = priority,
                DueDate = dueDate,
                ProgressPercentage = status == "Completed" ? 1.0 : progress,
                ProgressText = status == "Completed" ? "100%" : $"{(int)(progress * 100)}%"
            };

            // Set colors based on status and priority
            task.StatusColor = status switch
            {
                "To Do" => Colors.Gray,
                "In Progress" => Colors.Orange,
                "Review" => Colors.Blue,
                "Completed" => Colors.Green,
                _ => Colors.Gray
            };

            task.PriorityColor = priority switch
            {
                "Low" => Colors.Green,
                "Medium" => Colors.Orange,
                "High" => Colors.Red,
                _ => Colors.Gray
            };

            task.IsOverdue = dueDate < DateTime.Today && status != "Completed";
            task.DueDateText = dueDate.Date == DateTime.Today ? "Due Today" :
                              dueDate.Date == DateTime.Today.AddDays(1) ? "Due Tomorrow" :
                              dueDate < DateTime.Today ? $"Overdue by {(DateTime.Today - dueDate).Days} days" :
                              $"Due {dueDate:MMM dd}";

            task.DueDateColor = task.IsOverdue ? Colors.Red :
                               dueDate.Date == DateTime.Today ? Colors.Orange :
                               Colors.Gray;

            task.CanStart = status == "To Do";
            task.CanComplete = status == "In Progress" || status == "Review";

            tasks.Add(task);
        }

        return tasks;
    }

    private string GenerateTaskTitle()
    {
        var titles = new[]
        {
            "Update user interface",
            "Fix authentication bug",
            "Implement new feature",
            "Review code changes",
            "Write unit tests",
            "Update documentation",
            "Database migration",
            "API integration",
            "Performance optimization",
            "Security audit",
            "UI/UX improvements",
            "Bug fixes"
        };

        return titles[Random.Shared.Next(titles.Length)];
    }
}

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public double ProgressPercentage { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public Color StatusColor { get; set; }
    public Color PriorityColor { get; set; }
    public Color DueDateColor { get; set; }
    public string DueDateText { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public bool CanStart { get; set; }
    public bool CanComplete { get; set; }
}