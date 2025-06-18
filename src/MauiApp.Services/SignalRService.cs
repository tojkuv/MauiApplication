using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MauiApp.Core.DTOs;
using System.Text.Json;

namespace MauiApp.Services;

public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _hubConnection;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    // Events
    public event EventHandler<ChatMessageDto>? MessageReceived;
    public event EventHandler<ChatMessageDto>? MessageUpdated;
    public event EventHandler<Guid>? MessageDeleted;
    public event EventHandler<MessageReactionDto>? ReactionAdded;
    public event EventHandler<MessageReactionDto>? ReactionRemoved;
    public event EventHandler<UserPresenceDto>? UserPresenceUpdated;
    public event EventHandler<UserTypingEventArgs>? UserTypingStarted;
    public event EventHandler<UserTypingEventArgs>? UserTypingStopped;
    public event EventHandler<UserJoinedEventArgs>? UserJoinedProject;
    public event EventHandler<UserLeftEventArgs>? UserLeftProject;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public SignalRService(
        IAuthenticationService authenticationService,
        ILogger<SignalRService> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_hubConnection is not null)
            {
                _logger.LogInformation("SignalR connection already exists, disposing existing connection");
                await _hubConnection.DisposeAsync();
            }

            var token = await _authenticationService.GetAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token available for SignalR connection");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    IsConnected = false,
                    Error = "Authentication required"
                });
                return;
            }

            // Build the connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7001/collaborationHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            // Set up event handlers
            SetupEventHandlers();

            // Start the connection
            await _hubConnection.StartAsync();
            
            _logger.LogInformation("SignalR connection started successfully");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                IsConnected = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                IsConnected = false,
                Error = ex.Message
            });
            throw;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task StopAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                
                _logger.LogInformation("SignalR connection stopped");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    IsConnected = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SignalR connection");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task JoinProjectAsync(Guid projectId)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("JoinProject", projectId.ToString());
            _logger.LogInformation("Joined project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task LeaveProjectAsync(Guid projectId)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("LeaveProject", projectId.ToString());
            _logger.LogInformation("Left project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task SendMessageAsync(Guid projectId, string content, Guid? replyToMessageId = null)
    {
        await EnsureConnectedAsync();
        
        try
        {
            if (replyToMessageId.HasValue)
            {
                await _hubConnection!.InvokeAsync("SendMessage", projectId.ToString(), content, replyToMessageId.Value.ToString());
            }
            else
            {
                await _hubConnection!.InvokeAsync("SendMessage", projectId.ToString(), content);
            }
            
            _logger.LogInformation("Sent message to project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task EditMessageAsync(Guid messageId, string content)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("EditMessage", messageId.ToString(), content);
            _logger.LogInformation("Edited message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit message {MessageId}", messageId);
            throw;
        }
    }

    public async Task DeleteMessageAsync(Guid messageId)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("DeleteMessage", messageId.ToString());
            _logger.LogInformation("Deleted message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId}", messageId);
            throw;
        }
    }

    public async Task AddReactionAsync(Guid messageId, string reaction)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("AddReaction", messageId.ToString(), reaction);
            _logger.LogInformation("Added reaction {Reaction} to message {MessageId}", reaction, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add reaction to message {MessageId}", messageId);
            throw;
        }
    }

    public async Task RemoveReactionAsync(Guid messageId, string reaction)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("RemoveReaction", messageId.ToString(), reaction);
            _logger.LogInformation("Removed reaction {Reaction} from message {MessageId}", reaction, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove reaction from message {MessageId}", messageId);
            throw;
        }
    }

    public async Task UpdatePresenceAsync(string status, string? activity = null)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("UpdatePresence", status, activity);
            _logger.LogInformation("Updated presence to {Status}", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update presence");
            throw;
        }
    }

    public async Task StartTypingAsync(Guid projectId)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("StartTyping", projectId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start typing indicator for project {ProjectId}", projectId);
        }
    }

    public async Task StopTypingAsync(Guid projectId)
    {
        await EnsureConnectedAsync();
        
        try
        {
            await _hubConnection!.InvokeAsync("StopTyping", projectId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop typing indicator for project {ProjectId}", projectId);
        }
    }

    private void SetupEventHandlers()
    {
        if (_hubConnection == null) return;

        // Message events
        _hubConnection.On<string>("MessageReceived", (messageJson) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<ChatMessageDto>(messageJson, _jsonOptions);
                if (message != null)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize received message");
            }
        });

        _hubConnection.On<string>("MessageUpdated", (messageJson) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<ChatMessageDto>(messageJson, _jsonOptions);
                if (message != null)
                {
                    MessageUpdated?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize updated message");
            }
        });

        _hubConnection.On<string>("MessageDeleted", (messageIdStr) =>
        {
            if (Guid.TryParse(messageIdStr, out var messageId))
            {
                MessageDeleted?.Invoke(this, messageId);
            }
        });

        // Reaction events
        _hubConnection.On<string>("ReactionAdded", (reactionJson) =>
        {
            try
            {
                var reaction = JsonSerializer.Deserialize<MessageReactionDto>(reactionJson, _jsonOptions);
                if (reaction != null)
                {
                    ReactionAdded?.Invoke(this, reaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize reaction");
            }
        });

        _hubConnection.On<string>("ReactionRemoved", (reactionJson) =>
        {
            try
            {
                var reaction = JsonSerializer.Deserialize<MessageReactionDto>(reactionJson, _jsonOptions);
                if (reaction != null)
                {
                    ReactionRemoved?.Invoke(this, reaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize reaction");
            }
        });

        // Presence events
        _hubConnection.On<string>("UserPresenceUpdated", (presenceJson) =>
        {
            try
            {
                var presence = JsonSerializer.Deserialize<UserPresenceDto>(presenceJson, _jsonOptions);
                if (presence != null)
                {
                    UserPresenceUpdated?.Invoke(this, presence);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize user presence");
            }
        });

        // Typing events
        _hubConnection.On<string, string, string>("UserStartedTyping", (userIdStr, userName, projectIdStr) =>
        {
            if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(projectIdStr, out var projectId))
            {
                UserTypingStarted?.Invoke(this, new UserTypingEventArgs
                {
                    UserId = userId,
                    UserName = userName,
                    ProjectId = projectId
                });
            }
        });

        _hubConnection.On<string, string, string>("UserStoppedTyping", (userIdStr, userName, projectIdStr) =>
        {
            if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(projectIdStr, out var projectId))
            {
                UserTypingStopped?.Invoke(this, new UserTypingEventArgs
                {
                    UserId = userId,
                    UserName = userName,
                    ProjectId = projectId
                });
            }
        });

        // Project events
        _hubConnection.On<string, string, string>("UserJoinedProject", (userIdStr, userName, projectIdStr) =>
        {
            if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(projectIdStr, out var projectId))
            {
                UserJoinedProject?.Invoke(this, new UserJoinedEventArgs
                {
                    UserId = userId,
                    UserName = userName,
                    ProjectId = projectId
                });
            }
        });

        _hubConnection.On<string, string, string>("UserLeftProject", (userIdStr, userName, projectIdStr) =>
        {
            if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(projectIdStr, out var projectId))
            {
                UserLeftProject?.Invoke(this, new UserLeftEventArgs
                {
                    UserId = userId,
                    UserName = userName,
                    ProjectId = projectId
                });
            }
        });

        // Connection events
        _hubConnection.Closed += async (error) =>
        {
            _logger.LogWarning("SignalR connection closed: {Error}", error?.Message);
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                IsConnected = false,
                Error = error?.Message
            });
        };

        _hubConnection.Reconnecting += (error) =>
        {
            _logger.LogInformation("SignalR connection reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR connection reconnected with ID: {ConnectionId}", connectionId);
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                IsConnected = true
            });
            return Task.CompletedTask;
        };
    }

    private async Task EnsureConnectedAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            await StartAsync();
        }
        
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("SignalR connection is not available");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _connectionSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}