using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Core.DTOs;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Timers;

namespace MauiApp.ViewModels.Collaboration;

public partial class ChatViewModel : ObservableObject
{
    private readonly ISignalRService _signalRService;
    private readonly IApiService _apiService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ChatViewModel> _logger;
    
    private System.Timers.Timer? _typingTimer;
    private bool _isTyping = false;
    private Guid _currentUserId;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string connectionStatus = "Connecting...";

    [ObservableProperty]
    private Guid selectedProjectId;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private string messageText = string.Empty;

    [ObservableProperty]
    private bool isSendingMessage;

    [ObservableProperty]
    private ChatMessageDto? replyingToMessage;

    [ObservableProperty]
    private ObservableCollection<ChatMessageDto> messages = new();

    [ObservableProperty]
    private ObservableCollection<UserPresenceDto> onlineUsers = new();

    [ObservableProperty]
    private ObservableCollection<UserTypingIndicator> typingUsers = new();

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string currentUserName = string.Empty;

    public ChatViewModel(
        ISignalRService signalRService,
        IApiService apiService,
        IAuthenticationService authenticationService,
        ICurrentUserService currentUserService,
        ILogger<ChatViewModel> logger)
    {
        _signalRService = signalRService;
        _apiService = apiService;
        _authenticationService = authenticationService;
        _currentUserService = currentUserService;
        _logger = logger;

        // Setup SignalR event handlers
        SetupSignalREventHandlers();
        
        // Initialize typing timer
        _typingTimer = new System.Timers.Timer(3000); // 3 seconds
        _typingTimer.Elapsed += OnTypingTimerElapsed;
        _typingTimer.AutoReset = false;
    }

