using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IDataService _dataService;
    private readonly IApiService _apiService;
    private readonly IOAuth2Service _oauth2Service;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<AuthenticationService> _logger;
    
    private const string AuthTokenKey = "auth_token";
    private const string UserIdKey = "user_id";
    private const string RefreshTokenKey = "refresh_token";
    private const string AuthMethodKey = "auth_method";

    public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

    public AuthenticationService(
        IDataService dataService, 
        IApiService apiService,
        IOAuth2Service oauth2Service,
        ISecureStorageService secureStorage,
        ILogger<AuthenticationService> logger)
    {
        _dataService = dataService;
        _apiService = apiService;
        _oauth2Service = oauth2Service;
        _secureStorage = secureStorage;
        _logger = logger;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                return _oauth2Service.IsAuthenticated();
            }
            else
            {
                var token = await GetAuthTokenAsync();
                return !string.IsNullOrEmpty(token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        try
        {
            _logger.LogInformation("Attempting traditional login for user: {Email}", email);
            
            var loginRequest = new { Email = email, Password = password };
            var response = await _apiService.PostAsync<LoginResponse>("/auth/login", loginRequest);
            
            if (response?.Token != null)
            {
                await _secureStorage.SetAsync(AuthTokenKey, response.Token);
                await _secureStorage.SetAsync(UserIdKey, response.UserId ?? string.Empty);
                await _secureStorage.SetAsync(AuthMethodKey, "traditional");
                
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    await _secureStorage.SetAsync(RefreshTokenKey, response.RefreshToken);
                }
                
                var userInfo = new UserInfo
                {
                    Id = response.UserId ?? string.Empty,
                    Email = email
                };

                var result = new AuthenticationResult
                {
                    IsSuccess = true,
                    User = userInfo,
                    AccessToken = response.Token,
                    RefreshToken = response.RefreshToken,
                    ExpiresAt = response.ExpiresAt
                };

                AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
                {
                    IsAuthenticated = true,
                    User = userInfo
                });

                _logger.LogInformation("Traditional login successful for user: {Email}", email);
                return result;
            }
            
            _logger.LogWarning("Traditional login failed for user: {Email}", email);
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid email or password"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during traditional login for user: {Email}", email);
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AuthenticationResult> LoginWithOAuth2Async()
    {
        try
        {
            _logger.LogInformation("Attempting OAuth2 login");
            
            var loginResult = await _oauth2Service.LoginAsync();
            
            if (loginResult.IsError)
            {
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = loginResult.Error
                };
            }

            await _secureStorage.SetAsync(AuthMethodKey, "oauth2");
            
            var userInfo = await _oauth2Service.GetUserInfoAsync();
            
            var result = new AuthenticationResult
            {
                IsSuccess = true,
                User = userInfo,
                AccessToken = loginResult.AccessToken,
                RefreshToken = loginResult.RefreshToken,
                ExpiresAt = loginResult.AccessTokenExpiration
            };

            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = true,
                User = userInfo
            });

            _logger.LogInformation("OAuth2 login successful");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 login");
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AuthenticationResult> LoginWithProviderAsync(string provider)
    {
        try
        {
            _logger.LogInformation("Attempting OAuth2 login with provider: {Provider}", provider);
            
            var loginResult = await _oauth2Service.LoginWithProviderAsync(provider);
            
            if (loginResult.IsError)
            {
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = loginResult.Error
                };
            }

            await _secureStorage.SetAsync(AuthMethodKey, "oauth2");
            
            var userInfo = await _oauth2Service.GetUserInfoAsync();
            
            var result = new AuthenticationResult
            {
                IsSuccess = true,
                User = userInfo,
                AccessToken = loginResult.AccessToken,
                RefreshToken = loginResult.RefreshToken,
                ExpiresAt = loginResult.AccessTokenExpiration
            };

            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = true,
                User = userInfo
            });

            _logger.LogInformation("OAuth2 login with provider {Provider} successful", provider);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 login with provider: {Provider}", provider);
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AuthenticationResult> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        try
        {
            _logger.LogInformation("Attempting registration for user: {Email}", email);
            
            var registerRequest = new 
            { 
                Email = email, 
                Password = password, 
                FirstName = firstName, 
                LastName = lastName 
            };
            
            var response = await _apiService.PostAsync<LoginResponse>("/auth/register", registerRequest);
            
            if (response?.Token != null)
            {
                await _secureStorage.SetAsync(AuthTokenKey, response.Token);
                await _secureStorage.SetAsync(UserIdKey, response.UserId ?? string.Empty);
                await _secureStorage.SetAsync(AuthMethodKey, "traditional");
                
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    await _secureStorage.SetAsync(RefreshTokenKey, response.RefreshToken);
                }
                
                var userInfo = new UserInfo
                {
                    Id = response.UserId ?? string.Empty,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Name = $"{firstName} {lastName}".Trim()
                };

                var result = new AuthenticationResult
                {
                    IsSuccess = true,
                    User = userInfo,
                    AccessToken = response.Token,
                    RefreshToken = response.RefreshToken,
                    ExpiresAt = response.ExpiresAt
                };

                AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
                {
                    IsAuthenticated = true,
                    User = userInfo
                });

                _logger.LogInformation("Registration successful for user: {Email}", email);
                return result;
            }
            
            _logger.LogWarning("Registration failed for user: {Email}", email);
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Registration failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Email}", email);
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logging out user");
            
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                await _oauth2Service.LogoutAsync();
            }
            
            // Clear all stored authentication data
            await _secureStorage.RemoveAsync(AuthTokenKey);
            await _secureStorage.RemoveAsync(UserIdKey);
            await _secureStorage.RemoveAsync(RefreshTokenKey);
            await _secureStorage.RemoveAsync(AuthMethodKey);

            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = false,
                User = null
            });
            
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
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                var userInfo = await _oauth2Service.GetUserInfoAsync();
                return userInfo?.Id;
            }
            else
            {
                return await _secureStorage.GetAsync(UserIdKey);
            }
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
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                return await _oauth2Service.GetAccessTokenAsync();
            }
            else
            {
                return await _secureStorage.GetAsync(AuthTokenKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auth token");
            return null;
        }
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                return await _oauth2Service.GetUserInfoAsync();
            }
            else
            {
                var userId = await _secureStorage.GetAsync(UserIdKey);
                if (string.IsNullOrEmpty(userId))
                    return null;

                // For traditional auth, try to get user info from API
                try
                {
                    var userResponse = await _apiService.GetAsync<UserInfoResponse>($"/auth/user/{userId}");
                    if (userResponse != null)
                    {
                        return new UserInfo
                        {
                            Id = userResponse.Id,
                            Email = userResponse.Email,
                            FirstName = userResponse.FirstName,
                            LastName = userResponse.LastName,
                            Name = userResponse.Name,
                            AvatarUrl = userResponse.AvatarUrl
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch user info from API, using cached data");
                }

                // Fallback to basic user info
                return new UserInfo
                {
                    Id = userId
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing auth token");
            
            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            
            if (authMethod == "oauth2")
            {
                return await _oauth2Service.RefreshTokenAsync();
            }
            else
            {
                var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("No refresh token available");
                    return false;
                }
                
                var refreshRequest = new { RefreshToken = refreshToken };
                var response = await _apiService.PostAsync<LoginResponse>("/auth/refresh", refreshRequest);
                
                if (response?.Token != null)
                {
                    await _secureStorage.SetAsync(AuthTokenKey, response.Token);
                    
                    if (!string.IsNullOrEmpty(response.RefreshToken))
                    {
                        await _secureStorage.SetAsync(RefreshTokenKey, response.RefreshToken);
                    }
                    
                    _logger.LogInformation("Token refresh successful");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to refresh token");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return false;
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

public class UserInfoResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}