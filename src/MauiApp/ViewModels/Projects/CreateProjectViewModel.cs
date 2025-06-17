using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using MauiApp.Data.Repositories;
using MauiApp.Data.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace MauiApp.ViewModels.Projects;

public partial class CreateProjectViewModel : ObservableObject
{
    private readonly ILocalProjectRepository _projectRepository;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<CreateProjectViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [Required(ErrorMessage = "Project name is required")]
    [MinLength(3, ErrorMessage = "Project name must be at least 3 characters")]
    [MaxLength(200, ErrorMessage = "Project name cannot exceed 200 characters")]
    private string name = string.Empty;

    [ObservableProperty]
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    private string description = string.Empty;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? dueDate;

    [ObservableProperty]
    private string budget = string.Empty;

    [ObservableProperty]
    private string selectedStatus = "Active";

    [ObservableProperty]
    private string selectedColor = "#2196F3";

    [ObservableProperty]
    private bool isEditMode = false;

    [ObservableProperty]
    private string pageTitle = "Create Project";

    [ObservableProperty]
    private string saveButtonText = "Create Project";

    [ObservableProperty]
    private bool hasValidationErrors = false;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    private Guid? _editingProjectId;

    public List<string> StatusOptions { get; } = new()
    {
        "Active",
        "Planning",
        "On Hold",
        "Completed",
        "Cancelled"
    };

    public List<ColorOption> ColorOptions { get; } = new()
    {
        new("#2196F3", "Blue"),
        new("#4CAF50", "Green"),
        new("#FF9800", "Orange"),
        new("#F44336", "Red"),
        new("#9C27B0", "Purple"),
        new("#607D8B", "Blue Grey"),
        new("#795548", "Brown"),
        new("#009688", "Teal")
    };

    public CreateProjectViewModel(
        ILocalProjectRepository projectRepository,
        INavigationService navigationService,
        IAuthenticationService authenticationService,
        ILogger<CreateProjectViewModel> logger)
    {
        _projectRepository = projectRepository;
        _navigationService = navigationService;
        _authenticationService = authenticationService;
        _logger = logger;

        // Set default due date to 3 months from now
        DueDate = DateTime.Today.AddMonths(3);
    }

