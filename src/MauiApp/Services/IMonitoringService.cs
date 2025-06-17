namespace MauiApp.Services;

public interface IMonitoringService
{
    // Performance monitoring
    Task<IDisposable> StartOperationTimingAsync(string operationName, Dictionary<string, string>? tags = null);
    Task RecordOperationDurationAsync(string operationName, TimeSpan duration, bool isSuccess = true, Dictionary<string, string>? tags = null);
    Task RecordMemoryUsageAsync();
    Task RecordCpuUsageAsync();
    Task RecordNetworkUsageAsync(long bytesReceived, long bytesSent);
    
    // Application health monitoring
    Task RecordHealthCheckAsync(string component, bool isHealthy, string? details = null);
    Task RecordStartupTimeAsync(TimeSpan startupDuration);
    Task RecordCrashAsync(Exception exception, string context = "");
    Task RecordResponseTimeAsync(string endpoint, TimeSpan responseTime);
    
    // User experience monitoring
    Task RecordUserInteractionAsync(string interactionType, string elementName, string screenName);
    Task RecordScreenLoadTimeAsync(string screenName, TimeSpan loadTime);
    Task RecordErrorDialogShownAsync(string errorType, string message);
    Task RecordFeatureUsageAsync(string featureName, Dictionary<string, object>? parameters = null);
    
    // Business metrics
    Task RecordBusinessMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null);
    Task IncrementCounterAsync(string counterName, Dictionary<string, string>? tags = null);
    Task RecordUserActionAsync(string action, string category, string? label = null, int? value = null);
    
    // Data and sync monitoring
    Task RecordDataSyncAsync(string syncType, int itemsCount, TimeSpan duration, bool isSuccess);
    Task RecordDatabaseOperationAsync(string operation, string entity, TimeSpan duration, bool isSuccess);
    Task RecordCacheHitRateAsync(string cacheType, int hits, int misses);
    
    // Custom events and metrics
    Task RecordCustomEventAsync(string eventName, Dictionary<string, object>? properties = null, Dictionary<string, double>? metrics = null);
    Task SetUserPropertyAsync(string propertyName, string value);
    Task RecordExceptionAsync(Exception exception, Dictionary<string, string>? properties = null);
    
    // Monitoring configuration
    Task SetSamplingRateAsync(double rate);
    Task EnableMonitoringAsync(bool enable);
    Task FlushAsync();
    Task<MonitoringHealth> GetHealthStatusAsync();
    
    // Real-time monitoring
    event EventHandler<PerformanceAlert>? PerformanceAlertRaised;
    event EventHandler<HealthStatusChanged>? HealthStatusChanged;
}

public class PerformanceAlert
{
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Threshold { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class HealthStatusChanged
{
    public string Component { get; set; } = string.Empty;
    public bool PreviousStatus { get; set; }
    public bool CurrentStatus { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MonitoringHealth
{
    public bool IsHealthy { get; set; }
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    public DateTime LastChecked { get; set; }
    public TimeSpan TotalResponseTime { get; set; }
    public double MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
}

public class ComponentHealth
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime LastChecked { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

public class OperationTimer : IDisposable
{
    private readonly IMonitoringService _monitoringService;
    private readonly string _operationName;
    private readonly Dictionary<string, string>? _tags;
    private readonly DateTime _startTime;
    private bool _disposed = false;

    public OperationTimer(IMonitoringService monitoringService, string operationName, Dictionary<string, string>? tags = null)
    {
        _monitoringService = monitoringService;
        _operationName = operationName;
        _tags = tags;
        _startTime = DateTime.UtcNow;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            var duration = DateTime.UtcNow - _startTime;
            _ = _monitoringService.RecordOperationDurationAsync(_operationName, duration, true, _tags);
            _disposed = true;
        }
    }

    public void CompleteWithError()
    {
        if (!_disposed)
        {
            var duration = DateTime.UtcNow - _startTime;
            _ = _monitoringService.RecordOperationDurationAsync(_operationName, duration, false, _tags);
            _disposed = true;
        }
    }
}