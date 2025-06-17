namespace MauiApp.Services;

public interface IAuthenticationService
{
    Task<bool> IsAuthenticatedAsync();
    Task<AuthenticationResult> LoginAsync(string email, string password);
    Task<AuthenticationResult> LoginWithOAuth2Async();
    Task<AuthenticationResult> LoginWithProviderAsync(string provider);
    Task<AuthenticationResult> RegisterAsync(string email, string password, string firstName, string lastName);
    Task LogoutAsync();
    Task<string?> GetCurrentUserIdAsync();
    Task<string?> GetAuthTokenAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<bool> RefreshTokenAsync();
    event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;
}

public class AuthenticationStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; set; }
    public UserInfo? User { get; set; }
}