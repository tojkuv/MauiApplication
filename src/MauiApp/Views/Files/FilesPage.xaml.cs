using MauiApp.ViewModels.Files;

namespace MauiApp.Views.Files;

public partial class FilesPage : ContentPage
{
    private readonly FilesViewModel _viewModel;

    public FilesPage(FilesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Get project ID from query parameters or navigation
        var projectIdParam = await GetProjectIdFromQuery();
        if (projectIdParam != Guid.Empty)
        {
            await _viewModel.InitializeCommand.ExecuteAsync(projectIdParam);
        }
        else
        {
            // Show project selection or navigate back if no project ID
            await Shell.Current.DisplayAlert("Error", "No project selected for file management", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task<Guid> GetProjectIdFromQuery()
    {
        try
        {
            // Try to get project ID from query parameters
            var query = Shell.Current.CurrentState.Location.ToString();
            if (query.Contains("projectId="))
            {
                var projectIdString = query.Split("projectId=")[1].Split('&')[0];
                if (Guid.TryParse(projectIdString, out var projectId))
                {
                    return projectId;
                }
            }
            
            // Fallback: try to get from previous page or use a default project
            // In a real app, you might get this from a navigation parameter or user selection
            return Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }
}