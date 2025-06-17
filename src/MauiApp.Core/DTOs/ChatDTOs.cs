using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.DTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public Guid? ReplyToMessageId { get; set; }
    public ChatMessageDto? ReplyToMessage { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MessageReactionDto> Reactions { get; set; } = new();
    public List<MessageReadStatusDto> ReadStatuses { get; set; } = new();
    public int ReplyCount { get; set; }
}

public class SendMessageRequest
{
    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public string MessageType { get; set; } = "text";

    public Guid? ReplyToMessageId { get; set; }
}

public class EditMessageRequest
{
    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public class MessageReactionDto
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Reaction { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddReactionRequest
{
    [Required]
    [StringLength(10)]
    public string Reaction { get; set; } = string.Empty;
}

public class MessageReadStatusDto
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}

public class UserPresenceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = "offline";
    public string? Activity { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdatePresenceRequest
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "online";

    [StringLength(200)]
    public string? Activity { get; set; }
}

public class UserConnectionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}

public class ProjectChatSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int UnreadCount { get; set; }
    public ChatMessageDto? LastMessage { get; set; }
    public List<UserPresenceDto> OnlineUsers { get; set; } = new();
    public DateTime LastActivity { get; set; }
}

public class ChatHistoryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? Before { get; set; }
    public DateTime? After { get; set; }
    public string? SearchTerm { get; set; }
}