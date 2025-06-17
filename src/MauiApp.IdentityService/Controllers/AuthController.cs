using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.IdentityService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IIdentityService identityService, ILogger<AuthController> logger)
    {
        _identityService = identityService;
        _logger = logger;
    }

    /// <summary>
    /// User login
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);
            
            var ipAddress = GetIpAddress();
            var result = await _identityService.LoginAsync(request, ipAddress);
            
            _logger.LogInformation("Login successful for user: {UserId}", result.User.Id);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for email {Email}: {Message}", request.Email, ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// User registration
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthenticationResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);
            
            var ipAddress = GetIpAddress();
            var result = await _identityService.RegisterAsync(request, ipAddress);
            
            _logger.LogInformation("Registration successful for user: {UserId}", result.User.Id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for email {Email}: {Message}", request.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Refresh access token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _identityService.RefreshTokenAsync(request, ipAddress);
            
            _logger.LogInformation("Token refresh successful");
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "An error occurred during token refresh" });
        }
    }

    /// <summary>
    /// User logout
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromBody] LogoutRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _identityService.LogoutAsync(userId, request.RefreshToken);
            
            _logger.LogInformation("Logout successful for user: {UserId}", userId);
            return Ok(new { message = "Logout successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Revoke all user tokens
    /// </summary>
    [HttpPost("revoke-all")]
    [Authorize]
    public async Task<ActionResult> RevokeAllTokens()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _identityService.RevokeAllTokensAsync(userId);
            
            _logger.LogInformation("All tokens revoked for user: {UserId}", userId);
            return Ok(new { message = "All tokens revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens");
            return StatusCode(500, new { message = "An error occurred while revoking tokens" });
        }
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _identityService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(500, new { message = "An error occurred while getting profile" });
        }
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _identityService.UpdateProfileAsync(userId, request);
            
            _logger.LogInformation("Profile updated for user: {UserId}", userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Profile update failed for user {UserId}: {Message}", GetCurrentUserId(), ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new { message = "An error occurred while updating profile" });
        }
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _identityService.ChangePasswordAsync(userId, request);
            
            if (!result)
            {
                return BadRequest(new { message = "Invalid current password" });
            }
            
            _logger.LogInformation("Password changed for user: {UserId}", userId);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "An error occurred while changing password" });
        }
    }

    /// <summary>
    /// Forgot password
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _identityService.ForgotPasswordAsync(request);
            
            _logger.LogInformation("Password reset requested for email: {Email}", request.Email);
            return Ok(new { message = "If the email exists, a password reset link has been sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request");
            return StatusCode(500, new { message = "An error occurred while processing the request" });
        }
    }

    /// <summary>
    /// Reset password
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var result = await _identityService.ResetPasswordAsync(request);
            
            if (!result)
            {
                return BadRequest(new { message = "Invalid reset token or email" });
            }
            
            _logger.LogInformation("Password reset successful for email: {Email}", request.Email);
            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(500, new { message = "An error occurred while resetting password" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token");
        }
        return userId;
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}