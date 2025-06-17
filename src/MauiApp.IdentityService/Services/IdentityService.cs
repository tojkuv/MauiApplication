using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.IdentityService.Data;

namespace MauiApp.IdentityService.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly IdentityDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService,
        IdentityDbContext context,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthenticationResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new UnauthorizedAccessException("Account is not active");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            JwtId = _jwtService.GetJwtIdFromToken(accessToken)!,
            UserId = user.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            DeviceInfo = request.DeviceInfo,
            IpAddress = ipAddress
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthenticationResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = MapToUserDto(user)
        };
    }

    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true, // For MVP, skip email verification
            Status = UserStatus.Active
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Add default role
        await _userManager.AddToRoleAsync(user, "User");

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            JwtId = _jwtService.GetJwtIdFromToken(accessToken)!,
            UserId = user.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            DeviceInfo = request.DeviceInfo,
            IpAddress = ipAddress
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthenticationResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = MapToUserDto(user)
        };
    }

    public async Task<AuthenticationResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null)
    {
        var jwtId = _jwtService.GetJwtIdFromToken(request.AccessToken);
        if (jwtId == null)
        {
            throw new UnauthorizedAccessException("Invalid access token");
        }

        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.JwtId == jwtId);

        if (refreshToken == null || refreshToken.IsUsed || refreshToken.IsInvalidated)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (refreshToken.ExpiryDate <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token has expired");
        }

        // Mark current token as used
        refreshToken.IsUsed = true;
        await _context.SaveChangesAsync();

        // Generate new tokens
        var user = refreshToken.User;
        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Store new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            JwtId = _jwtService.GetJwtIdFromToken(newAccessToken)!,
            UserId = user.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            DeviceInfo = refreshToken.DeviceInfo,
            IpAddress = ipAddress
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthenticationResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = MapToUserDto(user)
        };
    }

    public async Task<bool> LogoutAsync(Guid userId, string refreshToken)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.Token == refreshToken);

        if (token != null)
        {
            token.IsInvalidated = true;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> RevokeAllTokensAsync(Guid userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsInvalidated)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsInvalidated = true;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return MapToUserDto(user);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded;
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal that the user doesn't exist
            return true;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        
        // TODO: Send email with reset token
        // For MVP, we'll just return true
        
        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded;
    }

    public async Task<bool> VerifyEmailAsync(string email, string token)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task<bool> ResendEmailVerificationAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.EmailConfirmed)
        {
            return false;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        
        // TODO: Send verification email
        // For MVP, we'll just return true
        
        return true;
    }

    private static UserDto MapToUserDto(ApplicationUser user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }
}