using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using MauiApp.Data.Repositories;
using MauiApp.Data.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Projects;

public partial class ProjectsListViewModel : ObservableObject
{
    private readonly ILocalProjectRepository _projectRepository;
    private readonly INavigationService _navigationService;
    private readonly IOfflineSyncService _syncService;
    private readonly ILogger<ProjectsListViewModel> _logger;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedFilter = "all";

    [ObservableProperty]
    private ObservableCollection<ProjectItem> projects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectItem> filteredProjects = new();

    [ObservableProperty]
    private string projectsSummary = "Loading projects...";

    [ObservableProperty]
    private bool hasProjects = false;

    [ObservableProperty]
    private int activeProjectsCount = 0;

    [ObservableProperty]
    private int completedProjectsCount = 0;

    [ObservableProperty]
    private int archivedProjectsCount = 0;

    public ProjectsListViewModel(
        ILocalProjectRepository projectRepository,
        INavigationService navigationService,
        IOfflineSyncService syncService,
        ILogger<ProjectsListViewModel> logger)
    {
        _projectRepository = projectRepository;
        _navigationService = navigationService;
        _syncService = syncService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadProjects()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading projects from local database");

            var localProjects = await _projectRepository.GetActiveProjectsAsync();
            
            Projects.Clear();
            foreach (var project in localProjects)
            {
                Projects.Add(MapToProjectItem(project));
            }

            ApplyFilter();
            UpdateProjectsSummary();
            HasProjects = Projects.Any();

            _logger.LogInformation($"Loaded {Projects.Count} projects");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading projects");
            await Shell.Current.DisplayAlert("Error", "Unable to load projects. Please try again.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
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
                var syncResult = await _syncService.SyncProjectsAsync();
                if (!syncResult.IsSuccess)
                {
                    _logger.LogWarning($"Sync failed: {syncResult.ErrorMessage}");
                }
            }

            // Reload from local database
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing projects");
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
    private async Task FilterProjects(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        try
        {
            await _navigationService.NavigateToAsync("projects/create");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to create project");
        }
    }

    [RelayCommand]
    private async Task ViewProject(ProjectItem project)
    {
        try
        {
            if (project == null) return;
            await _navigationService.NavigateToAsync($"projects/detail?projectId={project.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to project details");
        }
    }

    [RelayCommand]
    private async Task EditProject(ProjectItem project)
    {
        try
        {
            if (project == null) return;
            await _navigationService.NavigateToAsync($"projects/create?projectId={project.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to edit project");
        }
    }

    [RelayCommand]
    private async Task ArchiveProject(ProjectItem project)
    {
        try
        {
            if (project == null) return;

            var confirm = await Shell.Current.DisplayAlert(
                "Archive Project",
                $"Are you sure you want to archive '{project.Name}'?",
                "Archive",
                "Cancel");

            if (confirm)
            {
                var localProject = await _projectRepository.GetByIdAsync(project.Id);
                if (localProject != null)
                {
                    localProject.IsArchived = true;
                    await _projectRepository.UpdateAsync(localProject);
                    await _projectRepository.SaveChangesAsync();

                    // Remove from list
                    Projects.Remove(project);
                    ApplyFilter();
                    UpdateProjectsSummary();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project");
            await Shell.Current.DisplayAlert("Error", "Unable to archive project. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task ViewTasks(ProjectItem project)
    {
        try
        {
            if (project == null) return;
            await _navigationService.NavigateToAsync($"tasks/list?projectId={project.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to project tasks");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredProjects.Clear();

        var filtered = Projects.AsEnumerable();

        // Apply text search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply status filter
        filtered = SelectedFilter switch
        {
            "active" => filtered.Where(p => p.Status == "Active"),
            "completed" => filtered.Where(p => p.Status == "Completed"),
            "onhold" => filtered.Where(p => p.Status == "On Hold"),
            "overdue" => filtered.Where(p => p.IsOverdue),
            _ => filtered // "all"
        };

        foreach (var project in filtered.OrderBy(p => p.Name))
        {
            FilteredProjects.Add(project);
        }
    }

    private void UpdateProjectsSummary()
    {
        ActiveProjectsCount = Projects.Count(p => p.Status == "Active");
        CompletedProjectsCount = Projects.Count(p => p.Status == "Completed");
        ArchivedProjectsCount = Projects.Count(p => p.Status == "Archived");

        var overdueCount = Projects.Count(p => p.IsOverdue);

        ProjectsSummary = $"{Projects.Count} projects • {ActiveProjectsCount} active";
        if (overdueCount > 0)
        {
            ProjectsSummary += $" • {overdueCount} overdue";
        }
    }

    private ProjectItem MapToProjectItem(LocalProject localProject)
    {
        var daysUntilDue = localProject.DueDate.HasValue
            ? (localProject.DueDate.Value.Date - DateTime.Today).Days
            : (int?)null;

        return new ProjectItem
        {
            Id = localProject.Id,
            Name = localProject.Name,
            Description = localProject.Description,
            Status = localProject.Status,
            StartDate = localProject.StartDate,
            DueDate = localProject.DueDate,
            Budget = localProject.Budget,
            Color = string.IsNullOrEmpty(localProject.Color) ? "#2196F3" : localProject.Color,
            Progress = CalculateProjectProgress(localProject.Id),
            TaskCount = GetProjectTaskCount(localProject.Id),
            TeamSize = GetProjectTeamSize(localProject.Id),
            IsOverdue = localProject.DueDate.HasValue && localProject.DueDate.Value.Date < DateTime.Today && localProject.Status != "Completed",
            DaysUntilDue = daysUntilDue,
            DueDateText = GetDueDateText(localProject.DueDate),
            StatusColor = GetStatusColor(localProject.Status),
            ProgressColor = GetProgressColor(CalculateProjectProgress(localProject.Id)),
            IsSynced = localProject.IsSynced,
            HasLocalChanges = localProject.HasLocalChanges
        };
    }

    private double CalculateProjectProgress(Guid projectId)
    {
        // TODO: Calculate based on project tasks
        return Random.Shared.NextDouble() * 100;
    }

    private int GetProjectTaskCount(Guid projectId)
    {
        // TODO: Get actual task count from repository
        return Random.Shared.Next(3, 25);
    }

    private int GetProjectTeamSize(Guid projectId)
    {
        // TODO: Get actual team size from repository
        return Random.Shared.Next(2, 8);
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
            _ => dueDate.Value.ToString("MMM dd, yyyy")
        };
    }

    private Color GetStatusColor(string status)
    {
        return status switch
        {
            "Active" => Colors.Green,
            "Completed" => Colors.Blue,
            "On Hold" => Colors.Orange,
            "Cancelled" => Colors.Red,
            _ => Colors.Gray
        };
    }

    private Color GetProgressColor(double progress)
    {
        return progress switch
        {
            < 30 => Colors.Red,
            < 70 => Colors.Orange,
            _ => Colors.Green
        };
    }
}

public class ProjectItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Budget { get; set; }
    public string Color { get; set; } = string.Empty;
    public double Progress { get; set; }
    public int TaskCount { get; set; }
    public int TeamSize { get; set; }
    public bool IsOverdue { get; set; }
    public int? DaysUntilDue { get; set; }
    public string DueDateText { get; set; } = string.Empty;
    public Color StatusColor { get; set; }
    public Color ProgressColor { get; set; }
    public bool IsSynced { get; set; }
    public bool HasLocalChanges { get; set; }
    public string BudgetText => Budget > 0 ? $"${Budget:N0}" : "No budget";
    public string ProgressText => $"{Progress:F0}%";
}