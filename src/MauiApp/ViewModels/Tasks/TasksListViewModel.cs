using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using MauiApp.Data.Repositories;
using MauiApp.Data.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Tasks;

public partial class TasksListViewModel : ObservableObject
{
    private readonly ILocalTaskRepository _taskRepository;
    private readonly ILocalProjectRepository _projectRepository;
    private readonly INavigationService _navigationService;
    private readonly IOfflineSyncService _syncService;
    private readonly ILogger<TasksListViewModel> _logger;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedFilter = "all";

    [ObservableProperty]
    private string selectedProject = "all";

    [ObservableProperty]
    private ObservableCollection<TaskItem> allTasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskItem> filteredTasks = new();

    [ObservableProperty]
    private ObservableCollection<ProjectOption> projectOptions = new();

    [ObservableProperty]
    private string tasksSummary = "Loading tasks...";

    [ObservableProperty]
    private bool hasTasks = false;

    [ObservableProperty]
    private int todoCount = 0;

    [ObservableProperty]
    private int inProgressCount = 0;

    [ObservableProperty]
    private int completedCount = 0;

    [ObservableProperty]
    private int overdueCount = 0;

    private Guid? _projectFilter;

    public TasksListViewModel(
        ILocalTaskRepository taskRepository,
        ILocalProjectRepository projectRepository,
        INavigationService navigationService,
        IOfflineSyncService syncService,
        ILogger<TasksListViewModel> logger)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _navigationService = navigationService;
        _syncService = syncService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadTasks()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading tasks from local database");

            // Load projects for filter dropdown
            await LoadProjectOptionsAsync();

            // Load tasks
            var localTasks = _projectFilter.HasValue 
                ? await _taskRepository.GetByProjectIdAsync(_projectFilter.Value)
                : (await _taskRepository.GetAllAsync()).AsEnumerable();
            
            AllTasks.Clear();
            foreach (var task in localTasks)
            {
                AllTasks.Add(await MapToTaskItemAsync(task));
            }

            ApplyFilter();
            UpdateTasksSummary();
            HasTasks = AllTasks.Any();

