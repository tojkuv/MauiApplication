using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Services;
using MauiApp.Services;
using MauiApp.Data;
using MauiApp.Data.Repositories;
using MauiApp.ViewModels;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Maui;

namespace MauiApp;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure SQLite Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "app.db");
        builder.Services.AddDbContext<LocalDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Register authentication handler
        builder.Services.AddTransient<AuthenticationHandler>();
        
        // Configure HTTP client with authentication handler
        builder.Services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7001"); // Default to localhost for development
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<AuthenticationHandler>();

        // Register services
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IDataService, DataService>();
        builder.Services.AddSingleton<ISecureStorageService, SecureStorageService>();
        builder.Services.AddSingleton<IOAuth2Service, OAuth2Service>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<ILoggingService, LoggingService>();
        builder.Services.AddSingleton<IEnhancedLoggingService, EnhancedLoggingService>();
        builder.Services.AddSingleton<IMonitoringService, MonitoringService>();
        builder.Services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<ISignalRService, SignalRService>();
        builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
        builder.Services.AddSingleton<ILocalNotificationService, LocalNotificationService>();
        builder.Services.AddSingleton<IAnalyticsService, AnalyticsService>();
        builder.Services.AddSingleton<IAnalyticsCacheService, AnalyticsCacheService>();
        
        // Notification Services
        builder.Services.AddSingleton<ICacheService, CacheService>();
        builder.Services.AddSingleton<INotificationDeliveryService, NotificationDeliveryService>();
        builder.Services.AddSingleton<INotificationTemplateService, NotificationTemplateService>();
        builder.Services.AddSingleton<INotificationSchedulingService, NotificationSchedulingService>();
        builder.Services.AddSingleton<INotificationPreferencesService, NotificationPreferencesService>();
        builder.Services.AddSingleton<INotificationQueueService, NotificationQueueService>();
        
        // Time Tracking Service
        builder.Services.AddSingleton<ITimeTrackingService, TimeTrackingService>();

        // Register repositories
        builder.Services.AddScoped<ILocalProjectRepository, LocalProjectRepository>();
        builder.Services.AddScoped<ILocalTaskRepository, LocalTaskRepository>();
        
        // Register ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AppShellViewModel>();
        
        // Authentication ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Authentication.WelcomeViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Authentication.LoginViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Authentication.RegisterViewModel>();

        // Dashboard ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Dashboard.DashboardViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Dashboard.MyTasksViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Dashboard.ActivityViewModel>();

        // Project ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Projects.ProjectsListViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Projects.CreateProjectViewModel>();

        // Task ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Tasks.TasksListViewModel>();
        builder.Services.AddTransient<MauiApp.ViewModels.Tasks.KanbanBoardViewModel>();

        // Collaboration ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Collaboration.ChatViewModel>();

        // Files ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Files.FilesViewModel>();

        // Reports ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.Reports.ReportsViewModel>();

        // Other ViewModels
        builder.Services.AddTransient<MauiApp.ViewModels.NotificationsViewModel>();

        // Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        
        // Authentication Pages
        builder.Services.AddTransient<MauiApp.Views.Authentication.WelcomePage>();
        builder.Services.AddTransient<MauiApp.Views.Authentication.LoginPage>();
        builder.Services.AddTransient<MauiApp.Views.Authentication.RegisterPage>();

        // Dashboard Pages
        builder.Services.AddTransient<MauiApp.Views.Dashboard.DashboardPage>();
        builder.Services.AddTransient<MauiApp.Views.Dashboard.MyTasksPage>();
        builder.Services.AddTransient<MauiApp.Views.Dashboard.ActivityPage>();

        // Placeholder Pages
        builder.Services.AddTransient<MauiApp.Views.Projects.ProjectsListPage>();
        builder.Services.AddTransient<MauiApp.Views.Projects.ProjectDetailPage>();
        builder.Services.AddTransient<MauiApp.Views.Projects.CreateProjectPage>();
        builder.Services.AddTransient<MauiApp.Views.Tasks.TasksPage>();
        builder.Services.AddTransient<MauiApp.Views.Tasks.KanbanBoardPage>();
        builder.Services.AddTransient<MauiApp.Views.Tasks.TaskDetailPage>();
        builder.Services.AddTransient<MauiApp.Views.Tasks.CreateTaskPage>();
        builder.Services.AddTransient<MauiApp.Views.Collaboration.ChatPage>();
        builder.Services.AddTransient<MauiApp.Views.Collaboration.ChannelsPage>();
        builder.Services.AddTransient<MauiApp.Views.Files.FilesPage>();
        builder.Services.AddTransient<MauiApp.Views.Reports.ReportsPage>();
        builder.Services.AddTransient<MauiApp.Views.TimeTracking.TimeTrackingPage>();
        builder.Services.AddTransient<MauiApp.Views.NotificationsPage>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddLogging();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}