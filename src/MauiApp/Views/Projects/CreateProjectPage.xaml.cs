using MauiApp.ViewModels.Projects;

namespace MauiApp.Views.Projects;

public partial class CreateProjectPage : ContentPage
{
    public CreateProjectPage(CreateProjectViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Check if we have a projectId parameter for editing
        if (BindingContext is CreateProjectViewModel viewModel)
        {
            var uri = Shell.Current.CurrentState.Location.ToString();
            if (uri.Contains("projectId="))
            {
                var projectId = ExtractProjectIdFromUri(uri);
                if (!string.IsNullOrEmpty(projectId))
                {
                    await viewModel.LoadProjectCommand.ExecuteAsync(projectId);
                }
            }
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