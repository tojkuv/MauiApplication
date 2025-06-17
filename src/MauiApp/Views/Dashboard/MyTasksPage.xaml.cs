using MauiApp.ViewModels.Dashboard;

namespace MauiApp.Views.Dashboard;

public partial class MyTasksPage : ContentPage
{
    public MyTasksPage(MyTasksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is MyTasksViewModel viewModel)
        {
            await viewModel.LoadTasksCommand.ExecuteAsync(null);
        }
    }
}