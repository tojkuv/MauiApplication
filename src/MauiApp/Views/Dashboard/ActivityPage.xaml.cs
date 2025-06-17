using MauiApp.ViewModels.Dashboard;

namespace MauiApp.Views.Dashboard;

public partial class ActivityPage : ContentPage
{
    public ActivityPage(ActivityViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is ActivityViewModel viewModel)
        {
            await viewModel.LoadActivitiesCommand.ExecuteAsync(null);
        }
    }
}