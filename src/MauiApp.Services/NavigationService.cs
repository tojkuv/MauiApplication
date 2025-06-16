using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;

    public NavigationService(ILogger<NavigationService> logger)
    {
        _logger = logger;
    }

    public async Task NavigateToAsync(string route)
    {
        try
        {
            _logger.LogInformation("Navigating to route: {Route}", route);
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to route: {Route}", route);
            throw;
        }
    }

    public async Task NavigateToAsync(string route, IDictionary<string, object> parameters)
    {
        try
        {
            _logger.LogInformation("Navigating to route: {Route} with parameters", route);
            await Shell.Current.GoToAsync(route, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to route: {Route} with parameters", route);
            throw;
        }
    }

    public async Task GoBackAsync()
    {
        try
        {
            _logger.LogInformation("Going back");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error going back");
            throw;
        }
    }

    public async Task GoBackToRootAsync()
    {
        try
        {
            _logger.LogInformation("Going back to root");
            await Shell.Current.GoToAsync("//");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error going back to root");
            throw;
        }
    }
}