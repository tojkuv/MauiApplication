using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using MauiApp.Core.Services;
using MauiApp.Services;
using MauiApp.Data;
using MauiApp.ViewModels;
using CommunityToolkit.Mvvm;

namespace MauiApp;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add Aspire service discovery
        builder.Services.AddServiceDiscovery();

        // Configure HTTP client with service discovery
        builder.Services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = new Uri("https+http://apiservice");
        }).AddServiceDiscovery();

        // Register services
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IDataService, DataService>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<ILoggingService, LoggingService>();
        
        // Register ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddLogging();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}