    [RelayCommand]
    private async Task LoadProject(string projectId)
    {
        if (string.IsNullOrEmpty(projectId) || !Guid.TryParse(projectId, out var id))
            return;

        try
        {
            IsLoading = true;
            _editingProjectId = id;
            IsEditMode = true;
            PageTitle = "Edit Project";
            SaveButtonText = "Update Project";

            var project = await _projectRepository.GetByIdAsync(id);
            if (project != null)
            {
                Name = project.Name;
                Description = project.Description;
                StartDate = project.StartDate;
                DueDate = project.DueDate;
                Budget = project.Budget > 0 ? project.Budget.ToString() : string.Empty;
                SelectedStatus = project.Status;
                SelectedColor = string.IsNullOrEmpty(project.Color) ? "#2196F3" : project.Color;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading project for editing");
            await Shell.Current.DisplayAlert("Error", "Unable to load project details.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        try
        {
            if (!ValidateForm())
                return;

            IsLoading = true;

            var currentUser = await _authenticationService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "Unable to determine current user.", "OK");
                return;
            }

            LocalProject project;

            if (IsEditMode && _editingProjectId.HasValue)
            {
                // Update existing project
                project = await _projectRepository.GetByIdAsync(_editingProjectId.Value);
                if (project == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Project not found.", "OK");
                    return;
                }
            }
            else
            {
                // Create new project
                project = new LocalProject
                {
                    Id = Guid.NewGuid(),
                    OwnerId = Guid.Parse(currentUser.Id),
                    CreatedAt = DateTime.UtcNow
                };
            }

            // Update project properties
            project.Name = Name.Trim();
            project.Description = Description.Trim();
            project.StartDate = StartDate;
            project.DueDate = DueDate;
            project.Status = SelectedStatus;
            project.Color = SelectedColor;
            project.UpdatedAt = DateTime.UtcNow;
            project.HasLocalChanges = true;
            project.IsSynced = false;

            // Parse budget
            if (decimal.TryParse(Budget, out var budgetValue))
            {
                project.Budget = budgetValue;
            }

            if (IsEditMode)
            {
                await _projectRepository.UpdateAsync(project);
            }
            else
            {
                await _projectRepository.AddAsync(project);
            }

            await _projectRepository.SaveChangesAsync();

            _logger.LogInformation($"Project {(IsEditMode ? "updated" : "created")}: {project.Name}");

            // Show success message
            var message = IsEditMode ? "Project updated successfully!" : "Project created successfully!";
            await Shell.Current.DisplayAlert("Success", message, "OK");

            // Navigate back to projects list
            await _navigationService.NavigateToAsync("projects/list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving project");
            await Shell.Current.DisplayAlert("Error", "Unable to save project. Please try again.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Cancel()
    {
        try
        {
            var hasChanges = !string.IsNullOrWhiteSpace(Name) || 
                           !string.IsNullOrWhiteSpace(Description) || 
                           !string.IsNullOrWhiteSpace(Budget);

            if (hasChanges)
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Discard Changes",
                    "Are you sure you want to discard your changes?",
                    "Discard",
                    "Continue Editing");

                if (!confirm)
                    return;
            }

            await _navigationService.NavigateToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling project creation");
        }
    }

    [RelayCommand]
    private void ClearDueDate()
    {
        DueDate = null;
    }

    [RelayCommand]
    private void SetDueDateToday()
    {
        DueDate = DateTime.Today;
    }

    [RelayCommand]
    private void SetDueDateNextWeek()
    {
        DueDate = DateTime.Today.AddDays(7);
    }

    [RelayCommand]
    private void SetDueDateNextMonth()
    {
        DueDate = DateTime.Today.AddMonths(1);
    }

    [RelayCommand]
    private void SetDueDateThreeMonths()
    {
        DueDate = DateTime.Today.AddMonths(3);
    }

    [RelayCommand]
    private void SelectColor(string colorHex)
    {
        if (!string.IsNullOrEmpty(colorHex))
        {
            SelectedColor = colorHex;
        }
    }

    partial void OnNameChanged(string value)
    {
        ValidateForm();
    }

    partial void OnDescriptionChanged(string value)
    {
        ValidateForm();
    }

    partial void OnBudgetChanged(string value)
    {
        ValidateForm();
    }

    private bool ValidateForm()
    {
        var errors = new List<string>();

        // Validate name
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Project name is required");
        }
        else if (Name.Trim().Length < 3)
        {
            errors.Add("Project name must be at least 3 characters");
        }
        else if (Name.Trim().Length > 200)
        {
            errors.Add("Project name cannot exceed 200 characters");
        }

        // Validate description
        if (!string.IsNullOrEmpty(Description) && Description.Length > 1000)
        {
            errors.Add("Description cannot exceed 1000 characters");
        }

        // Validate budget
        if (!string.IsNullOrWhiteSpace(Budget) && !decimal.TryParse(Budget, out var budgetValue))
        {
            errors.Add("Budget must be a valid number");
        }
        else if (decimal.TryParse(Budget, out var parsedBudget) && parsedBudget < 0)
        {
            errors.Add("Budget cannot be negative");
        }

        // Validate dates
        if (DueDate.HasValue && DueDate.Value.Date < StartDate.Date)
        {
            errors.Add("Due date cannot be before start date");
        }

        HasValidationErrors = errors.Any();
        ValidationMessage = string.Join(Environment.NewLine, errors);

        return !HasValidationErrors;
    }
}

public class ColorOption
{
    public ColorOption(string hex, string name)
    {
        Hex = hex;
        Name = name;
        Color = Color.FromArgb(hex);
    }

    public string Hex { get; }
    public string Name { get; }
    public Color Color { get; }
}