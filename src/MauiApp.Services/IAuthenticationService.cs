namespace MauiApp.Services;

public interface IAuthenticationService
{
    Task<bool> IsAuthenticatedAsync();
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<string?> GetCurrentUserIdAsync();
    Task<string?> GetAuthTokenAsync();
    Task RefreshTokenAsync();
}