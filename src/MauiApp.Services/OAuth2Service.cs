using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MauiApp.Services;

public class OAuth2Service : IOAuth2Service
{
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<OAuth2Service> _logger;
    private readonly OidcClient _oidcClient;
    
    private const string AccessTokenKey = "oauth2_access_token";
    private const string RefreshTokenKey = "oauth2_refresh_token";
    private const string IdTokenKey = "oauth2_id_token";
    private const string ExpiresAtKey = "oauth2_expires_at";

    public OAuth2Service(
        ISecureStorageService secureStorage,
        ILogger<OAuth2Service> logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;

        // Configure OAuth2 options for Azure AD B2C
        var options = new OidcClientOptions
        {
            Authority = "https://mauiapp.b2clogin.com/mauiapp.onmicrosoft.com/B2C_1_signupsignin/v2.0",
            ClientId = "your-azure-ad-b2c-client-id",
            Scope = "openid profile email offline_access api://your-api-scope/access",
            RedirectUri = "mauiapp://authenticated",
            Browser = new MauiAuthenticationBrowser(),
            Policy = new Policy
            {
                RequireIdentityTokenSignature = false
            }
        };

        _oidcClient = new OidcClient(options);
    }

    public async Task<LoginResult> LoginAsync()
    {
        try
        {
            _logger.LogInformation("Starting OAuth2 login");
            
            var result = await _oidcClient.LoginAsync();
            
            if (result.IsError)
            {
                _logger.LogError("OAuth2 login failed: {Error}", result.Error);
                return result;
            }

            // Store tokens securely
            await _secureStorage.SetAsync(AccessTokenKey, result.AccessToken);
            await _secureStorage.SetAsync(IdTokenKey, result.IdentityToken);
            
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, result.RefreshToken);
            }

            if (result.AccessTokenExpiration.HasValue)
            {
                await _secureStorage.SetAsync(ExpiresAtKey, result.AccessTokenExpiration.Value.ToString("O"));
            }

