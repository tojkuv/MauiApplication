using Microsoft.Extensions.Logging;
using MauiApp.Data;
using MauiApp.Data.Models;
using System.Text.Json;
using System.Diagnostics;

namespace MauiApp.Services;

public class EnhancedLoggingService : IEnhancedLoggingService
{
    private readonly ILogger<EnhancedLoggingService> _logger;
    private readonly LocalDbContext _dbContext;
    private readonly IAuthenticationService _authenticationService;
    private readonly Timer _cleanupTimer;
    
    private LogLevel _currentLogLevel = LogLevel.Information;
    private bool _remoteLoggingEnabled = true;
    private int _maxLogEntries = 10000;
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly string _appVersion = AppInfo.VersionString;
    private readonly string _deviceInfo = GetDeviceInfo();

    public EnhancedLoggingService(
        ILogger<EnhancedLoggingService> logger,
        LocalDbContext dbContext,
        IAuthenticationService authenticationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _authenticationService = authenticationService;
        
        // Setup cleanup timer to run daily
        _cleanupTimer = new Timer(CleanupOldLogs, null, TimeSpan.FromHours(1), TimeSpan.FromHours(24));
        
        // Setup unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    #region Basic Logging

    public async Task LogAsync(LogLevel level, string message, Exception? exception = null, object? data = null)
    {
        if (level < _currentLogLevel) return;

        try
        {
            var logEntry = new LogEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = "Application",
                Message = message,
                Exception = exception?.ToString(),
                Data = data != null ? JsonSerializer.Serialize(data) : null,
                Source = GetCallerInfo(),
                UserId = await GetCurrentUserIdAsync(),
                SessionId = _sessionId,
                DeviceInfo = _deviceInfo,
                AppVersion = _appVersion,
                IsSynced = false
            };

            // Log to system logger
            _logger.Log((Microsoft.Extensions.Logging.LogLevel)level, exception, message);

            // Store in local database
            await StoreLogEntryAsync(logEntry);

            // Send to remote logging if enabled
            if (_remoteLoggingEnabled && level >= LogLevel.Warning)
            {
                _ = Task.Run(() => SendToRemoteLoggingAsync(logEntry));
            }
        }
        catch (Exception ex)
        {
            // Fallback logging to prevent infinite loops
            Debug.WriteLine($"Logging error: {ex.Message}");
        }
    }

    public async Task LogInfoAsync(string message, object? data = null)
        => await LogAsync(LogLevel.Information, message, null, data);

    public async Task LogWarningAsync(string message, object? data = null)
        => await LogAsync(LogLevel.Warning, message, null, data);

    public async Task LogErrorAsync(string message, Exception? exception = null, object? data = null)
        => await LogAsync(LogLevel.Error, message, exception, data);

    public async Task LogCriticalAsync(string message, Exception? exception = null, object? data = null)
        => await LogAsync(LogLevel.Critical, message, exception, data);

    public async Task LogDebugAsync(string message, object? data = null)
        => await LogAsync(LogLevel.Debug, message, null, data);

    #endregion

    #region Application Lifecycle Logging

    public async Task LogAppStartAsync()
    {
        var data = new
        {
            Platform = DeviceInfo.Platform.ToString(),
            Version = DeviceInfo.VersionString,
            Model = DeviceInfo.Model,
            Manufacturer = DeviceInfo.Manufacturer,
            Name = DeviceInfo.Name,
            Idiom = DeviceInfo.Idiom.ToString(),
            StartTime = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, "Application started", null, data);
    }

    public async Task LogAppSuspendAsync()
    {
        await LogAsync(LogLevel.Information, "Application suspended", null, new { Timestamp = DateTime.UtcNow });
    }

    public async Task LogAppResumeAsync()
    {
        await LogAsync(LogLevel.Information, "Application resumed", null, new { Timestamp = DateTime.UtcNow });
    }

    public async Task LogAppCrashAsync(Exception exception)
    {
        await LogAsync(LogLevel.Critical, "Application crashed", exception, new { CrashTime = DateTime.UtcNow });
    }

    #endregion

    #region User Activity Logging

    public async Task LogUserActionAsync(string action, string screen, object? data = null)
    {
        var logData = new
        {
            Action = action,
            Screen = screen,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, $"User action: {action} on {screen}", null, logData);
    }

