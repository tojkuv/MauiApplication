using Microsoft.EntityFrameworkCore;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.CollaborationService.Data;

namespace MauiApp.CollaborationService.Services;

public class ChatService : IChatService
{
    private readonly CollaborationDbContext _context;
    private readonly ILogger<ChatService> _logger;

    public ChatService(CollaborationDbContext context, ILogger<ChatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatMessageDto> SendMessageAsync(Guid projectId, string content, Guid userId, Guid? replyToMessageId = null, string messageType = "text")
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SenderId = userId,
            Content = content.Trim(),
            MessageType = messageType,
            ReplyToMessageId = replyToMessageId,
            SentAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEdited = false
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Load the complete message with related data
        var savedMessage = await _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(rm => rm!.Sender)
            .Include(m => m.Reactions)
            .ThenInclude(r => r.User)
            .Include(m => m.ReadStatuses)
            .FirstAsync(m => m.Id == message.Id);

        _logger.LogInformation("Message sent by user {UserId} in project {ProjectId}", userId, projectId);

        return MapToDto(savedMessage);
    }

    public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, string newContent, Guid userId)
    {
        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(rm => rm!.Sender)
            .Include(m => m.Reactions)
            .ThenInclude(r => r.User)
            .Include(m => m.ReadStatuses)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            throw new InvalidOperationException("Message not found");
        }

        if (message.SenderId != userId)
        {
            throw new UnauthorizedAccessException("User can only edit their own messages");
        }

        if (message.IsDeleted)
        {
            throw new InvalidOperationException("Cannot edit deleted message");
        }

        message.Content = newContent.Trim();
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);

        return MapToDto(message);
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return false;
        }

        if (message.SenderId != userId)
        {
            throw new UnauthorizedAccessException("User can only delete their own messages");
        }

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);

        return true;
    }

    public async Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(rm => rm!.Sender)
            .Include(m => m.Reactions)
            .ThenInclude(r => r.User)
            .Include(m => m.ReadStatuses)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null || message.IsDeleted)
        {
            return null;
        }

        // Verify user has access to the project
        await ValidateProjectAccessAsync(message.ProjectId, userId);

        return MapToDto(message);
    }

    public async Task<IEnumerable<ChatMessageDto>> GetChatHistoryAsync(Guid projectId, Guid userId, ChatHistoryRequest request)
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var query = _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(rm => rm!.Sender)
            .Include(m => m.Reactions)
            .ThenInclude(r => r.User)
            .Include(m => m.ReadStatuses)
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt);

        if (request.BeforeDate.HasValue)
        {
            query = query.Where(m => m.SentAt < request.BeforeDate.Value);
        }

        if (request.AfterDate.HasValue)
        {
            query = query.Where(m => m.SentAt > request.AfterDate.Value);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            query = query.Where(m => m.Content.Contains(request.SearchTerm));
        }

        var messages = await query
            .Take(request.PageSize ?? 50)
            .ToListAsync();

        return messages.Select(MapToDto);
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid projectId, Guid userId)
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var unreadCount = await _context.ChatMessages
            .Where(m => m.ProjectId == projectId && 
                       !m.IsDeleted && 
                       m.SenderId != userId &&
                       !m.ReadStatuses.Any(rs => rs.UserId == userId))
            .CountAsync();

        return unreadCount;
    }

    public async Task<MessageReactionDto> AddReactionAsync(Guid messageId, string reaction, Guid userId)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null || message.IsDeleted)
        {
            throw new InvalidOperationException("Message not found");
        }

        // Verify user has access to the project
        await ValidateProjectAccessAsync(message.ProjectId, userId);

        // Check if reaction already exists
        var existingReaction = await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Reaction == reaction);

        if (existingReaction != null)
        {
            throw new InvalidOperationException("User has already reacted with this emoji");
        }

        var newReaction = new MessageReaction
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            UserId = userId,
            Reaction = reaction,
            CreatedAt = DateTime.UtcNow
        };

        _context.MessageReactions.Add(newReaction);
        await _context.SaveChangesAsync();

        // Load with user data
        var savedReaction = await _context.MessageReactions
            .Include(r => r.User)
            .FirstAsync(r => r.Id == newReaction.Id);

        _logger.LogInformation("Reaction {Reaction} added to message {MessageId} by user {UserId}", reaction, messageId, userId);

        return new MessageReactionDto
        {
            Id = savedReaction.Id,
            MessageId = savedReaction.MessageId,
            UserId = savedReaction.UserId,
            UserName = savedReaction.User?.FirstName + " " + savedReaction.User?.LastName,
            Reaction = savedReaction.Reaction,
            CreatedAt = savedReaction.CreatedAt
        };
    }

    public async Task<bool> RemoveReactionAsync(Guid messageId, string reaction, Guid userId)
    {
        var existingReaction = await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Reaction == reaction);

        if (existingReaction == null)
        {
            return false;
        }

        _context.MessageReactions.Remove(existingReaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reaction {Reaction} removed from message {MessageId} by user {UserId}", reaction, messageId, userId);

        return true;
    }

    public async Task<IEnumerable<MessageReactionDto>> GetMessageReactionsAsync(Guid messageId)
    {
        var reactions = await _context.MessageReactions
            .Include(r => r.User)
            .Where(r => r.MessageId == messageId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return reactions.Select(r => new MessageReactionDto
        {
            Id = r.Id,
            MessageId = r.MessageId,
            UserId = r.UserId,
            UserName = r.User?.FirstName + " " + r.User?.LastName,
            Reaction = r.Reaction,
            CreatedAt = r.CreatedAt
        });
    }

    public async Task MarkMessageAsReadAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null || message.IsDeleted)
        {
            return;
        }

        // Verify user has access to the project
        await ValidateProjectAccessAsync(message.ProjectId, userId);

        // Check if already marked as read
        var existingReadStatus = await _context.MessageReadStatuses
            .FirstOrDefaultAsync(rs => rs.MessageId == messageId && rs.UserId == userId);

        if (existingReadStatus == null)
        {
            var readStatus = new MessageReadStatus
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                UserId = userId,
                ReadAt = DateTime.UtcNow
            };

            _context.MessageReadStatuses.Add(readStatus);
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllMessagesAsReadAsync(Guid projectId, Guid userId)
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var unreadMessages = await _context.ChatMessages
            .Where(m => m.ProjectId == projectId && 
                       !m.IsDeleted && 
                       m.SenderId != userId &&
                       !m.ReadStatuses.Any(rs => rs.UserId == userId))
            .Select(m => m.Id)
            .ToListAsync();

        var readStatuses = unreadMessages.Select(messageId => new MessageReadStatus
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            UserId = userId,
            ReadAt = DateTime.UtcNow
        });

        _context.MessageReadStatuses.AddRange(readStatuses);
        await _context.SaveChangesAsync();

        _logger.LogInformation("All messages marked as read for user {UserId} in project {ProjectId}", userId, projectId);
    }

    public async Task<IEnumerable<MessageReadStatusDto>> GetMessageReadStatusesAsync(Guid messageId)
    {
        var readStatuses = await _context.MessageReadStatuses
            .Include(rs => rs.User)
            .Where(rs => rs.MessageId == messageId)
            .OrderBy(rs => rs.ReadAt)
            .ToListAsync();

        return readStatuses.Select(rs => new MessageReadStatusDto
        {
            Id = rs.Id,
            MessageId = rs.MessageId,
            UserId = rs.UserId,
            UserName = rs.User?.FirstName + " " + rs.User?.LastName,
            ReadAt = rs.ReadAt
        });
    }

    public async Task<UserPresenceDto> UpdatePresenceAsync(Guid projectId, Guid userId, string status, string? activity = null)
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var presence = await _context.UserPresences
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.UserId == userId);

        if (presence == null)
        {
            presence = new UserPresence
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Status = status,
                Activity = activity,
                LastSeenAt = DateTime.UtcNow
            };
            _context.UserPresences.Add(presence);
        }
        else
        {
            presence.Status = status;
            presence.Activity = activity;
            presence.LastSeenAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Load with user data
        var savedPresence = await _context.UserPresences
            .Include(p => p.User)
            .FirstAsync(p => p.Id == presence.Id);

        return new UserPresenceDto
        {
            ProjectId = savedPresence.ProjectId,
            UserId = savedPresence.UserId,
            UserName = savedPresence.User?.FirstName + " " + savedPresence.User?.LastName,
            Status = savedPresence.Status,
            Activity = savedPresence.Activity,
            LastSeenAt = savedPresence.LastSeenAt
        };
    }

    public async Task<IEnumerable<UserPresenceDto>> GetProjectPresencesAsync(Guid projectId)
    {
        var presences = await _context.UserPresences
            .Include(p => p.User)
            .Where(p => p.ProjectId == projectId)
            .ToListAsync();

        return presences.Select(p => new UserPresenceDto
        {
            ProjectId = p.ProjectId,
            UserId = p.UserId,
            UserName = p.User?.FirstName + " " + p.User?.LastName,
            Status = p.Status,
            Activity = p.Activity,
            LastSeenAt = p.LastSeenAt
        });
    }

    public async Task<UserPresenceDto?> GetUserPresenceAsync(Guid projectId, Guid userId)
    {
        var presence = await _context.UserPresences
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.UserId == userId);

        if (presence == null)
        {
            return null;
        }

        return new UserPresenceDto
        {
            ProjectId = presence.ProjectId,
            UserId = presence.UserId,
            UserName = presence.User?.FirstName + " " + presence.User?.LastName,
            Status = presence.Status,
            Activity = presence.Activity,
            LastSeenAt = presence.LastSeenAt
        };
    }

    public async Task<ProjectChatSummaryDto> GetProjectChatSummaryAsync(Guid projectId, Guid userId)
    {
        // Verify user has access to the project
        await ValidateProjectAccessAsync(projectId, userId);

        var totalMessages = await _context.ChatMessages
            .CountAsync(m => m.ProjectId == projectId && !m.IsDeleted);

        var unreadCount = await GetUnreadMessageCountAsync(projectId, userId);

        var lastMessage = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();

        var activeUsers = await _context.UserPresences
            .Include(p => p.User)
            .Where(p => p.ProjectId == projectId && p.Status == "online")
            .CountAsync();

        return new ProjectChatSummaryDto
        {
            ProjectId = projectId,
            TotalMessages = totalMessages,
            UnreadCount = unreadCount,
            LastMessage = lastMessage != null ? MapToDto(lastMessage) : null,
            ActiveUsers = activeUsers
        };
    }

    public async Task<UserConnectionDto> TrackConnectionAsync(Guid userId, string connectionId, string? userAgent = null, string? ipAddress = null)
    {
        var connection = new UserConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConnectionId = connectionId,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            ConnectedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _context.UserConnections.Add(connection);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connection tracked for user {UserId}: {ConnectionId}", userId, connectionId);

        return new UserConnectionDto
        {
            Id = connection.Id,
            UserId = connection.UserId,
            ConnectionId = connection.ConnectionId,
            UserAgent = connection.UserAgent,
            IpAddress = connection.IpAddress,
            ConnectedAt = connection.ConnectedAt,
            LastSeenAt = connection.LastSeenAt
        };
    }

    public async Task UpdateConnectionLastSeenAsync(string connectionId)
    {
        var connection = await _context.UserConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

        if (connection != null)
        {
            connection.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        var connection = await _context.UserConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

        if (connection != null)
        {
            _context.UserConnections.Remove(connection);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Connection removed: {ConnectionId}", connectionId);
        }
    }

    public async Task<IEnumerable<UserConnectionDto>> GetActiveConnectionsAsync(Guid projectId)
    {
        // Get user IDs that have access to this project
        var projectUserIds = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => pm.UserId)
            .ToListAsync();

        var connections = await _context.UserConnections
            .Where(c => projectUserIds.Contains(c.UserId))
            .ToListAsync();

        return connections.Select(c => new UserConnectionDto
        {
            Id = c.Id,
            UserId = c.UserId,
            ConnectionId = c.ConnectionId,
            UserAgent = c.UserAgent,
            IpAddress = c.IpAddress,
            ConnectedAt = c.ConnectedAt,
            LastSeenAt = c.LastSeenAt
        });
    }

    // Private helper methods
    private async Task ValidateProjectAccessAsync(Guid projectId, Guid userId)
    {
        var hasAccess = await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this project");
        }
    }

    private static ChatMessageDto MapToDto(ChatMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            ProjectId = message.ProjectId,
            SenderId = message.SenderId,
            SenderName = message.Sender?.FirstName + " " + message.Sender?.LastName,
            Content = message.Content,
            MessageType = message.MessageType,
            SentAt = message.SentAt,
            IsEdited = message.IsEdited,
            EditedAt = message.EditedAt,
            ReplyToMessageId = message.ReplyToMessageId,
            ReplyToMessage = message.ReplyToMessage != null ? new ChatMessageDto
            {
                Id = message.ReplyToMessage.Id,
                SenderId = message.ReplyToMessage.SenderId,
                SenderName = message.ReplyToMessage.Sender?.FirstName + " " + message.ReplyToMessage.Sender?.LastName,
                Content = message.ReplyToMessage.Content,
                SentAt = message.ReplyToMessage.SentAt
            } : null,
            Reactions = message.Reactions?.Select(r => new MessageReactionDto
            {
                Id = r.Id,
                MessageId = r.MessageId,
                UserId = r.UserId,
                UserName = r.User?.FirstName + " " + r.User?.LastName,
                Reaction = r.Reaction,
                CreatedAt = r.CreatedAt
            }).ToList() ?? new List<MessageReactionDto>(),
            ReadStatuses = message.ReadStatuses?.Select(rs => new MessageReadStatusDto
            {
                Id = rs.Id,
                MessageId = rs.MessageId,
                UserId = rs.UserId,
                UserName = rs.User?.FirstName + " " + rs.User?.LastName,
                ReadAt = rs.ReadAt
            }).ToList() ?? new List<MessageReadStatusDto>()
        };
    }
}