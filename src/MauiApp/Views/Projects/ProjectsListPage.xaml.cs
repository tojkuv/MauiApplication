using MauiApp.ViewModels.Projects;

namespace MauiApp.Views.Projects;

public partial class ProjectsListPage : ContentPage
{
    public ProjectsListPage(ProjectsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is ProjectsListViewModel viewModel)
        {
            await viewModel.LoadProjectsCommand.ExecuteAsync(null);
        }
    }
}