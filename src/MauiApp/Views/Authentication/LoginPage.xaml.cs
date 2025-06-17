using MauiApp.ViewModels.Authentication;

namespace MauiApp.Views.Authentication;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Focus on email entry when page appears
        if (BindingContext is LoginViewModel viewModel)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                EmailEntry.Focus();
            });
        }
    }
}