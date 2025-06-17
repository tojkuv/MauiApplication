namespace MauiApp.Services;

public interface IEnhancedLoggingService
{
    // Basic logging
    Task LogAsync(LogLevel level, string message, Exception? exception = null, object? data = null);
    Task LogInfoAsync(string message, object? data = null);
    Task LogWarningAsync(string message, object? data = null);
    Task LogErrorAsync(string message, Exception? exception = null, object? data = null);
    Task LogCriticalAsync(string message, Exception? exception = null, object? data = null);
    Task LogDebugAsync(string message, object? data = null);
    
    // Application lifecycle logging
    Task LogAppStartAsync();
    Task LogAppSuspendAsync();
    Task LogAppResumeAsync();
    Task LogAppCrashAsync(Exception exception);
    
    // User activity logging
    Task LogUserActionAsync(string action, string screen, object? data = null);
    Task LogPageViewAsync(string pageName, TimeSpan? duration = null);
    Task LogButtonClickAsync(string buttonName, string screen);
    Task LogSearchAsync(string query, string screen, int resultsCount = 0);
    
    // Performance logging
    Task LogPerformanceAsync(string operation, TimeSpan duration, bool isSuccess = true, object? data = null);
    Task LogApiCallAsync(string endpoint, string method, TimeSpan duration, int statusCode, bool isSuccess);
    Task LogDatabaseOperationAsync(string operation, string table, TimeSpan duration, bool isSuccess);
    
    // Error and crash logging
    Task LogUnhandledExceptionAsync(Exception exception, string context = "");
    Task LogValidationErrorAsync(string field, string error, object? data = null);
    Task LogNetworkErrorAsync(string operation, Exception exception);
    
    // Business logic logging
    Task LogBusinessEventAsync(string eventType, string description, object? data = null);
    Task LogDataSyncAsync(string syncType, bool isSuccess, int itemsCount = 0, TimeSpan? duration = null);
    Task LogFeatureUsageAsync(string featureName, object? parameters = null);
    
    // Analytics and metrics
    Task LogMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null);
    Task LogCounterAsync(string counterName, int increment = 1, Dictionary<string, string>? tags = null);
    Task LogCustomEventAsync(string eventName, Dictionary<string, object>? properties = null);
    
    // Log management
    Task<IEnumerable<LogEntry>> GetLogsAsync(DateTime? fromDate = null, LogLevel? minLevel = null, int maxEntries = 100);
    Task<long> GetLogsSizeAsync();
    Task ClearLogsAsync(DateTime? olderThan = null);
    Task ExportLogsAsync(string filePath, DateTime? fromDate = null, DateTime? toDate = null);
    
    // Configuration
    Task SetLogLevelAsync(LogLevel level);
    Task EnableRemoteLoggingAsync(bool enable);
    Task SetMaxLogEntriesAsync(int maxEntries);
}

public class LogEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Data { get; set; }
    public string Source { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public bool IsSynced { get; set; } = false;
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}