            _logger.LogInformation("OAuth2 login successful");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 login");
            return new LoginResult
            {
                IsError = true,
                Error = ex.Message
            };
        }
    }

    public async Task<LoginResult> LoginWithProviderAsync(string provider)
    {
        try
        {
            _logger.LogInformation("Starting OAuth2 login with provider: {Provider}", provider);
            
            // For different providers, you would modify the authority URL
            var extraParameters = new Dictionary<string, string>();
            
            switch (provider.ToLowerInvariant())
            {
                case "google":
                    extraParameters.Add("domain_hint", "gmail.com");
                    break;
                case "microsoft":
                    extraParameters.Add("domain_hint", "live.com");
                    break;
                case "apple":
                    extraParameters.Add("domain_hint", "apple.com");
                    break;
            }

            var result = await _oidcClient.LoginAsync(new LoginRequest
            {
                FrontChannelExtraParameters = extraParameters
            });

            if (result.IsError)
            {
                _logger.LogError("OAuth2 login with provider {Provider} failed: {Error}", provider, result.Error);
                return result;
            }

            // Store tokens securely
            await _secureStorage.SetAsync(AccessTokenKey, result.AccessToken);
            await _secureStorage.SetAsync(IdTokenKey, result.IdentityToken);
            
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, result.RefreshToken);
            }

            if (result.AccessTokenExpiration.HasValue)
            {
                await _secureStorage.SetAsync(ExpiresAtKey, result.AccessTokenExpiration.Value.ToString("O"));
            }

            _logger.LogInformation("OAuth2 login with provider {Provider} successful", provider);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 login with provider: {Provider}", provider);
            return new LoginResult
            {
                IsError = true,
                Error = ex.Message
            };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Starting OAuth2 logout");
            
            // Get the ID token for logout
            var idToken = await _secureStorage.GetAsync(IdTokenKey);
            
            if (!string.IsNullOrEmpty(idToken))
            {
                await _oidcClient.LogoutAsync(new LogoutRequest
                {
                    IdTokenHint = idToken
                });
            }

            // Clear stored tokens
            await _secureStorage.RemoveAsync(AccessTokenKey);
            await _secureStorage.RemoveAsync(RefreshTokenKey);
            await _secureStorage.RemoveAsync(IdTokenKey);
            await _secureStorage.RemoveAsync(ExpiresAtKey);

            _logger.LogInformation("OAuth2 logout completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth2 logout");
            throw;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var token = await _secureStorage.GetAsync(AccessTokenKey);
            
            if (string.IsNullOrEmpty(token))
                return null;

            // Check if token is expired
            var expiresAtString = await _secureStorage.GetAsync(ExpiresAtKey);
            if (!string.IsNullOrEmpty(expiresAtString) && 
                DateTime.TryParse(expiresAtString, out var expiresAt) && 
                expiresAt <= DateTime.UtcNow.AddMinutes(-5)) // 5 minute buffer
            {
                // Try to refresh the token
                var refreshed = await RefreshTokenAsync();
                if (refreshed)
                {
                    return await _secureStorage.GetAsync(AccessTokenKey);
                }
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token");
            return null;
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token available");
                return false;
            }

            _logger.LogInformation("Refreshing access token");

            var result = await _oidcClient.RefreshTokenAsync(refreshToken);
            
            if (result.IsError)
            {
                _logger.LogError("Token refresh failed: {Error}", result.Error);
                return false;
            }

            // Update stored tokens
            await _secureStorage.SetAsync(AccessTokenKey, result.AccessToken);
            await _secureStorage.SetAsync(IdTokenKey, result.IdentityToken);
            
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, result.RefreshToken);
            }

            if (result.AccessTokenExpiration.HasValue)
            {
                await _secureStorage.SetAsync(ExpiresAtKey, result.AccessTokenExpiration.Value.ToString("O"));
            }

            _logger.LogInformation("Token refresh successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return false;
        }
    }

    public async Task<UserInfo?> GetUserInfoAsync()
    {
        try
        {
            var idToken = await _secureStorage.GetAsync(IdTokenKey);
            if (string.IsNullOrEmpty(idToken))
                return null;

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);

            var userInfo = new UserInfo
            {
                Id = GetClaimValue(token.Claims, "sub") ?? GetClaimValue(token.Claims, "oid") ?? string.Empty,
                Email = GetClaimValue(token.Claims, "email") ?? GetClaimValue(token.Claims, "preferred_username") ?? string.Empty,
                Name = GetClaimValue(token.Claims, "name") ?? string.Empty,
                FirstName = GetClaimValue(token.Claims, "given_name") ?? string.Empty,
                LastName = GetClaimValue(token.Claims, "family_name") ?? string.Empty,
                AvatarUrl = GetClaimValue(token.Claims, "picture") ?? string.Empty
            };

            // Extract roles
            var roleClaims = token.Claims.Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role);
            userInfo.Roles = roleClaims.Select(c => c.Value).ToList();

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return null;
        }
    }

    public bool IsAuthenticated()
    {
        try
        {
            var task = _secureStorage.GetAsync(AccessTokenKey);
            task.Wait();
            return !string.IsNullOrEmpty(task.Result);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetClaimValue(IEnumerable<Claim> claims, string claimType)
    {
        return claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }
}

// Custom browser implementation for MAUI
public class MauiAuthenticationBrowser : IBrowser
{
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await WebAuthenticator.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = new Uri(options.StartUrl),
                    CallbackUrl = new Uri(options.EndUrl)
                });

            var url = new Uri(options.EndUrl).AddParameters(result.Properties);
            
            return new BrowserResult
            {
                Response = url.ToString(),
                ResultType = BrowserResultType.Success
            };
        }
        catch (TaskCanceledException)
        {
            return new BrowserResult
            {
                ResultType = BrowserResultType.UserCancel
            };
        }
        catch (Exception ex)
        {
            return new BrowserResult
            {
                ResultType = BrowserResultType.UnknownError,
                Error = ex.Message
            };
        }
    }
}

// Extension method for URI parameter handling
public static class UriExtensions
{
    public static Uri AddParameters(this Uri uri, IDictionary<string, string> parameters)
    {
        var uriBuilder = new UriBuilder(uri);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        
        foreach (var param in parameters)
        {
            query[param.Key] = param.Value;
        }
        
        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri;
    }
}