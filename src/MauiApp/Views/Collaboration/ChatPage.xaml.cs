using MauiApp.ViewModels.Collaboration;

namespace MauiApp.Views.Collaboration;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

    public ChatPage(ChatViewModel viewModel)
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
            
            // Scroll to bottom after loading messages
            await ScrollToBottomAsync();
        }
        else
        {
            // Show project selection or navigate back if no project ID
            await Shell.Current.DisplayAlert("Error", "No project selected for chat", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Disconnect from chat when leaving the page
        await _viewModel.DisconnectAsync();
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

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await Task.Delay(100); // Small delay to ensure UI is rendered
            await MessagesScrollView.ScrollToAsync(0, double.MaxValue, false);
        }
        catch (Exception ex)
        {
            // Log but don't throw - scrolling is not critical
            System.Diagnostics.Debug.WriteLine($"Error scrolling to bottom: {ex.Message}");
        }
    }
}