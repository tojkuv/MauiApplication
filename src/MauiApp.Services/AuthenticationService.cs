using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IDataService _dataService;
    private readonly IApiService _apiService;
    private readonly ILogger<AuthenticationService> _logger;
    
    private const string AuthTokenKey = "auth_token";
    private const string UserIdKey = "user_id";
    private const string RefreshTokenKey = "refresh_token";

    public AuthenticationService(
        IDataService dataService, 
        IApiService apiService, 
        ILogger<AuthenticationService> logger)
    {
        _dataService = dataService;
        _apiService = apiService;
        _logger = logger;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await GetAuthTokenAsync();
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            _logger.LogInformation("Attempting login for user: {Email}", email);
            
            var loginRequest = new { Email = email, Password = password };
            var response = await _apiService.PostAsync<LoginResponse>("/auth/login", loginRequest);
            
            if (response?.Token != null)
            {
                await _dataService.SaveItemAsync(AuthTokenKey, response.Token);
                await _dataService.SaveItemAsync(UserIdKey, response.UserId);
                
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    await _dataService.SaveItemAsync(RefreshTokenKey, response.RefreshToken);
                }
                
                _logger.LogInformation("Login successful for user: {Email}", email);
                return true;
            }
            
            _logger.LogWarning("Login failed for user: {Email}", email);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Email}", email);
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logging out user");
            
            await _dataService.RemoveItemAsync(AuthTokenKey);
            await _dataService.RemoveItemAsync(UserIdKey);
            await _dataService.RemoveItemAsync(RefreshTokenKey);
            
            _logger.LogInformation("Logout completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    public async Task<string?> GetCurrentUserIdAsync()
    {
        try
        {
            return await _dataService.GetItemAsync<string>(UserIdKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user ID");
            return null;
        }
    }

    public async Task<string?> GetAuthTokenAsync()
    {
        try
        {
            return await _dataService.GetItemAsync<string>(AuthTokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auth token");
            return null;
        }
    }

    public async Task RefreshTokenAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing auth token");
            
            var refreshToken = await _dataService.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new InvalidOperationException("No refresh token available");
            }
            
            var refreshRequest = new { RefreshToken = refreshToken };
            var response = await _apiService.PostAsync<LoginResponse>("/auth/refresh", refreshRequest);
            
            if (response?.Token != null)
            {
                await _dataService.SaveItemAsync(AuthTokenKey, response.Token);
                
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    await _dataService.SaveItemAsync(RefreshTokenKey, response.RefreshToken);
                }
                
                _logger.LogInformation("Token refresh successful");
            }
            else
            {
                throw new InvalidOperationException("Failed to refresh token");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            throw;
        }
    }
}

public class LoginResponse
{
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}