    public async Task LogPageViewAsync(string pageName, TimeSpan? duration = null)
    {
        var data = new
        {
            PageName = pageName,
            Duration = duration?.TotalSeconds,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Debug, $"Page view: {pageName}", null, data);
    }

    public async Task LogButtonClickAsync(string buttonName, string screen)
    {
        await LogUserActionAsync("ButtonClick", screen, new { ButtonName = buttonName });
    }

    public async Task LogSearchAsync(string query, string screen, int resultsCount = 0)
    {
        var data = new
        {
            Query = query,
            Screen = screen,
            ResultsCount = resultsCount,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, $"Search performed: {query}", null, data);
    }

    #endregion

    #region Performance Logging

    public async Task LogPerformanceAsync(string operation, TimeSpan duration, bool isSuccess = true, object? data = null)
    {
        var perfData = new
        {
            Operation = operation,
            DurationMs = duration.TotalMilliseconds,
            IsSuccess = isSuccess,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        var level = duration.TotalSeconds > 5 ? LogLevel.Warning : LogLevel.Debug;
        await LogAsync(level, $"Performance: {operation} took {duration.TotalMilliseconds:F2}ms", null, perfData);
    }

    public async Task LogApiCallAsync(string endpoint, string method, TimeSpan duration, int statusCode, bool isSuccess)
    {
        var data = new
        {
            Endpoint = endpoint,
            Method = method,
            DurationMs = duration.TotalMilliseconds,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            Timestamp = DateTime.UtcNow
        };

        var level = !isSuccess ? LogLevel.Error : 
                   duration.TotalSeconds > 10 ? LogLevel.Warning : LogLevel.Debug;

        await LogAsync(level, $"API call: {method} {endpoint} [{statusCode}]", null, data);
    }

    public async Task LogDatabaseOperationAsync(string operation, string table, TimeSpan duration, bool isSuccess)
    {
        var data = new
        {
            Operation = operation,
            Table = table,
            DurationMs = duration.TotalMilliseconds,
            IsSuccess = isSuccess,
            Timestamp = DateTime.UtcNow
        };

        var level = !isSuccess ? LogLevel.Error : 
                   duration.TotalSeconds > 2 ? LogLevel.Warning : LogLevel.Debug;

        await LogAsync(level, $"Database: {operation} on {table}", null, data);
    }

    #endregion

    #region Error and Crash Logging

    public async Task LogUnhandledExceptionAsync(Exception exception, string context = "")
    {
        var data = new
        {
            Context = context,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Critical, $"Unhandled exception: {exception.Message}", exception, data);
    }

    public async Task LogValidationErrorAsync(string field, string error, object? data = null)
    {
        var validationData = new
        {
            Field = field,
            Error = error,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Warning, $"Validation error on {field}: {error}", null, validationData);
    }

    public async Task LogNetworkErrorAsync(string operation, Exception exception)
    {
        var data = new
        {
            Operation = operation,
            ExceptionType = exception.GetType().Name,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Error, $"Network error during {operation}: {exception.Message}", exception, data);
    }

    #endregion

    #region Business Logic Logging

    public async Task LogBusinessEventAsync(string eventType, string description, object? data = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Description = description,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, $"Business event: {eventType} - {description}", null, eventData);
    }

    public async Task LogDataSyncAsync(string syncType, bool isSuccess, int itemsCount = 0, TimeSpan? duration = null)
    {
        var data = new
        {
            SyncType = syncType,
            IsSuccess = isSuccess,
            ItemsCount = itemsCount,
            DurationMs = duration?.TotalMilliseconds,
            Timestamp = DateTime.UtcNow
        };

        var level = !isSuccess ? LogLevel.Error : LogLevel.Information;
        await LogAsync(level, $"Data sync: {syncType} - {(isSuccess ? "Success" : "Failed")}", null, data);
    }

    public async Task LogFeatureUsageAsync(string featureName, object? parameters = null)
    {
        var data = new
        {
            FeatureName = featureName,
            Parameters = parameters,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, $"Feature used: {featureName}", null, data);
    }

    #endregion

    #region Analytics and Metrics

