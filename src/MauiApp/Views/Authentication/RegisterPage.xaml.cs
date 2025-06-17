using MauiApp.ViewModels.Authentication;

namespace MauiApp.Views.Authentication;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Focus on first name entry when page appears
        if (BindingContext is RegisterViewModel viewModel)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                FirstNameEntry.Focus();
            });
        }
    }
}