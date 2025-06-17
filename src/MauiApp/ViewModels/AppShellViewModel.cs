using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;

namespace MauiApp.ViewModels;

public partial class AppShellViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<AppShellViewModel> _logger;

    [ObservableProperty]
    private string userName = "Loading...";

    [ObservableProperty]
    private string userRole = "";

    [ObservableProperty]
    private string currentProject = "";

    [ObservableProperty]
    private string userAvatarUrl = "user_avatar.png";

    public AppShellViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        ILogger<AppShellViewModel> logger)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _logger = logger;

        // Subscribe to authentication state changes
        _authenticationService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        
        // Load user information
        _ = LoadUserInformationAsync();
    }

    [RelayCommand]
    private async Task NavigateToSettings()
    {
        try
        {
            await _navigationService.NavigateToAsync("SettingsPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to settings");
            await Shell.Current.DisplayAlert("Error", "Unable to open settings", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToHelp()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://help.projecthub.com"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening help page");
            await Shell.Current.DisplayAlert("Error", "Unable to open help page", "OK");
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        try
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Sign Out", 
                "Are you sure you want to sign out?", 
                "Sign Out", 
                "Cancel");

            if (confirm)
            {
                _logger.LogInformation("User initiated logout");
                
                await _authenticationService.LogoutAsync();
                
                // Navigate to welcome page
                await _navigationService.NavigateToAsync("//WelcomePage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            await Shell.Current.DisplayAlert("Error", "Unable to sign out. Please try again.", "OK");
        }
    }

    private async Task LoadUserInformationAsync()
    {
        try
        {
            var userInfo = await _authenticationService.GetCurrentUserAsync();
            
            if (userInfo != null)
            {
                UserName = !string.IsNullOrEmpty(userInfo.Name) 
                    ? userInfo.Name 
                    : $"{userInfo.FirstName} {userInfo.LastName}".Trim();
                    
                if (string.IsNullOrEmpty(UserName))
                {
                    UserName = userInfo.Email;
                }

                // Set role from user roles (take first role or default)
                UserRole = userInfo.Roles?.FirstOrDefault() ?? "Team Member";
                
                // Set avatar URL if available
                if (!string.IsNullOrEmpty(userInfo.AvatarUrl))
                {
                    UserAvatarUrl = userInfo.AvatarUrl;
                }

                // TODO: Load current project information from API
                CurrentProject = "📊 Loading...";
                await LoadCurrentProjectAsync();
            }
            else
            {
                UserName = "Guest User";
                UserRole = "Guest";
                CurrentProject = "No Project Selected";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user information");
            UserName = "Unknown User";
            UserRole = "User";
            CurrentProject = "";
        }
    }

    private async Task LoadCurrentProjectAsync()
    {
        try
        {
            // TODO: Implement API call to get user's current/active project
            // For now, use a placeholder
            await Task.Delay(1000); // Simulate API call
            CurrentProject = "📱 Mobile App Project";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading current project");
            CurrentProject = "No Active Project";
        }
    }

    private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
    {
        // Reload user information when authentication state changes
        _ = LoadUserInformationAsync();
    }
}