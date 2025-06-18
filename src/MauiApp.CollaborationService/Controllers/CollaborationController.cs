using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.CollaborationService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CollaborationController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<CollaborationController> _logger;

    public CollaborationController(IChatService chatService, ILogger<CollaborationController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { Status = "Healthy", Service = "Collaboration", Timestamp = DateTime.UtcNow });
    }

    // Chat History and Messages
    [HttpGet("projects/{projectId}/messages")]
    public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetChatHistory(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? before = null,
        [FromQuery] DateTime? after = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            var userId = GetUserId();
            var request = new ChatHistoryRequest
            {
                Page = page,
                PageSize = pageSize,
                Before = before,
                After = after,
                SearchTerm = searchTerm
            };

            var messages = await _chatService.GetChatHistoryAsync(projectId, userId, request);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat history for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("messages/{messageId}")]
    public async Task<ActionResult<ChatMessageDto>> GetMessage(Guid messageId)
    {
        try
        {
            var userId = GetUserId();
            var message = await _chatService.GetMessageByIdAsync(messageId, userId);
            
            if (message == null)
            {
                return NotFound("Message not found");
            }

            return Ok(message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("projects/{projectId}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        Guid projectId,
        [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = GetUserId();
            var message = await _chatService.SendMessageAsync(
                projectId, 
                request.Content, 
                userId, 
                request.ReplyToMessageId, 
                request.MessageType ?? "text");

            return Created($"/api/collaboration/messages/{message.Id}", message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("messages/{messageId}")]
    public async Task<ActionResult<ChatMessageDto>> EditMessage(
        Guid messageId,
        [FromBody] EditMessageRequest request)
    {
        try
        {
            var userId = GetUserId();
            var message = await _chatService.EditMessageAsync(messageId, request.Content, userId);
            return Ok(message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to edit this message");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        try
        {
            var userId = GetUserId();
            var deleted = await _chatService.DeleteMessageAsync(messageId, userId);
            
            if (!deleted)
            {
                return NotFound("Message not found");
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to delete this message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Message Reactions
    [HttpPost("messages/{messageId}/reactions")]
    public async Task<ActionResult<MessageReactionDto>> AddReaction(
        Guid messageId,
        [FromBody] AddReactionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var reaction = await _chatService.AddReactionAsync(messageId, request.Reaction, userId);
            return Created($"/api/collaboration/messages/{messageId}/reactions", reaction);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this message");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reaction to message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("messages/{messageId}/reactions/{reaction}")]
    public async Task<IActionResult> RemoveReaction(Guid messageId, string reaction)
    {
        try
        {
            var userId = GetUserId();
            var removed = await _chatService.RemoveReactionAsync(messageId, reaction, userId);
            
            if (!removed)
            {
                return NotFound("Reaction not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing reaction from message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("messages/{messageId}/reactions")]
    public async Task<ActionResult<IEnumerable<MessageReactionDto>>> GetMessageReactions(Guid messageId)
    {
        try
        {
            var reactions = await _chatService.GetMessageReactionsAsync(messageId);
            return Ok(reactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reactions for message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Read Status
    [HttpPost("messages/{messageId}/read")]
    public async Task<IActionResult> MarkMessageAsRead(Guid messageId)
    {
        try
        {
            var userId = GetUserId();
            await _chatService.MarkMessageAsReadAsync(messageId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("projects/{projectId}/messages/read-all")]
    public async Task<IActionResult> MarkAllMessagesAsRead(Guid projectId)
    {
        try
        {
            var userId = GetUserId();
            await _chatService.MarkAllMessagesAsReadAsync(projectId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all messages as read for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("messages/{messageId}/read-status")]
    public async Task<ActionResult<IEnumerable<MessageReadStatusDto>>> GetMessageReadStatuses(Guid messageId)
    {
        try
        {
            var readStatuses = await _chatService.GetMessageReadStatusesAsync(messageId);
            return Ok(readStatuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting read statuses for message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Unread Count
    [HttpGet("projects/{projectId}/unread-count")]
    public async Task<ActionResult<int>> GetUnreadMessageCount(Guid projectId)
    {
        try
        {
            var userId = GetUserId();
            var count = await _chatService.GetUnreadMessageCountAsync(projectId, userId);
            return Ok(count);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Presence Management
    [HttpPost("projects/{projectId}/presence")]
    public async Task<ActionResult<UserPresenceDto>> UpdatePresence(
        Guid projectId,
        [FromBody] UpdatePresenceRequest request)
    {
        try
        {
            var userId = GetUserId();
            var presence = await _chatService.UpdatePresenceAsync(projectId, userId, request.Status, request.Activity);
            return Ok(presence);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("projects/{projectId}/presence")]
    public async Task<ActionResult<IEnumerable<UserPresenceDto>>> GetProjectPresences(Guid projectId)
    {
        try
        {
            var presences = await _chatService.GetProjectPresencesAsync(projectId);
            return Ok(presences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting presences for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("projects/{projectId}/users/{userId}/presence")]
    public async Task<ActionResult<UserPresenceDto>> GetUserPresence(Guid projectId, Guid userId)
    {
        try
        {
            var presence = await _chatService.GetUserPresenceAsync(projectId, userId);
            
            if (presence == null)
            {
                return NotFound("User presence not found");
            }

            return Ok(presence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user presence for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Project Chat Summary
    [HttpGet("projects/{projectId}/summary")]
    public async Task<ActionResult<ProjectChatSummaryDto>> GetProjectChatSummary(Guid projectId)
    {
        try
        {
            var userId = GetUserId();
            var summary = await _chatService.GetProjectChatSummaryAsync(projectId, userId);
            return Ok(summary);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to this project");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat summary for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Connection Management
    [HttpGet("projects/{projectId}/connections")]
    public async Task<ActionResult<IEnumerable<UserConnectionDto>>> GetActiveConnections(Guid projectId)
    {
        try
        {
            var connections = await _chatService.GetActiveConnectionsAsync(projectId);
            return Ok(connections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active connections for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Helper method to get current user ID
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }
        return userId;
    }
}