using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface IChatService
{
    // Message operations
    Task<ChatMessageDto> SendMessageAsync(Guid projectId, string content, Guid userId, Guid? replyToMessageId = null, string messageType = "text");
    Task<ChatMessageDto> EditMessageAsync(Guid messageId, string newContent, Guid userId);
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
    Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId, Guid userId);

    // Chat history
    Task<IEnumerable<ChatMessageDto>> GetChatHistoryAsync(Guid projectId, Guid userId, ChatHistoryRequest request);
    Task<int> GetUnreadMessageCountAsync(Guid projectId, Guid userId);

    // Message reactions
    Task<MessageReactionDto> AddReactionAsync(Guid messageId, string reaction, Guid userId);
    Task<bool> RemoveReactionAsync(Guid messageId, string reaction, Guid userId);
    Task<IEnumerable<MessageReactionDto>> GetMessageReactionsAsync(Guid messageId);

    // Read status
    Task MarkMessageAsReadAsync(Guid messageId, Guid userId);
    Task MarkAllMessagesAsReadAsync(Guid projectId, Guid userId);
    Task<IEnumerable<MessageReadStatusDto>> GetMessageReadStatusesAsync(Guid messageId);

    // User presence
    Task<UserPresenceDto> UpdatePresenceAsync(Guid projectId, Guid userId, string status, string? activity = null);
    Task<IEnumerable<UserPresenceDto>> GetProjectPresencesAsync(Guid projectId);
    Task<UserPresenceDto?> GetUserPresenceAsync(Guid projectId, Guid userId);

    // Project chat summary
    Task<ProjectChatSummaryDto> GetProjectChatSummaryAsync(Guid projectId, Guid userId);

    // Connection management
    Task<UserConnectionDto> TrackConnectionAsync(Guid userId, string connectionId, string? userAgent = null, string? ipAddress = null);
    Task UpdateConnectionLastSeenAsync(string connectionId);
    Task RemoveConnectionAsync(string connectionId);
    Task<IEnumerable<UserConnectionDto>> GetActiveConnectionsAsync(Guid projectId);
}