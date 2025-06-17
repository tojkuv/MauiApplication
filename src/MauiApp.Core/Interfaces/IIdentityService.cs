using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;

namespace MauiApp.Core.Interfaces;

public interface IIdentityService
{
    Task<AuthenticationResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthenticationResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null);
    Task<bool> LogoutAsync(Guid userId, string refreshToken);
    Task<bool> RevokeAllTokensAsync(Guid userId);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    Task<bool> VerifyEmailAsync(string email, string token);
    Task<bool> ResendEmailVerificationAsync(string email);
}

public interface IJwtService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    bool ValidateAccessToken(string token);
    Guid? GetUserIdFromToken(string token);
    string? GetJwtIdFromToken(string token);
}