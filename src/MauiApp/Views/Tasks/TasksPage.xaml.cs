using MauiApp.ViewModels.Tasks;

namespace MauiApp.Views.Tasks;

public partial class TasksPage : ContentPage
{
    public TasksPage(TasksListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is TasksListViewModel viewModel)
        {
            // Check if we have a projectId parameter for filtering
            var uri = Shell.Current.CurrentState.Location.ToString();
            if (uri.Contains("projectId="))
            {
                var projectId = ExtractProjectIdFromUri(uri);
                if (!string.IsNullOrEmpty(projectId))
                {
                    await viewModel.LoadTasksForProjectCommand.ExecuteAsync(projectId);
                    return;
                }
            }
            
            // Load all tasks if no project filter
            await viewModel.LoadTasksCommand.ExecuteAsync(null);
        }
    }

    private string ExtractProjectIdFromUri(string uri)
    {
        try
        {
            var parts = uri.Split('?', '&');
            var projectIdPart = parts.FirstOrDefault(p => p.StartsWith("projectId="));
            return projectIdPart?.Substring("projectId=".Length);
        }
        catch
        {
            return string.Empty;
        }
    }
}