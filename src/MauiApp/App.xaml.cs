using MauiApp.Services;

namespace MauiApp;

public partial class App : Application
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly IDatabaseService _databaseService;

    public App(IAuthenticationService authenticationService, INavigationService navigationService, IDatabaseService databaseService)
    {
        InitializeComponent();
        
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _databaseService = databaseService;
        
        MainPage = new AppShell();
        
        // Initialize database and navigate to initial page
        _ = InitializeAppAsync();
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            // Initialize database first
            await _databaseService.InitializeAsync();
            
            // Then navigate to initial page
            await NavigateToInitialPage();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing app: {ex.Message}");
            // Continue to navigation even if database init fails
            await NavigateToInitialPage();
        }
    }

    private async Task NavigateToInitialPage()
    {
        try
        {
            // Check if user is authenticated
            var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
            
            if (isAuthenticated)
            {
                // User is logged in, go to dashboard
                await _navigationService.NavigateToAsync("//dashboard/overview");
            }
            else
            {
                // User is not logged in, go to welcome page
                await _navigationService.NavigateToAsync("WelcomePage");
            }
        }
        catch (Exception ex)
        {
            // If there's an error, default to welcome page
            System.Diagnostics.Debug.WriteLine($"Error checking authentication state: {ex.Message}");
            await _navigationService.NavigateToAsync("WelcomePage");
        }
    }
}