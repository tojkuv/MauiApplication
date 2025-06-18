using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace MauiApp.Services;

public class AuthenticationHandler : DelegatingHandler
{
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<AuthenticationHandler> _logger;
    
    private const string AuthTokenKey = "auth_token";
    private const string AuthMethodKey = "auth_method";

    public AuthenticationHandler(ISecureStorageService secureStorage, ILogger<AuthenticationHandler> logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            // Skip adding auth header for auth endpoints
            if (request.RequestUri?.LocalPath.Contains("/auth/") == true)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var authMethod = await _secureStorage.GetAsync(AuthMethodKey);
            string? token = null;
            
            if (authMethod == "oauth2")
            {
                // For OAuth2, we would need access to OAuth2Service
                // For now, skip OAuth2 token handling
                _logger.LogDebug("OAuth2 token handling not implemented in handler");
            }
            else
            {
                token = await _secureStorage.GetAsync(AuthTokenKey);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Added Bearer token to request for {Uri}", request.RequestUri);
            }
            else
            {
                _logger.LogDebug("No auth token available for request to {Uri}", request.RequestUri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding authentication header to request");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}