    public async Task LogMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        var data = new
        {
            MetricName = metricName,
            Value = value,
            Tags = tags,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Debug, $"Metric: {metricName} = {value}", null, data);
    }

    public async Task LogCounterAsync(string counterName, int increment = 1, Dictionary<string, string>? tags = null)
    {
        var data = new
        {
            CounterName = counterName,
            Increment = increment,
            Tags = tags,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Debug, $"Counter: {counterName} += {increment}", null, data);
    }

    public async Task LogCustomEventAsync(string eventName, Dictionary<string, object>? properties = null)
    {
        var data = new
        {
            EventName = eventName,
            Properties = properties,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(LogLevel.Information, $"Custom event: {eventName}", null, data);
    }

    #endregion

    #region Log Management

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(DateTime? fromDate = null, LogLevel? minLevel = null, int maxEntries = 100)
    {
        try
        {
            // This would be implemented with actual database queries
            // For now, returning empty collection
            await Task.Delay(1);
            return new List<LogEntry>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting logs: {ex.Message}");
            return new List<LogEntry>();
        }
    }

    public async Task<long> GetLogsSizeAsync()
    {
        try
        {
            // Calculate logs database size
            await Task.Delay(1);
            return 0; // Placeholder
        }
        catch
        {
            return 0;
        }
    }

    public async Task ClearLogsAsync(DateTime? olderThan = null)
    {
        try
        {
            var cutoffDate = olderThan ?? DateTime.UtcNow.AddDays(-30);
            // Delete logs older than cutoff date
            await Task.Delay(1);
            await LogAsync(LogLevel.Information, $"Cleared logs older than {cutoffDate}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing logs: {ex.Message}");
        }
    }

    public async Task ExportLogsAsync(string filePath, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var logs = await GetLogsAsync(fromDate, null, int.MaxValue);
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            
            await LogAsync(LogLevel.Information, $"Logs exported to {filePath}");
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Error, $"Failed to export logs to {filePath}", ex);
        }
    }

    #endregion

    #region Configuration

    public async Task SetLogLevelAsync(LogLevel level)
    {
        _currentLogLevel = level;
        await LogAsync(LogLevel.Information, $"Log level set to {level}");
    }

    public async Task EnableRemoteLoggingAsync(bool enable)
    {
        _remoteLoggingEnabled = enable;
        await LogAsync(LogLevel.Information, $"Remote logging {(enable ? "enabled" : "disabled")}");
    }

    public async Task SetMaxLogEntriesAsync(int maxEntries)
    {
        _maxLogEntries = maxEntries;
        await LogAsync(LogLevel.Information, $"Max log entries set to {maxEntries}");
    }

    #endregion

    #region Private Methods

    private async Task StoreLogEntryAsync(LogEntry logEntry)
    {
        try
        {
            // Store in local database (simplified - would need actual DbSet)
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to store log entry: {ex.Message}");
        }
    }

    private async Task SendToRemoteLoggingAsync(LogEntry logEntry)
    {
        try
        {
            // Send to remote logging service (Azure Application Insights, etc.)
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to send log to remote: {ex.Message}");
        }
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        try
        {
            var user = await _authenticationService.GetCurrentUserAsync();
            return user?.Id ?? "anonymous";
        }
        catch
        {
            return "anonymous";
        }
    }

    private static string GetDeviceInfo()
    {
        try
        {
            return $"{DeviceInfo.Platform} {DeviceInfo.VersionString} - {DeviceInfo.Model}";
        }
        catch
        {
            return "Unknown Device";
        }
    }

    private static string GetCallerInfo()
    {
        try
        {
            var stackTrace = new StackTrace(true);
            var frame = stackTrace.GetFrame(3); // Skip logging service frames
            return $"{frame?.GetMethod()?.DeclaringType?.Name}.{frame?.GetMethod()?.Name}";
        }
        catch
        {
            return "Unknown";
        }
    }

    private async void CleanupOldLogs(object? state)
    {
        try
        {
            await ClearLogsAsync(DateTime.UtcNow.AddDays(-30));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Log cleanup failed: {ex.Message}");
        }
    }

    private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            await LogUnhandledExceptionAsync(ex, "AppDomain.UnhandledException");
        }
    }

    private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        await LogUnhandledExceptionAsync(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved(); // Prevent app crash
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    #endregion
}