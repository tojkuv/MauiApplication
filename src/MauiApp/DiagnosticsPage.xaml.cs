using MauiApp.ViewModels;

namespace MauiApp;

public partial class DiagnosticsPage : ContentPage
{
    public DiagnosticsPage(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is DiagnosticsViewModel viewModel)
        {
            await viewModel.LoadDiagnosticsCommand.ExecuteAsync(null);
        }
    }
}