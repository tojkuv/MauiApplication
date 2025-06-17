using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.NotificationService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationService notificationService, ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { Status = "Healthy", Service = "Notification", Timestamp = DateTime.UtcNow });
    }

    // Core notification operations
    [HttpPost("send")]
    public async Task<ActionResult<NotificationDto>> SendNotification([FromBody] SendNotificationRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var notification = await _notificationService.CreateNotificationAsync(request, userId);
            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("send/bulk")]
    public async Task<ActionResult<List<NotificationDto>>> SendBulkNotification([FromBody] BulkNotificationRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var notifications = await _notificationService.CreateBulkNotificationAsync(request, userId);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk notification");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("push")]
    public async Task<ActionResult> SendPushNotification([FromBody] PushNotificationDto request)
    {
        try
        {
            var success = await _notificationService.SendPushNotificationAsync(request);
            if (success)
                return Ok(new { Message = "Push notification sent successfully" });
            else
                return BadRequest(new { Message = "Failed to send push notification" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification");
            return StatusCode(500, "Internal server error");
        }
    }

    // Notification management
    [HttpGet("{notificationId}")]
    public async Task<ActionResult<NotificationDto>> GetNotification(Guid notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var notification = await _notificationService.GetNotificationAsync(notificationId, userId);
            if (notification == null)
                return NotFound();

            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification {NotificationId}", notificationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("history")]
    public async Task<ActionResult<NotificationHistoryResponseDto>> GetNotificationHistory([FromBody] NotificationHistoryRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            // Override userId in request to ensure user can only see their own notifications
            request.UserId = userId;
            
            var history = await _notificationService.GetNotificationHistoryAsync(request, userId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification history");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("unread")]
    public async Task<ActionResult<List<NotificationDto>>> GetUnreadNotifications([FromQuery] int maxCount = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var notifications = await _notificationService.GetUnreadNotificationsAsync(userId, maxCount);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notifications");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("unread/count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { Count = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            return StatusCode(500, "Internal server error");
        }
    }

    // Mark notifications
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult> MarkAsRead(Guid notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _notificationService.MarkAsReadAsync(notificationId, userId);
            if (!success)
                return NotFound();

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("read/multiple")]
    public async Task<ActionResult> MarkMultipleAsRead([FromBody] MarkNotificationRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _notificationService.MarkMultipleAsReadAsync(request.NotificationIds, userId);
            if (!success)
                return BadRequest("Failed to mark notifications as read");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking multiple notifications as read");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("read/all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _notificationService.MarkAllAsReadAsync(userId);
            if (!success)
                return BadRequest("Failed to mark all notifications as read");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{notificationId}")]
    public async Task<ActionResult> DeleteNotification(Guid notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);
            if (!success)
                return NotFound();

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Device token management
    [HttpPost("devices/register")]
    public async Task<ActionResult<DeviceTokenDto>> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var deviceToken = await _notificationService.RegisterDeviceTokenAsync(request, userId);
            return Ok(deviceToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device token");
            return StatusCode(500, "Internal server error");
        }
    }

    // User preferences
    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetUserPreferences()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var preferences = await _notificationService.GetUserPreferencesAsync(userId);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user preferences");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("preferences")]
    public async Task<ActionResult> UpdateUserPreferences([FromBody] NotificationPreferencesDto preferences)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            preferences.UserId = userId; // Ensure correct user ID
            
            var success = await _notificationService.UpdateUserPreferencesAsync(preferences, userId);
            if (!success)
                return BadRequest("Failed to update preferences");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user preferences");
            return StatusCode(500, "Internal server error");
        }
    }

    // Quick notification endpoints
    [HttpPost("notify/user/{targetUserId}")]
    public async Task<ActionResult> NotifyUser(Guid targetUserId, [FromBody] QuickNotificationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            await _notificationService.NotifyUserAsync(targetUserId, request.Title, request.Message, request.Type);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending quick notification to user {UserId}", targetUserId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("notify/project/{projectId}")]
    public async Task<ActionResult> NotifyProjectMembers(Guid projectId, [FromBody] QuickNotificationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            await _notificationService.NotifyProjectMembersAsync(projectId, request.Title, request.Message, request.Type);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to project {ProjectId} members", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("notify/task/{taskId}")]
    public async Task<ActionResult> NotifyTaskAssignee(Guid taskId, [FromBody] QuickNotificationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            await _notificationService.NotifyTaskAssigneeAsync(taskId, request.Title, request.Message);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to task {TaskId} assignee", taskId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Admin endpoints
    [HttpPost("system")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SendSystemNotification([FromBody] SystemNotificationRequest request)
    {
        try
        {
            var success = await _notificationService.SendSystemNotificationAsync(
                request.Title, 
                request.Message, 
                request.UserIds);
                
            if (success)
                return Ok(new { Message = "System notification sent successfully" });
            else
                return BadRequest("Failed to send system notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending system notification");
            return StatusCode(500, "Internal server error");
        }
    }

    // Test endpoint
    [HttpGet("test")]
    public async Task<ActionResult> TestEndpoint()
    {
        try
        {
            var userId = GetCurrentUserId();
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            
            return Ok(new 
            { 
                Message = "Notification Service is working",
                UserId = userId,
                UnreadCount = unreadCount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test endpoint error");
            return StatusCode(500, ex.Message);
        }
    }
}

// Request models
public class QuickNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
}

public class SystemNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<Guid>? UserIds { get; set; }
}