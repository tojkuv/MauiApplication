using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MauiApp.Core.Interfaces;
using MauiApp.Core.DTOs;
using System.Security.Claims;

namespace MauiApp.CollaborationService.Hubs;

[Authorize]
public class CollaborationHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<CollaborationHub> _logger;

    public CollaborationHub(IChatService chatService, ILogger<CollaborationHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Track connection
            await _chatService.TrackConnectionAsync(
                userId.Value, 
                Context.ConnectionId,
                Context.GetHttpContext()?.Request.Headers["User-Agent"].FirstOrDefault(),
                Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString()
            );

            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await _chatService.RemoveConnectionAsync(Context.ConnectionId);
            _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Project management
    public async Task JoinProject(string projectIdString)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            // Join SignalR group for the project
            await Groups.AddToGroupAsync(Context.ConnectionId, $"project_{projectId}");
            
            // Update user presence
            await _chatService.UpdatePresenceAsync(projectId, userId.Value, "online", "Active in project");
            
            // Notify other users in the project
            await Clients.OthersInGroup($"project_{projectId}")
                .SendAsync("UserJoinedProject", new
                {
                    UserId = userId.Value,
                    ProjectId = projectId,
                    Timestamp = DateTime.UtcNow
                });

            await Clients.Caller.SendAsync("JoinedProject", projectId);
            _logger.LogInformation("User {UserId} joined project {ProjectId}", userId, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining project {ProjectId} for user {UserId}", projectId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to join project");
        }
    }

    public async Task LeaveProject(string projectIdString)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            // Leave SignalR group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project_{projectId}");
            
            // Update presence to offline for this project
            await _chatService.UpdatePresenceAsync(projectId, userId.Value, "offline");
            
            // Notify other users
            await Clients.OthersInGroup($"project_{projectId}")
                .SendAsync("UserLeftProject", new
                {
                    UserId = userId.Value,
                    ProjectId = projectId,
                    Timestamp = DateTime.UtcNow
                });

            await Clients.Caller.SendAsync("LeftProject", projectId);
            _logger.LogInformation("User {UserId} left project {ProjectId}", userId, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving project {ProjectId} for user {UserId}", projectId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to leave project");
        }
    }

    // Chat messaging
    public async Task SendMessage(string projectIdString, string content, string? replyToMessageIdString = null)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            await Clients.Caller.SendAsync("Error", "Message content cannot be empty");
            return;
        }

        try
        {
            Guid? replyToMessageId = null;
            if (!string.IsNullOrEmpty(replyToMessageIdString) && Guid.TryParse(replyToMessageIdString, out var replyId))
            {
                replyToMessageId = replyId;
            }

            // Send message through chat service
            var message = await _chatService.SendMessageAsync(projectId, content, userId.Value, replyToMessageId);
            
            // Update user activity
            await _chatService.UpdatePresenceAsync(projectId, userId.Value, "online", "Sending message");
            
            // Broadcast to all users in the project
            await Clients.Group($"project_{projectId}")
                .SendAsync("MessageReceived", message);

            _logger.LogInformation("Message sent by user {UserId} in project {ProjectId}", userId, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for user {UserId} in project {ProjectId}", userId, projectId);
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    public async Task EditMessage(string messageIdString, string newContent)
    {
        if (!Guid.TryParse(messageIdString, out var messageId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid message ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            await Clients.Caller.SendAsync("Error", "Message content cannot be empty");
            return;
        }

        try
        {
            var editedMessage = await _chatService.EditMessageAsync(messageId, newContent, userId.Value);
            
            // Broadcast to all users in the project
            await Clients.Group($"project_{editedMessage.ProjectId}")
                .SendAsync("MessageEdited", editedMessage);

            _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing message {MessageId} for user {UserId}", messageId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to edit message");
        }
    }

    public async Task DeleteMessage(string messageIdString)
    {
        if (!Guid.TryParse(messageIdString, out var messageId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid message ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            // Get message details before deletion for notification
            var message = await _chatService.GetMessageByIdAsync(messageId, userId.Value);
            if (message == null)
            {
                await Clients.Caller.SendAsync("Error", "Message not found");
                return;
            }

            var deleted = await _chatService.DeleteMessageAsync(messageId, userId.Value);
            if (deleted)
            {
                // Broadcast to all users in the project
                await Clients.Group($"project_{message.ProjectId}")
                    .SendAsync("MessageDeleted", new { MessageId = messageId, DeletedBy = userId.Value });

                _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to delete message");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to delete message");
        }
    }

    // Message reactions
    public async Task AddReaction(string messageIdString, string reaction)
    {
        if (!Guid.TryParse(messageIdString, out var messageId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid message ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        if (string.IsNullOrWhiteSpace(reaction))
        {
            await Clients.Caller.SendAsync("Error", "Reaction cannot be empty");
            return;
        }

        try
        {
            var reactionDto = await _chatService.AddReactionAsync(messageId, reaction, userId.Value);
            
            // Get message details for project ID
            var message = await _chatService.GetMessageByIdAsync(messageId, userId.Value);
            if (message != null)
            {
                // Broadcast to all users in the project
                await Clients.Group($"project_{message.ProjectId}")
                    .SendAsync("ReactionAdded", reactionDto);
            }

            _logger.LogInformation("Reaction {Reaction} added to message {MessageId} by user {UserId}", reaction, messageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reaction {Reaction} to message {MessageId} for user {UserId}", reaction, messageId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to add reaction");
        }
    }

    public async Task RemoveReaction(string messageIdString, string reaction)
    {
        if (!Guid.TryParse(messageIdString, out var messageId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid message ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        if (string.IsNullOrWhiteSpace(reaction))
        {
            await Clients.Caller.SendAsync("Error", "Reaction cannot be empty");
            return;
        }

        try
        {
            var removed = await _chatService.RemoveReactionAsync(messageId, reaction, userId.Value);
            if (removed)
            {
                // Get message details for project ID
                var message = await _chatService.GetMessageByIdAsync(messageId, userId.Value);
                if (message != null)
                {
                    // Broadcast to all users in the project
                    await Clients.Group($"project_{message.ProjectId}")
                        .SendAsync("ReactionRemoved", new
                        {
                            MessageId = messageId,
                            Reaction = reaction,
                            UserId = userId.Value
                        });
                }
            }

            _logger.LogInformation("Reaction {Reaction} removed from message {MessageId} by user {UserId}", reaction, messageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing reaction {Reaction} from message {MessageId} for user {UserId}", reaction, messageId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to remove reaction");
        }
    }

    // Typing indicators
    public async Task StartTyping(string projectIdString)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            // Update presence
            await _chatService.UpdatePresenceAsync(projectId, userId.Value, "online", "Typing...");
            
            // Notify others in the project
            await Clients.OthersInGroup($"project_{projectId}")
                .SendAsync("UserStartedTyping", new
                {
                    UserId = userId.Value,
                    ProjectId = projectId,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating typing status for user {UserId} in project {ProjectId}", userId, projectId);
        }
    }

    public async Task StopTyping(string projectIdString)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            // Update presence
            await _chatService.UpdatePresenceAsync(projectId, userId.Value, "online");
            
            // Notify others in the project
            await Clients.OthersInGroup($"project_{projectId}")
                .SendAsync("UserStoppedTyping", new
                {
                    UserId = userId.Value,
                    ProjectId = projectId,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating typing status for user {UserId} in project {ProjectId}", userId, projectId);
        }
    }

    // Presence management
    public async Task UpdateStatus(string projectIdString, string status, string? activity = null)
    {
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid project ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            var presence = await _chatService.UpdatePresenceAsync(projectId, userId.Value, status, activity);
            
            // Broadcast presence update
            await Clients.Group($"project_{projectId}")
                .SendAsync("PresenceUpdated", presence);

            _logger.LogInformation("Presence updated for user {UserId} in project {ProjectId}: {Status}", userId, projectId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for user {UserId} in project {ProjectId}", userId, projectId);
            await Clients.Caller.SendAsync("Error", "Failed to update presence");
        }
    }

    // Mark messages as read
    public async Task MarkAsRead(string messageIdString)
    {
        if (!Guid.TryParse(messageIdString, out var messageId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid message ID");
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        try
        {
            await _chatService.MarkMessageAsReadAsync(messageId, userId.Value);
            
            // Get message details for project notification
            var message = await _chatService.GetMessageByIdAsync(messageId, userId.Value);
            if (message != null)
            {
                // Notify message sender about read status
                await Clients.Group($"project_{message.ProjectId}")
                    .SendAsync("MessageRead", new
                    {
                        MessageId = messageId,
                        ReadBy = userId.Value,
                        ReadAt = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read for user {UserId}", messageId, userId);
        }
    }

    // Utility methods
    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }
}