            _logger.LogInformation($"Loaded {AllTasks.Count} tasks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tasks");
            await Shell.Current.DisplayAlert("Error", "Unable to load tasks. Please try again.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadTasksForProject(string projectId)
    {
        if (Guid.TryParse(projectId, out var id))
        {
            _projectFilter = id;
            SelectedProject = projectId;
        }
        else
        {
            _projectFilter = null;
            SelectedProject = "all";
        }

        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            
            // Try to sync with server if online
            if (await _syncService.IsOnlineAsync())
            {
                var syncResult = await _syncService.SyncTasksAsync();
                if (!syncResult.IsSuccess)
                {
                    _logger.LogWarning($"Sync failed: {syncResult.ErrorMessage}");
                }
            }

            // Reload from local database
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing tasks");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task FilterTasks(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task FilterByProject(string projectId)
    {
        await LoadTasksForProjectAsync(projectId);
    }

    [RelayCommand]
    private async Task CreateTask()
    {
        try
        {
            var route = _projectFilter.HasValue 
                ? $"tasks/create?projectId={_projectFilter.Value}" 
                : "tasks/create";
            await _navigationService.NavigateToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to create task");
        }
    }

    [RelayCommand]
    private async Task ViewTask(TaskItem task)
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
    private async Task EditTask(TaskItem task)
    {
        try
        {
            if (task == null) return;
            await _navigationService.NavigateToAsync($"tasks/create?taskId={task.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to edit task");
        }
    }

    [RelayCommand]
    private async Task StartTask(TaskItem task)
    {
        try
        {
            if (task == null) return;

            var localTask = await _taskRepository.GetByIdAsync(task.Id);
            if (localTask != null)
            {
                localTask.Status = "In Progress";
                localTask.StartDate = DateTime.UtcNow;
                await _taskRepository.UpdateAsync(localTask);
                await _taskRepository.SaveChangesAsync();

                // Update the task item
                task.Status = "In Progress";
                task.StatusColor = GetStatusColor("In Progress");
                task.CanStart = false;
                task.CanComplete = true;

                ApplyFilter();
                UpdateTasksSummary();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting task");
            await Shell.Current.DisplayAlert("Error", "Unable to start task. Please try again.", "OK");
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
                var localTask = await _taskRepository.GetByIdAsync(task.Id);
                if (localTask != null)
                {
                    localTask.Status = "Completed";
                    localTask.CompletedDate = DateTime.UtcNow;
                    localTask.ProgressPercentage = 100;
                    await _taskRepository.UpdateAsync(localTask);
                    await _taskRepository.SaveChangesAsync();

                    // Update the task item
                    task.Status = "Completed";
                    task.StatusColor = GetStatusColor("Completed");
                    task.ProgressPercentage = 100;
                    task.ProgressText = "100%";
                    task.CanStart = false;
                    task.CanComplete = false;

                    ApplyFilter();
                    UpdateTasksSummary();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task");
            await Shell.Current.DisplayAlert("Error", "Unable to complete task. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task ViewKanbanBoard()
    {
        try
        {
            var route = _projectFilter.HasValue 
                ? $"tasks/kanban?projectId={_projectFilter.Value}" 
                : "tasks/kanban";
            await _navigationService.NavigateToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to Kanban board");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedProjectChanged(string value)
    {
        if (value != null)
        {
            _ = FilterByProjectAsync(value);
        }
    }

    private void ApplyFilter()
    {
        FilteredTasks.Clear();

        var filtered = AllTasks.AsEnumerable();

        // Apply text search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(t =>
                t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply status filter
        filtered = SelectedFilter switch
        {
            "todo" => filtered.Where(t => t.Status == "To Do"),
            "inprogress" => filtered.Where(t => t.Status == "In Progress"),
            "review" => filtered.Where(t => t.Status == "Review"),
            "completed" => filtered.Where(t => t.Status == "Completed"),
            "overdue" => filtered.Where(t => t.IsOverdue),
            "high" => filtered.Where(t => t.Priority == "High"),
            "duetoday" => filtered.Where(t => t.DueDate?.Date == DateTime.Today),
            _ => filtered // "all"
        };

        foreach (var task in filtered.OrderBy(t => t.DueDate).ThenBy(t => t.SortOrder))
        {
            FilteredTasks.Add(task);
        }
    }

    private void UpdateTasksSummary()
    {
        TodoCount = AllTasks.Count(t => t.Status == "To Do");
        InProgressCount = AllTasks.Count(t => t.Status == "In Progress");
        CompletedCount = AllTasks.Count(t => t.Status == "Completed");
        OverdueCount = AllTasks.Count(t => t.IsOverdue);

        TasksSummary = $"{AllTasks.Count} tasks • {InProgressCount} in progress";
        if (OverdueCount > 0)
        {
            TasksSummary += $" • {OverdueCount} overdue";
        }
    }

    private async Task LoadProjectOptionsAsync()
    {
        try
        {
            var projects = await _projectRepository.GetActiveProjectsAsync();
            
            ProjectOptions.Clear();
            ProjectOptions.Add(new ProjectOption { Id = "all", Name = "All Projects" });
            
            foreach (var project in projects.OrderBy(p => p.Name))
            {
                ProjectOptions.Add(new ProjectOption 
                { 
                    Id = project.Id.ToString(), 
                    Name = project.Name,
                    Color = project.Color
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading project options");
        }
    }

    private async Task<TaskItem> MapToTaskItemAsync(LocalTask localTask)
    {
        var project = await _projectRepository.GetByIdAsync(localTask.ProjectId);
        
        var daysUntilDue = localTask.DueDate.HasValue
            ? (localTask.DueDate.Value.Date - DateTime.Today).Days
            : (int?)null;

        return new TaskItem
        {
            Id = localTask.Id,
            Title = localTask.Title,
            Description = localTask.Description,
            ProjectId = localTask.ProjectId,
            ProjectName = project?.Name ?? "Unknown Project",
            ProjectColor = project?.Color ?? "#2196F3",
            Status = localTask.Status,
            Priority = localTask.Priority,
            DueDate = localTask.DueDate,
            StartDate = localTask.StartDate,
            CompletedDate = localTask.CompletedDate,
            ProgressPercentage = localTask.ProgressPercentage,
            ProgressText = $"{localTask.ProgressPercentage:F0}%",
            EstimatedHours = localTask.EstimatedHours,
            ActualHours = localTask.ActualHours,
            Tags = ParseTags(localTask.Tags),
            SortOrder = localTask.SortOrder,
            IsOverdue = localTask.DueDate.HasValue && localTask.DueDate.Value.Date < DateTime.Today && localTask.Status != "Completed",
            DaysUntilDue = daysUntilDue,
            DueDateText = GetDueDateText(localTask.DueDate),
            StatusColor = GetStatusColor(localTask.Status),
            PriorityColor = GetPriorityColor(localTask.Priority),
            DueDateColor = GetDueDateColor(localTask.DueDate, localTask.Status),
            CanStart = localTask.Status == "To Do",
            CanComplete = localTask.Status == "In Progress" || localTask.Status == "Review",
            IsSynced = localTask.IsSynced,
            HasLocalChanges = localTask.HasLocalChanges
        };
    }

    private List<string> ParseTags(string tagsJson)
    {
        try
        {
            if (string.IsNullOrEmpty(tagsJson))
                return new List<string>();
            
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private string GetDueDateText(DateTime? dueDate)
    {
        if (!dueDate.HasValue) return "No due date";

        var days = (dueDate.Value.Date - DateTime.Today).Days;
        return days switch
        {
            < 0 => $"Overdue by {Math.Abs(days)} days",
            0 => "Due today",
            1 => "Due tomorrow",
            <= 7 => $"Due in {days} days",
            _ => dueDate.Value.ToString("MMM dd")
        };
    }

    private Color GetStatusColor(string status)
    {
        return status switch
        {
            "To Do" => Colors.Gray,
            "In Progress" => Colors.Orange,
            "Review" => Colors.Blue,
            "Completed" => Colors.Green,
            _ => Colors.Gray
        };
    }

    private Color GetPriorityColor(string priority)
    {
        return priority switch
        {
            "Low" => Colors.Green,
            "Medium" => Colors.Orange,
            "High" => Colors.Red,
            "Critical" => Colors.DarkRed,
            _ => Colors.Gray
        };
    }

    private Color GetDueDateColor(DateTime? dueDate, string status)
    {
        if (!dueDate.HasValue || status == "Completed") return Colors.Gray;

        var days = (dueDate.Value.Date - DateTime.Today).Days;
        return days switch
        {
            < 0 => Colors.Red,
            0 => Colors.Orange,
            <= 3 => Colors.Orange,
            _ => Colors.Gray
        };
    }
}

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectColor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    public List<string> Tags { get; set; } = new();
    public int SortOrder { get; set; }
    public bool IsOverdue { get; set; }
    public int? DaysUntilDue { get; set; }
    public string DueDateText { get; set; } = string.Empty;
    public Color StatusColor { get; set; }
    public Color PriorityColor { get; set; }
    public Color DueDateColor { get; set; }
    public bool CanStart { get; set; }
    public bool CanComplete { get; set; }
    public bool IsSynced { get; set; }
    public bool HasLocalChanges { get; set; }
    public string TagsText => Tags.Any() ? string.Join(", ", Tags) : "No tags";
    public string EstimatedTimeText => EstimatedHours > 0 ? $"{EstimatedHours}h" : "No estimate";
    public string ActualTimeText => ActualHours > 0 ? $"{ActualHours}h" : "No time logged";
}

public class ProjectOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2196F3";
}