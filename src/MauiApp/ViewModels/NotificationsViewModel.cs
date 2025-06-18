using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Core.DTOs;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<NotificationsViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private ObservableCollection<NotificationDto> notifications = new();

    [ObservableProperty]
    private ObservableCollection<NotificationDto> unreadNotifications = new();

    [ObservableProperty]
    private int unreadCount;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private NotificationDto? selectedNotification;

    [ObservableProperty]
    private bool notificationsEnabled = true;

    [ObservableProperty]
    private string filterType = "all";

    public NotificationsViewModel(
        IPushNotificationService pushNotificationService,
        IApiService apiService,
        INavigationService navigationService,
        ILogger<NotificationsViewModel> logger)
    {
        _pushNotificationService = pushNotificationService;
        _apiService = apiService;
        _navigationService = navigationService;
        _logger = logger;

        // Subscribe to notification events
        _pushNotificationService.NotificationReceived += OnNotificationReceived;
        _pushNotificationService.NotificationTapped += OnNotificationTapped;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;

            // Initialize push notification service
            await _pushNotificationService.InitializeAsync();
            NotificationsEnabled = _pushNotificationService.IsPermissionGranted;

            // Load notification history
            await LoadNotificationsAsync();

            _logger.LogInformation("Notifications initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing notifications");
            HasError = true;
            ErrorMessage = "Failed to initialize notifications. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadNotificationsAsync()
    {
        try
        {
            IsLoading = true;

            // Load from local history first
            var localNotifications = await _pushNotificationService.GetNotificationHistoryAsync();
            
            // Load from server
            var serverRequest = new NotificationHistoryRequestDto
            {
                PageSize = 50,
                PageNumber = 1
            };

            var serverResponse = await _apiService.PostAsync<NotificationHistoryResponseDto>(
                "/api/notifications/history", serverRequest);

            // Combine and deduplicate notifications
            var allNotifications = new List<NotificationDto>();
            allNotifications.AddRange(localNotifications);

            if (serverResponse?.Notifications != null)
            {
                foreach (var serverNotification in serverResponse.Notifications)
                {
                    if (!allNotifications.Any(n => n.Id == serverNotification.Id))
                    {
                        allNotifications.Add(serverNotification);
                    }
                }
            }

            // Sort by creation date (newest first)
            var sortedNotifications = allNotifications
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            // Apply filtering
            var filteredNotifications = ApplyFilter(sortedNotifications);

            // Update collections on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Notifications.Clear();
                UnreadNotifications.Clear();

                foreach (var notification in filteredNotifications)
                {
                    Notifications.Add(notification);
                    if (!notification.IsRead)
                    {
                        UnreadNotifications.Add(notification);
                    }
                }

                UpdateUnreadCount();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notifications");
            HasError = true;
            ErrorMessage = "Failed to load notifications. Please try again.";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadNotificationsAsync();
    }

    [RelayCommand]
    private async Task MarkAsReadAsync(NotificationDto notification)
    {
        try
        {
            if (notification.IsRead)
                return;

            await _pushNotificationService.MarkNotificationAsReadAsync(notification.Id);
            
            // Update local state
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                notification.ReadAt = DateTime.UtcNow;
                UnreadNotifications.Remove(notification);
                UpdateUnreadCount();
            });

            _logger.LogInformation("Notification marked as read: {NotificationId}", notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read: {NotificationId}", notification.Id);
        }
    }

    [RelayCommand]
    private async Task MarkAllAsReadAsync()
    {
        try
        {
            var unreadIds = UnreadNotifications.Select(n => n.Id).ToList();
            if (!unreadIds.Any())
                return;

            // Mark all unread notifications as read
            var tasks = unreadIds.Select(id => _pushNotificationService.MarkNotificationAsReadAsync(id));
            await Task.WhenAll(tasks);

            // Update local state
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var notification in UnreadNotifications.ToList())
                {
                    notification.ReadAt = DateTime.UtcNow;
                }
                UnreadNotifications.Clear();
                UpdateUnreadCount();
            });

            await Shell.Current.DisplayAlert("Success", "All notifications marked as read", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read");
            await Shell.Current.DisplayAlert("Error", "Failed to mark all notifications as read", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteNotificationAsync(NotificationDto notification)
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Delete Notification",
                "Are you sure you want to delete this notification?",
                "Delete",
                "Cancel");

            if (confirmed)
            {
                // Remove from local collections
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Notifications.Remove(notification);
                    UnreadNotifications.Remove(notification);
                    UpdateUnreadCount();
                });

                _logger.LogInformation("Notification deleted: {NotificationId}", notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete notification: {NotificationId}", notification.Id);
        }
    }

    [RelayCommand]
    private async Task ClearAllNotificationsAsync()
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Clear All Notifications",
                "Are you sure you want to clear all notifications? This action cannot be undone.",
                "Clear",
                "Cancel");

            if (confirmed)
            {
                await _pushNotificationService.ClearAllNotificationsAsync();
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Notifications.Clear();
                    UnreadNotifications.Clear();
                    UpdateUnreadCount();
                });

                await Shell.Current.DisplayAlert("Success", "All notifications cleared", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all notifications");
            await Shell.Current.DisplayAlert("Error", "Failed to clear notifications", "OK");
        }
    }

    [RelayCommand]
    private async Task ShowNotificationDetailsAsync(NotificationDto notification)
    {
        try
        {
            SelectedNotification = notification;

            // Mark as read if not already
            if (!notification.IsRead)
            {
                await MarkAsReadAsync(notification);
            }

            // Handle navigation based on notification type
            await HandleNotificationNavigationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show notification details: {NotificationId}", notification.Id);
        }
    }

    [RelayCommand]
    private async Task RequestPermissionAsync()
    {
        try
        {
            await _pushNotificationService.RequestPermissionAsync();
            NotificationsEnabled = _pushNotificationService.IsPermissionGranted;

            if (NotificationsEnabled)
            {
                await Shell.Current.DisplayAlert("Success", "Notification permission granted", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Permission Denied", 
                    "Please enable notifications in your device settings", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request notification permission");
            await Shell.Current.DisplayAlert("Error", "Failed to request notification permission", "OK");
        }
    }

    [RelayCommand]
    private async Task ChangeFilterAsync(string filter)
    {
        FilterType = filter;
        await LoadNotificationsAsync();
    }

    [RelayCommand]
    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void OnNotificationReceived(object? sender, NotificationDto notification)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Add to the beginning of the list
                Notifications.Insert(0, notification);
                
                if (!notification.IsRead)
                {
                    UnreadNotifications.Insert(0, notification);
                }
                
                UpdateUnreadCount();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received notification");
        }
    }

    private async void OnNotificationTapped(object? sender, NotificationDto notification)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleNotificationNavigationAsync(notification);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notification tap");
        }
    }

    private async Task HandleNotificationNavigationAsync(NotificationDto notification)
    {
        try
        {
            switch (notification.Type)
            {
                case NotificationType.TaskAssigned:
                case NotificationType.TaskCompleted:
                case NotificationType.TaskOverdue:
                    if (notification.TaskId.HasValue)
                    {
                        await _navigationService.NavigateToAsync($"//tasks/detail?taskId={notification.TaskId}");
                    }
                    break;

                case NotificationType.ProjectUpdated:
                case NotificationType.ProjectInvitation:
                    if (notification.ProjectId.HasValue)
                    {
                        await _navigationService.NavigateToAsync($"//projects/detail?projectId={notification.ProjectId}");
                    }
                    break;

                case NotificationType.CommentAdded:
                    if (notification.TaskId.HasValue)
                    {
                        await _navigationService.NavigateToAsync($"//tasks/detail?taskId={notification.TaskId}");
                    }
                    else if (notification.ProjectId.HasValue)
                    {
                        await _navigationService.NavigateToAsync($"//collaboration/chat?projectId={notification.ProjectId}");
                    }
                    break;

                case NotificationType.FileShared:
                    if (notification.ProjectId.HasValue)
                    {
                        await _navigationService.NavigateToAsync($"//files?projectId={notification.ProjectId}");
                    }
                    break;

                default:
                    if (!string.IsNullOrEmpty(notification.ActionUrl))
                    {
                        await _navigationService.NavigateToAsync(notification.ActionUrl);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate for notification: {NotificationId}", notification.Id);
        }
    }

    private List<NotificationDto> ApplyFilter(List<NotificationDto> notifications)
    {
        return FilterType switch
        {
            "unread" => notifications.Where(n => !n.IsRead).ToList(),
            "tasks" => notifications.Where(n => n.Type == NotificationType.TaskAssigned || 
                                               n.Type == NotificationType.TaskCompleted || 
                                               n.Type == NotificationType.TaskOverdue).ToList(),
            "projects" => notifications.Where(n => n.Type == NotificationType.ProjectUpdated || 
                                                  n.Type == NotificationType.ProjectInvitation).ToList(),
            "comments" => notifications.Where(n => n.Type == NotificationType.CommentAdded).ToList(),
            _ => notifications
        };
    }

    private void UpdateUnreadCount()
    {
        UnreadCount = UnreadNotifications.Count;
    }

    partial void OnFilterTypeChanged(string value)
    {
        _ = Task.Run(async () => await LoadNotificationsAsync());
    }
}