using IdentityModel.OidcClient;

namespace MauiApp.Services;

public interface IOAuth2Service
{
    Task<LoginResult> LoginAsync();
    Task<LoginResult> LoginWithProviderAsync(string provider);
    Task LogoutAsync();
    Task<string?> GetAccessTokenAsync();
    Task<bool> RefreshTokenAsync();
    Task<UserInfo?> GetUserInfoAsync();
    bool IsAuthenticated();
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public UserInfo? User { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}