using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Tasks;

public partial class KanbanBoardViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<KanbanBoardViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private Guid selectedProjectId;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<KanbanColumnViewModel> columns = new();

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private TaskDto? selectedTask;

    [ObservableProperty]
    private bool isTaskDetailsVisible;

    [ObservableProperty]
    private string filterPriority = "All";

    [ObservableProperty]
    private string filterAssignee = "All";

    public ObservableCollection<string> PriorityFilters { get; } = new()
    {
        "All", "Low", "Medium", "High", "Critical"
    };

    public ObservableCollection<string> AssigneeFilters { get; } = new()
    {
        "All", "Assigned to Me", "Unassigned"
    };

    public bool HasActiveFilters => FilterPriority != "All" || FilterAssignee != "All" || !string.IsNullOrWhiteSpace(SearchQuery);

    public int TotalTasksCount => Columns.Sum(c => c.TaskCount);

    partial void OnFilterPriorityChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    partial void OnFilterAssigneeChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    partial void OnColumnsChanged(ObservableCollection<KanbanColumnViewModel> value)
    {
        OnPropertyChanged(nameof(TotalTasksCount));
    }

    public KanbanBoardViewModel(
        IApiService apiService,
        INavigationService navigationService,
        ILogger<KanbanBoardViewModel> logger)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadBoardAsync(Guid projectId)
    {
        try
        {
            IsLoading = true;
            HasError = false;
            SelectedProjectId = projectId;

            _logger.LogInformation("Loading Kanban board for project {ProjectId}", projectId);

            var board = await _apiService.GetAsync<KanbanBoardDto>($"/api/tasks/kanban/{projectId}");

            if (board != null)
            {
                ProjectName = board.ProjectName;
                
                Columns.Clear();
                foreach (var column in board.Columns.OrderBy(c => (int)c.Status))
                {
                    var columnViewModel = new KanbanColumnViewModel
                    {
                        Status = column.Status,
                        Title = column.Title,
                        TaskCount = column.TaskCount,
                        Tasks = new ObservableCollection<TaskDto>(column.Tasks),
                        AllTasks = new List<TaskDto>(column.Tasks) // Keep original for filtering
                    };
                    
                    Columns.Add(columnViewModel);
                }

                // Update assignee filter with actual project members
                await LoadAssigneeFiltersAsync();

                _logger.LogInformation("Kanban board loaded successfully with {ColumnCount} columns", Columns.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Kanban board for project {ProjectId}", projectId);
            HasError = true;
            ErrorMessage = "Failed to load Kanban board. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshBoardAsync()
    {
        try
        {
            IsRefreshing = true;
            await LoadBoardAsync(SelectedProjectId);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task MoveTaskAsync(TaskMoveEventArgs args)
    {
        try
        {
            _logger.LogInformation("Moving task {TaskId} from {FromStatus} to {ToStatus}", 
                args.TaskId, args.FromStatus, args.ToStatus);

            var moveRequest = new MoveTaskRequest
            {
                NewStatus = args.ToStatus,
                Position = args.Position
            };

            var updatedTask = await _apiService.PutAsync<TaskDto>($"/api/tasks/{args.TaskId}/move", moveRequest);

            if (updatedTask != null)
            {
                // Update local state immediately for better UX
                await UpdateLocalTaskStatus(args);
                
                _logger.LogInformation("Task {TaskId} moved successfully", args.TaskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving task {TaskId}", args.TaskId);
            
            // Revert the move on error
            await RefreshBoardAsync();
            
            await Shell.Current.DisplayAlert("Error", "Failed to move task. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task SelectTaskAsync(TaskDto task)
    {
        try
        {
            SelectedTask = task;
            IsTaskDetailsVisible = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting task {TaskId}", task.Id);
        }
    }

    [RelayCommand]
    private void CloseTaskDetails()
    {
        IsTaskDetailsVisible = false;
        SelectedTask = null;
    }

    [RelayCommand]
    private async Task CreateTaskAsync()
    {
        try
        {
            await _navigationService.NavigateToAsync($"tasks/create?projectId={SelectedProjectId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to create task");
        }
    }

    [RelayCommand]
    private async Task EditTaskAsync(TaskDto task)
    {
        try
        {
            await _navigationService.NavigateToAsync($"tasks/edit/{task.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to edit task {TaskId}", task.Id);
        }
    }

    [RelayCommand]
    private async Task SearchTasksAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await RefreshBoardAsync();
                return;
            }

            _logger.LogInformation("Searching tasks with query: {Query}", SearchQuery);
            
            // Filter tasks locally for now - in a real app, this would be server-side
            foreach (var column in Columns)
            {
                var filteredTasks = column.AllTasks.Where(t => 
                    t.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.AssigneeName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                column.Tasks.Clear();
                foreach (var task in filteredTasks)
                {
                    column.Tasks.Add(task);
                }
                
                column.TaskCount = column.Tasks.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks");
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        try
        {
            _logger.LogInformation("Applying filters - Priority: {Priority}, Assignee: {Assignee}", 
                FilterPriority, FilterAssignee);

            foreach (var column in Columns)
            {
                var filteredTasks = column.AllTasks.AsEnumerable();

                // Apply priority filter
                if (FilterPriority != "All")
                {
                    if (Enum.TryParse<TaskPriority>(FilterPriority, out var priority))
                    {
                        filteredTasks = filteredTasks.Where(t => t.Priority == priority);
                    }
                }

                // Apply assignee filter
                if (FilterAssignee != "All")
                {
                    if (FilterAssignee == "Assigned to Me")
                    {
                        // This would require getting current user ID
                        // For now, just show all assigned tasks
                        filteredTasks = filteredTasks.Where(t => t.AssigneeId.HasValue);
                    }
                    else if (FilterAssignee == "Unassigned")
                    {
                        filteredTasks = filteredTasks.Where(t => !t.AssigneeId.HasValue);
                    }
                }

                column.Tasks.Clear();
                foreach (var task in filteredTasks)
                {
                    column.Tasks.Add(task);
                }
                
                column.TaskCount = column.Tasks.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        try
        {
            FilterPriority = "All";
            FilterAssignee = "All";
            SearchQuery = string.Empty;
            
            await RefreshBoardAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing filters");
        }
    }

    [RelayCommand]
    private async Task ShowPriorityFilterAsync()
    {
        try
        {
            var result = await Shell.Current.DisplayActionSheet(
                "Filter by Priority",
                "Cancel",
                null,
                PriorityFilters.ToArray());

            if (result != null && result != "Cancel")
            {
                FilterPriority = result;
                await ApplyFiltersAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing priority filter");
        }
    }

    [RelayCommand]
    private async Task ShowAssigneeFilterAsync()
    {
        try
        {
            var result = await Shell.Current.DisplayActionSheet(
                "Filter by Assignee",
                "Cancel",
                null,
                AssigneeFilters.ToArray());

            if (result != null && result != "Cancel")
            {
                FilterAssignee = result;
                await ApplyFiltersAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing assignee filter");
        }
    }

    [RelayCommand]
    private async Task ClearPriorityFilterAsync()
    {
        try
        {
            FilterPriority = "All";
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing priority filter");
        }
    }

    [RelayCommand]
    private async Task ShowTaskMoveMenuAsync(TaskDto task)
    {
        try
        {
            var statusOptions = new List<string>();
            var currentColumn = Columns.FirstOrDefault(c => c.Tasks.Contains(task));
            
            // Add all available statuses except current one
            foreach (var column in Columns)
            {
                if (column.Status != task.Status)
                {
                    statusOptions.Add($"Move to {column.Title}");
                }
            }
            
            statusOptions.Add("Edit Task");
            statusOptions.Add("View Details");

            var result = await Shell.Current.DisplayActionSheet(
                $"Task: {task.Title}",
                "Cancel",
                null,
                statusOptions.ToArray());

            if (result != null && result != "Cancel")
            {
                if (result.StartsWith("Move to "))
                {
                    var targetColumnTitle = result.Substring(8); // Remove "Move to " prefix
                    var targetColumn = Columns.FirstOrDefault(c => c.Title == targetColumnTitle);
                    
                    if (targetColumn != null)
                    {
                        var moveArgs = new TaskMoveEventArgs
                        {
                            TaskId = task.Id,
                            FromStatus = task.Status,
                            ToStatus = targetColumn.Status,
                            Position = null // Add to end of column
                        };
                        
                        await MoveTaskAsync(moveArgs);
                    }
                }
                else if (result == "Edit Task")
                {
                    await EditTaskAsync(task);
                }
                else if (result == "View Details")
                {
                    await SelectTaskAsync(task);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing task move menu for task {TaskId}", task.Id);
        }
    }

    private async Task UpdateLocalTaskStatus(TaskMoveEventArgs args)
    {
        try
        {
            // Find and remove task from source column
            var sourceColumn = Columns.FirstOrDefault(c => c.Status == args.FromStatus);
            var targetColumn = Columns.FirstOrDefault(c => c.Status == args.ToStatus);
            
            if (sourceColumn != null && targetColumn != null)
            {
                var task = sourceColumn.Tasks.FirstOrDefault(t => t.Id == args.TaskId);
                if (task != null)
                {
                    sourceColumn.Tasks.Remove(task);
                    sourceColumn.TaskCount--;
                    
                    // Update task status
                    task.Status = args.ToStatus;
                    
                    // Add to target column at specified position
                    if (args.Position.HasValue && args.Position.Value < targetColumn.Tasks.Count)
                    {
                        targetColumn.Tasks.Insert(args.Position.Value, task);
                    }
                    else
                    {
                        targetColumn.Tasks.Add(task);
                    }
                    
                    targetColumn.TaskCount++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating local task status");
        }
    }

    private async Task LoadAssigneeFiltersAsync()
    {
        try
        {
            // Get unique assignees from all tasks
            var allAssignees = Columns
                .SelectMany(c => c.Tasks)
                .Where(t => t.AssigneeId.HasValue)
                .Select(t => t.AssigneeName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            // Update assignee filters
            AssigneeFilters.Clear();
            AssigneeFilters.Add("All");
            AssigneeFilters.Add("Assigned to Me");
            AssigneeFilters.Add("Unassigned");
            
            foreach (var assignee in allAssignees)
            {
                if (!string.IsNullOrEmpty(assignee))
                {
                    AssigneeFilters.Add(assignee);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assignee filters");
        }
    }
}

public class KanbanColumnViewModel : ObservableObject
{
    public MauiApp.Core.Entities.TaskStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public ObservableCollection<TaskDto> Tasks { get; set; } = new();
    
    // Keep original tasks for filtering
    public List<TaskDto> AllTasks { get; set; } = new();

    public string StatusColor => Status switch
    {
        MauiApp.Core.Entities.TaskStatus.Todo => "#6C757D",
        MauiApp.Core.Entities.TaskStatus.InProgress => "#0D6EFD", 
        MauiApp.Core.Entities.TaskStatus.Review => "#FFC107",
        MauiApp.Core.Entities.TaskStatus.Done => "#198754",
        _ => "#6C757D"
    };

    public string StatusIcon => Status switch
    {
        MauiApp.Core.Entities.TaskStatus.Todo => "📋",
        MauiApp.Core.Entities.TaskStatus.InProgress => "⚡",
        MauiApp.Core.Entities.TaskStatus.Review => "👀",
        MauiApp.Core.Entities.TaskStatus.Done => "✅",
        _ => "📋"
    };
}

public class TaskMoveEventArgs : EventArgs
{
    public Guid TaskId { get; set; }
    public MauiApp.Core.Entities.TaskStatus FromStatus { get; set; }
    public MauiApp.Core.Entities.TaskStatus ToStatus { get; set; }
    public int? Position { get; set; }
}