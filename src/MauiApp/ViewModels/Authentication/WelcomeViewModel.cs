using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;

namespace MauiApp.ViewModels.Authentication;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<WelcomeViewModel> _logger;

    [ObservableProperty]
    private bool isBusy;

    public WelcomeViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        ILogger<WelcomeViewModel> logger)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task NavigateToLogin()
    {
        try
        {
            await _navigationService.NavigateToAsync("LoginPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to login page");
            await Shell.Current.DisplayAlert("Error", "Unable to navigate to login page", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToRegister()
    {
        try
        {
            await _navigationService.NavigateToAsync("RegisterPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to register page");
            await Shell.Current.DisplayAlert("Error", "Unable to navigate to register page", "OK");
        }
    }

    [RelayCommand]
    private async Task LoginWithProvider(string provider)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            _logger.LogInformation("Attempting OAuth2 login with provider: {Provider}", provider);

            var result = await _authenticationService.LoginWithProviderAsync(provider);

            if (result.IsSuccess)
            {
                _logger.LogInformation("OAuth2 login successful with provider: {Provider}", provider);
                await _navigationService.NavigateToAsync("//MainPage");
            }
            else
            {
                _logger.LogWarning("OAuth2 login failed with provider {Provider}: {Error}", provider, result.ErrorMessage);
                await Shell.Current.DisplayAlert("Login Failed", 
                    result.ErrorMessage ?? "Authentication failed. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 login with provider: {Provider}", provider);
            await Shell.Current.DisplayAlert("Error", 
                "An unexpected error occurred during authentication. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenTerms()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://yourcompany.com/terms"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening terms of service");
            await Shell.Current.DisplayAlert("Error", "Unable to open Terms of Service", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenPrivacy()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://yourcompany.com/privacy"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening privacy policy");
            await Shell.Current.DisplayAlert("Error", "Unable to open Privacy Policy", "OK");
        }
    }
}