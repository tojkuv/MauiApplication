using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IEnhancedLoggingService _loggingService;
    private readonly IMonitoringService _monitoringService;
    private readonly IDatabaseService _databaseService;
    private readonly IOfflineSyncService _syncService;
    private readonly ILogger<DiagnosticsViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private MonitoringHealth healthStatus = new();

    [ObservableProperty]
    private DatabaseInfo databaseInfo = new();

    [ObservableProperty]
    private ObservableCollection<LogEntry> recentLogs = new();

    [ObservableProperty]
    private string applicationInfo = string.Empty;

    [ObservableProperty]
    private string performanceMetrics = string.Empty;

    [ObservableProperty]
    private int pendingChangesCount;

    [ObservableProperty]
    private DateTime? lastSyncTime;

    public DiagnosticsViewModel(
        IEnhancedLoggingService loggingService,
        IMonitoringService monitoringService,
        IDatabaseService databaseService,
        IOfflineSyncService syncService,
        ILogger<DiagnosticsViewModel> logger)
    {
        _loggingService = loggingService;
        _monitoringService = monitoringService;
        _databaseService = databaseService;
        _syncService = syncService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadDiagnostics()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading diagnostics information");

            await Task.WhenAll(
                LoadHealthStatusAsync(),
                LoadDatabaseInfoAsync(),
                LoadRecentLogsAsync(),
                LoadApplicationInfoAsync(),
                LoadSyncInfoAsync()
            );

            await _loggingService.LogUserActionAsync("ViewDiagnostics", "DiagnosticsPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading diagnostics");
            await Shell.Current.DisplayAlert("Error", "Unable to load diagnostics information.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshData()
    {
        await LoadDiagnosticsAsync();
    }

    [RelayCommand]
    private async Task ClearLogs()
    {
        try
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Clear Logs",
                "Are you sure you want to clear all logs? This action cannot be undone.",
                "Clear",
                "Cancel");

            if (confirm)
            {
                await _loggingService.ClearLogsAsync();
                await LoadRecentLogsAsync();
                await Shell.Current.DisplayAlert("Success", "Logs have been cleared.", "OK");
                
                await _loggingService.LogUserActionAsync("ClearLogs", "DiagnosticsPage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing logs");
            await Shell.Current.DisplayAlert("Error", "Unable to clear logs.", "OK");
        }
    }

    [RelayCommand]
    private async Task ExportLogs()
    {
        try
        {
            var fileName = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            
            await _loggingService.ExportLogsAsync(filePath);
            
            await Shell.Current.DisplayAlert("Success", $"Logs exported to {fileName}", "OK");
            await _loggingService.LogUserActionAsync("ExportLogs", "DiagnosticsPage", new { FileName = fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs");
            await Shell.Current.DisplayAlert("Error", "Unable to export logs.", "OK");
        }
    }

    [RelayCommand]
    private async Task TestPerformanceAlert()
    {
        try
        {
            // Trigger a test performance alert
            await _monitoringService.RecordOperationDurationAsync("TestOperation", TimeSpan.FromSeconds(10), true);
            await _monitoringService.RecordMemoryUsageAsync();
            
            await Shell.Current.DisplayAlert("Test Alert", "Test performance alerts have been triggered. Check the logs.", "OK");
            await _loggingService.LogUserActionAsync("TestPerformanceAlert", "DiagnosticsPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing performance alert");
        }
    }

    [RelayCommand]
    private async Task ForceSyncData()
    {
        try
        {
            var result = await _syncService.SyncAllAsync();
            
            var message = result.IsSuccess 
                ? $"Sync completed successfully. {result.SyncedItems}/{result.TotalItems} items synced."
                : $"Sync failed: {result.ErrorMessage}";
            
            await Shell.Current.DisplayAlert("Sync Result", message, "OK");
            await LoadSyncInfoAsync();
            
            await _loggingService.LogUserActionAsync("ForceSyncData", "DiagnosticsPage", new { IsSuccess = result.IsSuccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing data sync");
            await Shell.Current.DisplayAlert("Error", "Unable to sync data.", "OK");
        }
    }

    [RelayCommand]
    private async Task ClearDatabase()
    {
        try
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Clear Database",
                "Are you sure you want to clear all local data? This action cannot be undone and will require re-syncing all data.",
                "Clear",
                "Cancel");

            if (confirm)
            {
                await _databaseService.ClearAllDataAsync();
                await LoadDatabaseInfoAsync();
                await Shell.Current.DisplayAlert("Success", "Local database has been cleared.", "OK");
                
                await _loggingService.LogUserActionAsync("ClearDatabase", "DiagnosticsPage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing database");
            await Shell.Current.DisplayAlert("Error", "Unable to clear database.", "OK");
        }
    }

    [RelayCommand]
    private async Task ViewApplicationInfo()
    {
        var appInfo = $"Application Information:\n\n" +
                     $"Version: {AppInfo.VersionString}\n" +
                     $"Build: {AppInfo.BuildString}\n" +
                     $"Package Name: {AppInfo.PackageName}\n" +
                     $"Platform: {DeviceInfo.Platform}\n" +
                     $"OS Version: {DeviceInfo.VersionString}\n" +
                     $"Device Model: {DeviceInfo.Model}\n" +
                     $"Manufacturer: {DeviceInfo.Manufacturer}\n" +
                     $"Device Type: {DeviceInfo.Idiom}\n" +
                     $"Screen: {DeviceDisplay.MainDisplayInfo.Width}x{DeviceDisplay.MainDisplayInfo.Height}\n" +
                     $"Density: {DeviceDisplay.MainDisplayInfo.Density}";

        await Shell.Current.DisplayAlert("Application Info", appInfo, "OK");
        await _loggingService.LogUserActionAsync("ViewApplicationInfo", "DiagnosticsPage");
    }

    private async Task LoadHealthStatusAsync()
    {
        try
        {
            HealthStatus = await _monitoringService.GetHealthStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading health status");
            HealthStatus = new MonitoringHealth { IsHealthy = false };
        }
    }

    private async Task LoadDatabaseInfoAsync()
    {
        try
        {
            DatabaseInfo = await _databaseService.GetDatabaseInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading database info");
            DatabaseInfo = new DatabaseInfo();
        }
    }

    private async Task LoadRecentLogsAsync()
    {
        try
        {
            var logs = await _loggingService.GetLogsAsync(DateTime.UtcNow.AddHours(-24), LogLevel.Information, 50);
            
            RecentLogs.Clear();
            foreach (var log in logs.OrderByDescending(l => l.Timestamp))
            {
                RecentLogs.Add(log);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent logs");
            RecentLogs.Clear();
        }
    }

    private async Task LoadApplicationInfoAsync()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            ApplicationInfo = $"Memory Usage: {totalMemory:F2} MB\n" +
                            $"GC Collections: Gen0={gen0Collections}, Gen1={gen1Collections}, Gen2={gen2Collections}\n" +
                            $"Startup Time: {DateTime.Now - Process.GetCurrentProcess().StartTime:hh\\:mm\\:ss}\n" +
                            $"Thread Count: {Process.GetCurrentProcess().Threads.Count}";

            var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            PerformanceMetrics = $"Application Uptime: {uptime:dd\\.hh\\:mm\\:ss}\n" +
                               $"Current Memory: {totalMemory:F2} MB\n" +
                               $"Peak Memory: {Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024.0):F2} MB\n" +
                               $"Total Processor Time: {Process.GetCurrentProcess().TotalProcessorTime:hh\\:mm\\:ss}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application info");
            ApplicationInfo = "Unable to load application information";
            PerformanceMetrics = "Unable to load performance metrics";
        }
    }

    private async Task LoadSyncInfoAsync()
    {
        try
        {
            PendingChangesCount = await _syncService.GetPendingChangesCountAsync();
            LastSyncTime = await _syncService.GetLastSyncTimeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sync info");
            PendingChangesCount = 0;
            LastSyncTime = null;
        }
    }
}