    [RelayCommand]
    private async Task InitializeAsync(Guid projectId)
    {
        try
        {
            IsLoading = true;
            HasError = false;
            SelectedProjectId = projectId;

            // Get current user info
            var currentUser = await _authenticationService.GetCurrentUserAsync();
            if (currentUser != null)
            {
                _currentUserId = Guid.Parse(currentUser.Id);
                CurrentUserName = currentUser.Name ?? currentUser.Email ?? "You";
            }

            // Load project info
            await LoadProjectInfoAsync();

            // Connect to SignalR if not already connected
            if (!_signalRService.IsConnected)
            {
                await _signalRService.StartAsync();
            }

            // Join the project
            await _signalRService.JoinProjectAsync(projectId);

            // Load chat history
            await LoadChatHistoryAsync();

            _logger.LogInformation("Chat initialized for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing chat for project {ProjectId}", projectId);
            HasError = true;
            ErrorMessage = "Failed to initialize chat. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || IsSendingMessage)
            return;

        try
        {
            IsSendingMessage = true;
            
            var content = MessageText.Trim();
            var replyToId = ReplyingToMessage?.Id;
            
            // Clear the input immediately for better UX
            MessageText = string.Empty;
            ReplyingToMessage = null;

            // Stop typing indicator
            if (_isTyping)
            {
                await _signalRService.StopTypingAsync(SelectedProjectId);
                _isTyping = false;
            }

            // Send through SignalR for real-time delivery
            await _signalRService.SendMessageAsync(SelectedProjectId, content, replyToId);

            _logger.LogInformation("Message sent to project {ProjectId}", SelectedProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            await Shell.Current.DisplayAlert("Error", "Failed to send message. Please try again.", "OK");
            
            // Restore message text on error
            if (string.IsNullOrWhiteSpace(MessageText))
            {
                // The content was already sent, don't restore
            }
        }
        finally
        {
            IsSendingMessage = false;
        }
    }

    [RelayCommand]
    private void ReplyToMessage(ChatMessageDto message)
    {
        ReplyingToMessage = message;
        // Focus on the message input (handled in the view)
    }

    [RelayCommand]
    private void CancelReply()
    {
        ReplyingToMessage = null;
    }

    [RelayCommand]
    private async Task EditMessageAsync(ChatMessageDto message)
    {
        try
        {
            var newContent = await Shell.Current.DisplayPromptAsync(
                "Edit Message",
                "Update your message:",
                "Save",
                "Cancel",
                message.Content,
                maxLength: 2000);

            if (!string.IsNullOrWhiteSpace(newContent) && newContent != message.Content)
            {
                await _signalRService.EditMessageAsync(message.Id, newContent.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit message {MessageId}", message.Id);
            await Shell.Current.DisplayAlert("Error", "Failed to edit message. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(ChatMessageDto message)
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Delete Message",
                "Are you sure you want to delete this message?",
                "Delete",
                "Cancel");

            if (confirmed)
            {
                await _signalRService.DeleteMessageAsync(message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId}", message.Id);
            await Shell.Current.DisplayAlert("Error", "Failed to delete message. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task AddReactionAsync(ReactionEventArgs args)
    {
        try
        {
            await _signalRService.AddReactionAsync(args.MessageId, args.Reaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add reaction to message {MessageId}", args.MessageId);
        }
    }

    [RelayCommand]
    private async Task RemoveReactionAsync(ReactionEventArgs args)
    {
        try
        {
            await _signalRService.RemoveReactionAsync(args.MessageId, args.Reaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove reaction from message {MessageId}", args.MessageId);
        }
    }

    [RelayCommand]
    private async Task ShowChatMenuAsync()
    {
        try
        {
            var action = await Shell.Current.DisplayActionSheet(
                "Chat Options",
                "Cancel",
                null,
                "View Online Users",
                "Search Messages",
                "Chat Settings",
                "Clear Chat History");

            switch (action)
            {
                case "View Online Users":
                    await ShowOnlineUsersAsync();
                    break;
                case "Search Messages":
                    await SearchMessagesAsync();
                    break;
                case "Chat Settings":
                    await ShowChatSettingsAsync();
                    break;
                case "Clear Chat History":
                    await ClearChatHistoryAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing chat menu");
        }
    }

    [RelayCommand]
    private async Task ShowMessageOptionsAsync(ChatMessageDto message)
    {
        try
        {
            var isOwnMessage = message.AuthorId == _currentUserId;
            var options = new List<string> { "Reply" };
            
            if (isOwnMessage)
            {
                options.AddRange(new[] { "Edit", "Delete" });
            }
            
            options.AddRange(new[] { "React", "Copy Text" });
            
            var action = await Shell.Current.DisplayActionSheet(
                "Message Options",
                "Cancel",
                null,
                options.ToArray());

            switch (action)
            {
                case "Reply":
                    ReplyToMessage(message);
                    break;
                case "Edit" when isOwnMessage:
                    await EditMessageAsync(message);
                    break;
                case "Delete" when isOwnMessage:
                    await DeleteMessageAsync(message);
                    break;
                case "React":
                    await ShowReactionPickerAsync(message);
                    break;
                case "Copy Text":
                    await Clipboard.SetTextAsync(message.Content);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing message options for message {MessageId}", message.Id);
        }
    }

    private async Task ShowOnlineUsersAsync()
    {
        try
        {
            var usersList = string.Join("\n", OnlineUsers.Select(u => $"• {u.UserName} ({u.Status})"));
            await Shell.Current.DisplayAlert("Online Users", usersList, "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing online users");
        }
    }

    private async Task SearchMessagesAsync()
    {
        try
        {
            var searchTerm = await Shell.Current.DisplayPromptAsync(
                "Search Messages",
                "Enter search term:",
                "Search",
                "Cancel");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // TODO: Implement message search
                await Shell.Current.DisplayAlert("Search", $"Searching for: {searchTerm}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching messages");
        }
    }

    private async Task ShowChatSettingsAsync()
    {
        try
        {
            // TODO: Implement chat settings
            await Shell.Current.DisplayAlert("Chat Settings", "Chat settings coming soon!", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing chat settings");
        }
    }

    private async Task ClearChatHistoryAsync()
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Clear Chat History",
                "Are you sure you want to clear all chat history? This action cannot be undone.",
                "Clear",
                "Cancel");

            if (confirmed)
            {
                Messages.Clear();
                await Shell.Current.DisplayAlert("Success", "Chat history cleared.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history");
        }
    }

    private async Task ShowReactionPickerAsync(ChatMessageDto message)
    {
        try
        {
            var reactions = new[] { "👍", "❤️", "😂", "😮", "😢", "😡" };
            
            var selectedReaction = await Shell.Current.DisplayActionSheet(
                "Add Reaction",
                "Cancel",
                null,
                reactions);

            if (!string.IsNullOrEmpty(selectedReaction) && selectedReaction != "Cancel")
            {
                var args = new ReactionEventArgs
                {
                    MessageId = message.Id,
                    Reaction = selectedReaction
                };
                
                await AddReactionAsync(args);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing reaction picker for message {MessageId}", message.Id);
        }
    }

    [RelayCommand]
    private async Task LoadMoreMessagesAsync()
    {
        try
        {
            // Load older messages
            var oldestMessage = Messages.FirstOrDefault();
            if (oldestMessage != null)
            {
                var request = new ChatHistoryRequest
                {
                    Page = 1,
                    PageSize = 50,
                    Before = oldestMessage.CreatedAt
                };

                var historyMessages = await _apiService.PostAsync<List<ChatMessageDto>>(
                    $"/api/collaboration/projects/{SelectedProjectId}/messages/history", request);

                if (historyMessages != null && historyMessages.Any())
                {
                    // Insert older messages at the beginning
                    foreach (var message in historyMessages.OrderBy(m => m.CreatedAt))
                    {
                        Messages.Insert(0, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load more messages");
        }
    }

    private void OnMessageTextChanged()
    {
        if (!string.IsNullOrWhiteSpace(MessageText) && _signalRService.IsConnected)
        {
            HandleTypingIndicator();
        }
    }

    private async void HandleTypingIndicator()
    {
        try
        {
            if (!_isTyping)
            {
                await _signalRService.StartTypingAsync(SelectedProjectId);
                _isTyping = true;
            }

            // Reset the timer
            _typingTimer?.Stop();
            _typingTimer?.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling typing indicator");
        }
    }

    private async void OnTypingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (_isTyping)
            {
                await _signalRService.StopTypingAsync(SelectedProjectId);
                _isTyping = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping typing indicator");
        }
    }

    private async Task LoadProjectInfoAsync()
    {
        try
        {
            var project = await _apiService.GetAsync<ProjectDto>($"/api/projects/{SelectedProjectId}");
            if (project != null)
            {
                ProjectName = project.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project info for {ProjectId}", SelectedProjectId);
            ProjectName = "Unknown Project";
        }
    }

    private async Task LoadChatHistoryAsync()
    {
        try
        {
            var request = new ChatHistoryRequest
            {
                Page = 1,
                PageSize = 100
            };

            var historyMessages = await _apiService.PostAsync<List<ChatMessageDto>>(
                $"/api/collaboration/projects/{SelectedProjectId}/messages/history", request);

            if (historyMessages != null)
            {
                Messages.Clear();
                foreach (var message in historyMessages.OrderBy(m => m.CreatedAt))
                {
                    Messages.Add(message);
                }
            }

            // Load online users
            var onlineUsersList = await _apiService.GetAsync<List<UserPresenceDto>>(
                $"/api/collaboration/projects/{SelectedProjectId}/presence");

            if (onlineUsersList != null)
            {
                OnlineUsers.Clear();
                foreach (var user in onlineUsersList.Where(u => u.Status == "online"))
                {
                    OnlineUsers.Add(user);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat history for project {ProjectId}", SelectedProjectId);
        }
    }

    private void SetupSignalREventHandlers()
    {
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        _signalRService.MessageReceived += OnMessageReceived;
        _signalRService.MessageUpdated += OnMessageUpdated;
        _signalRService.MessageDeleted += OnMessageDeleted;
        _signalRService.ReactionAdded += OnReactionAdded;
        _signalRService.ReactionRemoved += OnReactionRemoved;
        _signalRService.UserPresenceUpdated += OnUserPresenceUpdated;
        _signalRService.UserTypingStarted += OnUserTypingStarted;
        _signalRService.UserTypingStopped += OnUserTypingStopped;
        _signalRService.UserJoinedProject += OnUserJoinedProject;
        _signalRService.UserLeftProject += OnUserLeftProject;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = e.IsConnected;
            ConnectionStatus = e.IsConnected ? "Connected" : $"Disconnected: {e.Error ?? "Unknown error"}";
        });
    }

    private void OnMessageReceived(object? sender, ChatMessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (message.ProjectId == SelectedProjectId)
            {
                Messages.Add(message);
            }
        });
    }

    private void OnMessageUpdated(object? sender, ChatMessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
            if (existingMessage != null)
            {
                var index = Messages.IndexOf(existingMessage);
                Messages[index] = message;
            }
        });
    }

    private void OnMessageDeleted(object? sender, Guid messageId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                Messages.Remove(message);
            }
        });
    }

    private void OnReactionAdded(object? sender, MessageReactionDto reaction)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == reaction.MessageId);
            if (message != null)
            {
                var existingReaction = message.Reactions.FirstOrDefault(r => 
                    r.UserId == reaction.UserId && r.Reaction == reaction.Reaction);
                
                if (existingReaction == null)
                {
                    message.Reactions.Add(reaction);
                }
            }
        });
    }

    private void OnReactionRemoved(object? sender, MessageReactionDto reaction)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == reaction.MessageId);
            if (message != null)
            {
                var existingReaction = message.Reactions.FirstOrDefault(r => r.Id == reaction.Id);
                if (existingReaction != null)
                {
                    message.Reactions.Remove(existingReaction);
                }
            }
        });
    }

    private void OnUserPresenceUpdated(object? sender, UserPresenceDto presence)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (presence.ProjectId == SelectedProjectId)
            {
                var existingUser = OnlineUsers.FirstOrDefault(u => u.UserId == presence.UserId);
                
                if (presence.Status == "online")
                {
                    if (existingUser == null)
                    {
                        OnlineUsers.Add(presence);
                    }
                    else
                    {
                        var index = OnlineUsers.IndexOf(existingUser);
                        OnlineUsers[index] = presence;
                    }
                }
                else if (existingUser != null)
                {
                    OnlineUsers.Remove(existingUser);
                }
            }
        });
    }

    private void OnUserTypingStarted(object? sender, UserTypingEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.ProjectId == SelectedProjectId && e.UserId != _currentUserId)
            {
                var indicator = new UserTypingIndicator
                {
                    UserId = e.UserId,
                    UserName = e.UserName,
                    StartedAt = DateTime.Now
                };
                
                var existing = TypingUsers.FirstOrDefault(u => u.UserId == e.UserId);
                if (existing == null)
                {
                    TypingUsers.Add(indicator);
                }
            }
        });
    }

    private void OnUserTypingStopped(object? sender, UserTypingEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var indicator = TypingUsers.FirstOrDefault(u => u.UserId == e.UserId);
            if (indicator != null)
            {
                TypingUsers.Remove(indicator);
            }
        });
    }

    private void OnUserJoinedProject(object? sender, UserJoinedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.ProjectId == SelectedProjectId)
            {
                // Add system message or notification
                _logger.LogInformation("User {UserName} joined the project", e.UserName);
            }
        });
    }

    private void OnUserLeftProject(object? sender, UserLeftEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.ProjectId == SelectedProjectId)
            {
                // Remove from online users
                var user = OnlineUsers.FirstOrDefault(u => u.UserId == e.UserId);
                if (user != null)
                {
                    OnlineUsers.Remove(user);
                }
                
                _logger.LogInformation("User {UserName} left the project", e.UserName);
            }
        });
    }

    partial void OnMessageTextChanged(string value)
    {
        OnMessageTextChanged();
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (SelectedProjectId != Guid.Empty)
            {
                await _signalRService.LeaveProjectAsync(SelectedProjectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from chat");
        }
    }
}

public class UserTypingIndicator
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

public class ReactionEventArgs : EventArgs
{
    public Guid MessageId { get; set; }
    public string Reaction { get; set; } = string.Empty;
}