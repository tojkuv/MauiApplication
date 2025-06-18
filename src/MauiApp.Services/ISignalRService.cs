using MauiApp.Core.DTOs;

namespace MauiApp.Services;

public interface ISignalRService
{
    // Connection management
    Task StartAsync();
    Task StopAsync();
    bool IsConnected { get; }
    
    // Project collaboration
    Task JoinProjectAsync(Guid projectId);
    Task LeaveProjectAsync(Guid projectId);
    
    // Messaging
    Task SendMessageAsync(Guid projectId, string content, Guid? replyToMessageId = null);
    Task EditMessageAsync(Guid messageId, string content);
    Task DeleteMessageAsync(Guid messageId);
    Task AddReactionAsync(Guid messageId, string reaction);
    Task RemoveReactionAsync(Guid messageId, string reaction);
    
    // Presence and activity
    Task UpdatePresenceAsync(string status, string? activity = null);
    Task StartTypingAsync(Guid projectId);
    Task StopTypingAsync(Guid projectId);
    
    // Events
    event EventHandler<ChatMessageDto>? MessageReceived;
    event EventHandler<ChatMessageDto>? MessageUpdated;
    event EventHandler<Guid>? MessageDeleted;
    event EventHandler<MessageReactionDto>? ReactionAdded;
    event EventHandler<MessageReactionDto>? ReactionRemoved;
    event EventHandler<UserPresenceDto>? UserPresenceUpdated;
    event EventHandler<UserTypingEventArgs>? UserTypingStarted;
    event EventHandler<UserTypingEventArgs>? UserTypingStopped;
    event EventHandler<UserJoinedEventArgs>? UserJoinedProject;
    event EventHandler<UserLeftEventArgs>? UserLeftProject;
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}

public class UserTypingEventArgs : EventArgs
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
}

public class UserJoinedEventArgs : EventArgs
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
}

public class UserLeftEventArgs : EventArgs
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string? Error { get; set; }
}