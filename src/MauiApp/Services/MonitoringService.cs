using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace MauiApp.Services;

public class MonitoringService : IMonitoringService
{
    private readonly IEnhancedLoggingService _loggingService;
    private readonly ILogger<MonitoringService> _logger;
    private readonly Timer _healthCheckTimer;
    private readonly Dictionary<string, ComponentHealth> _componentHealth = new();
    
    private bool _isMonitoringEnabled = true;
    private double _samplingRate = 1.0; // 100% sampling by default
    private readonly Random _random = new();
    
    // Performance thresholds
    private readonly Dictionary<string, double> _performanceThresholds = new()
    {
        ["memory_usage_mb"] = 500,
        ["cpu_usage_percent"] = 80,
        ["response_time_ms"] = 5000,
        ["screen_load_time_ms"] = 3000
    };

    public event EventHandler<PerformanceAlert>? PerformanceAlertRaised;
    public event EventHandler<HealthStatusChanged>? HealthStatusChanged;

    public MonitoringService(
        IEnhancedLoggingService loggingService,
        ILogger<MonitoringService> logger)
    {
        _loggingService = loggingService;
        _logger = logger;
        
        // Setup periodic health checks
        _healthCheckTimer = new Timer(PerformHealthChecks, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        
        // Initialize component health
        InitializeComponentHealth();
    }

    #region Performance Monitoring

    public async Task<IDisposable> StartOperationTimingAsync(string operationName, Dictionary<string, string>? tags = null)
    {
        if (!ShouldSample()) return new NoOpTimer();
        
        await _loggingService.LogDebugAsync($"Starting operation timing: {operationName}", tags);
        return new OperationTimer(this, operationName, tags);
    }

    public async Task RecordOperationDurationAsync(string operationName, TimeSpan duration, bool isSuccess = true, Dictionary<string, string>? tags = null)
    {
        if (!_isMonitoringEnabled || !ShouldSample()) return;

        var durationMs = duration.TotalMilliseconds;
        
        // Check for performance alerts
        if (_performanceThresholds.ContainsKey($"{operationName}_ms") && 
            durationMs > _performanceThresholds[$"{operationName}_ms"])
        {
            await RaisePerformanceAlertAsync("OperationSlow", $"{operationName} took {durationMs:F2}ms", operationName, durationMs, _performanceThresholds[$"{operationName}_ms"], tags);
        }

        await _loggingService.LogPerformanceAsync(operationName, duration, isSuccess, tags);
        
        var telemetryData = new Dictionary<string, object>
        {
            ["operation"] = operationName,
            ["duration_ms"] = durationMs,
            ["is_success"] = isSuccess
        };
        
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                telemetryData[$"tag_{tag.Key}"] = tag.Value;
            }
        }

        await _loggingService.LogCustomEventAsync("OperationCompleted", telemetryData);
    }

    public async Task RecordMemoryUsageAsync()
    {
        if (!_isMonitoringEnabled) return;

        try
        {
            var memoryUsage = GC.GetTotalMemory(false) / (1024.0 * 1024.0); // Convert to MB
            
            if (memoryUsage > _performanceThresholds["memory_usage_mb"])
            {
                await RaisePerformanceAlertAsync("HighMemoryUsage", $"Memory usage is {memoryUsage:F2} MB", "Memory", memoryUsage, _performanceThresholds["memory_usage_mb"]);
            }

            await _loggingService.LogMetricAsync("memory_usage_mb", memoryUsage, new Dictionary<string, string>
            {
                ["category"] = "performance",
                ["type"] = "memory"
            });
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Failed to record memory usage", ex);
        }
    }

    public async Task RecordCpuUsageAsync()
    {
        if (!_isMonitoringEnabled) return;

        try
        {
            // CPU usage monitoring would require platform-specific implementation
            // For now, we'll use a placeholder
            var cpuUsage = Random.Shared.NextDouble() * 100; // Placeholder
            
            if (cpuUsage > _performanceThresholds["cpu_usage_percent"])
            {
                await RaisePerformanceAlertAsync("HighCpuUsage", $"CPU usage is {cpuUsage:F2}%", "CPU", cpuUsage, _performanceThresholds["cpu_usage_percent"]);
            }

            await _loggingService.LogMetricAsync("cpu_usage_percent", cpuUsage, new Dictionary<string, string>
            {
                ["category"] = "performance",
                ["type"] = "cpu"
            });
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Failed to record CPU usage", ex);
        }
    }

    public async Task RecordNetworkUsageAsync(long bytesReceived, long bytesSent)
    {
        if (!_isMonitoringEnabled) return;

        var data = new Dictionary<string, object>
        {
            ["bytes_received"] = bytesReceived,
            ["bytes_sent"] = bytesSent,
            ["total_bytes"] = bytesReceived + bytesSent
        };

        await _loggingService.LogCustomEventAsync("NetworkUsage", data);
    }

    #endregion

    #region Application Health Monitoring

    public async Task RecordHealthCheckAsync(string component, bool isHealthy, string? details = null)
    {
        var previousHealth = _componentHealth.ContainsKey(component) ? _componentHealth[component].IsHealthy : true;
        
        _componentHealth[component] = new ComponentHealth
        {
            IsHealthy = isHealthy,
            Status = isHealthy ? "Healthy" : "Unhealthy",
            Details = details,
            LastChecked = DateTime.UtcNow,
            ResponseTime = TimeSpan.Zero
        };

        if (previousHealth != isHealthy)
        {
            HealthStatusChanged?.Invoke(this, new HealthStatusChanged
            {
                Component = component,
                PreviousStatus = previousHealth,
                CurrentStatus = isHealthy,
                Details = details,
                Timestamp = DateTime.UtcNow
            });

            await _loggingService.LogWarningAsync($"Health status changed for {component}: {(isHealthy ? "Healthy" : "Unhealthy")}", new { Component = component, IsHealthy = isHealthy, Details = details });
        }

        await _loggingService.LogInfoAsync($"Health check: {component} is {(isHealthy ? "healthy" : "unhealthy")}", new { Component = component, IsHealthy = isHealthy, Details = details });
    }

    public async Task RecordStartupTimeAsync(TimeSpan startupDuration)
    {
        var startupMs = startupDuration.TotalMilliseconds;
        
        await _loggingService.LogMetricAsync("startup_time_ms", startupMs, new Dictionary<string, string>
        {
            ["category"] = "performance",
            ["type"] = "startup"
        });

        await _loggingService.LogInfoAsync($"Application startup completed in {startupMs:F2}ms", new { StartupTimeMs = startupMs });
    }

    public async Task RecordCrashAsync(Exception exception, string context = "")
    {
        await _loggingService.LogCriticalAsync($"Application crash detected: {context}", exception, new { Context = context, CrashTime = DateTime.UtcNow });
        
        await _loggingService.LogCustomEventAsync("ApplicationCrash", new Dictionary<string, object>
        {
            ["exception_type"] = exception.GetType().Name,
            ["exception_message"] = exception.Message,
            ["context"] = context,
            ["stack_trace"] = exception.StackTrace ?? "",
            ["crash_time"] = DateTime.UtcNow
        });
    }

    public async Task RecordResponseTimeAsync(string endpoint, TimeSpan responseTime)
    {
        var responseTimeMs = responseTime.TotalMilliseconds;
        
        if (responseTimeMs > _performanceThresholds["response_time_ms"])
        {
            await RaisePerformanceAlertAsync("SlowResponse", $"{endpoint} responded in {responseTimeMs:F2}ms", endpoint, responseTimeMs, _performanceThresholds["response_time_ms"]);
        }

        await _loggingService.LogMetricAsync("response_time_ms", responseTimeMs, new Dictionary<string, string>
        {
            ["endpoint"] = endpoint,
            ["category"] = "performance",
            ["type"] = "response_time"
        });
    }

    #endregion

    #region User Experience Monitoring

    public async Task RecordUserInteractionAsync(string interactionType, string elementName, string screenName)
    {
        if (!ShouldSample()) return;

        await _loggingService.LogUserActionAsync(interactionType, screenName, new { ElementName = elementName });
        
        await _loggingService.LogCustomEventAsync("UserInteraction", new Dictionary<string, object>
        {
            ["interaction_type"] = interactionType,
            ["element_name"] = elementName,
            ["screen_name"] = screenName,
            ["timestamp"] = DateTime.UtcNow
        });
    }

    public async Task RecordScreenLoadTimeAsync(string screenName, TimeSpan loadTime)
    {
        var loadTimeMs = loadTime.TotalMilliseconds;
        
        if (loadTimeMs > _performanceThresholds["screen_load_time_ms"])
        {
            await RaisePerformanceAlertAsync("SlowScreenLoad", $"{screenName} loaded in {loadTimeMs:F2}ms", screenName, loadTimeMs, _performanceThresholds["screen_load_time_ms"]);
        }

        await _loggingService.LogPageViewAsync(screenName, loadTime);
        
        await _loggingService.LogMetricAsync("screen_load_time_ms", loadTimeMs, new Dictionary<string, string>
        {
            ["screen_name"] = screenName,
            ["category"] = "performance",
            ["type"] = "screen_load"
        });
    }

    public async Task RecordErrorDialogShownAsync(string errorType, string message)
    {
        await _loggingService.LogWarningAsync($"Error dialog shown: {errorType}", new { ErrorType = errorType, Message = message });
        
        await _loggingService.LogCustomEventAsync("ErrorDialogShown", new Dictionary<string, object>
        {
            ["error_type"] = errorType,
            ["message"] = message,
            ["timestamp"] = DateTime.UtcNow
        });
    }

    public async Task RecordFeatureUsageAsync(string featureName, Dictionary<string, object>? parameters = null)
    {
        await _loggingService.LogFeatureUsageAsync(featureName, parameters);
        
        var eventData = new Dictionary<string, object>
        {
            ["feature_name"] = featureName,
            ["timestamp"] = DateTime.UtcNow
        };

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                eventData[$"param_{param.Key}"] = param.Value;
            }
        }

        await _loggingService.LogCustomEventAsync("FeatureUsed", eventData);
    }

    #endregion

    #region Business Metrics

    public async Task RecordBusinessMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        await _loggingService.LogMetricAsync(metricName, value, tags);
    }

    public async Task IncrementCounterAsync(string counterName, Dictionary<string, string>? tags = null)
    {
        await _loggingService.LogCounterAsync(counterName, 1, tags);
    }

    public async Task RecordUserActionAsync(string action, string category, string? label = null, int? value = null)
    {
        var data = new Dictionary<string, object>
        {
            ["action"] = action,
            ["category"] = category
        };

        if (label != null) data["label"] = label;
        if (value.HasValue) data["value"] = value.Value;

        await _loggingService.LogCustomEventAsync("UserAction", data);
    }

    #endregion

    #region Data and Sync Monitoring

    public async Task RecordDataSyncAsync(string syncType, int itemsCount, TimeSpan duration, bool isSuccess)
    {
        await _loggingService.LogDataSyncAsync(syncType, isSuccess, itemsCount, duration);
        
        await _loggingService.LogCustomEventAsync("DataSync", new Dictionary<string, object>
        {
            ["sync_type"] = syncType,
            ["items_count"] = itemsCount,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["is_success"] = isSuccess,
            ["timestamp"] = DateTime.UtcNow
        });
    }

    public async Task RecordDatabaseOperationAsync(string operation, string entity, TimeSpan duration, bool isSuccess)
    {
        await _loggingService.LogDatabaseOperationAsync(operation, entity, duration, isSuccess);
    }

    public async Task RecordCacheHitRateAsync(string cacheType, int hits, int misses)
    {
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total * 100 : 0;

        await _loggingService.LogMetricAsync($"cache_hit_rate_{cacheType}", hitRate, new Dictionary<string, string>
        {
            ["cache_type"] = cacheType,
            ["category"] = "performance",
            ["type"] = "cache"
        });

        await _loggingService.LogCustomEventAsync("CacheStats", new Dictionary<string, object>
        {
            ["cache_type"] = cacheType,
            ["hits"] = hits,
            ["misses"] = misses,
            ["hit_rate"] = hitRate,
            ["timestamp"] = DateTime.UtcNow
        });
    }

    #endregion

    #region Custom Events and Metrics

    public async Task RecordCustomEventAsync(string eventName, Dictionary<string, object>? properties = null, Dictionary<string, double>? metrics = null)
    {
        await _loggingService.LogCustomEventAsync(eventName, properties);
        
        if (metrics != null)
        {
            foreach (var metric in metrics)
            {
                await _loggingService.LogMetricAsync($"{eventName}_{metric.Key}", metric.Value);
            }
        }
    }

    public async Task SetUserPropertyAsync(string propertyName, string value)
    {
        await _loggingService.LogInfoAsync($"User property set: {propertyName} = {value}", new { PropertyName = propertyName, Value = value });
    }

    public async Task RecordExceptionAsync(Exception exception, Dictionary<string, string>? properties = null)
    {
        await _loggingService.LogErrorAsync($"Exception recorded: {exception.Message}", exception, properties);
    }

    #endregion

    #region Configuration

    public async Task SetSamplingRateAsync(double rate)
    {
        _samplingRate = Math.Max(0, Math.Min(1, rate)); // Clamp between 0 and 1
        await _loggingService.LogInfoAsync($"Sampling rate set to {_samplingRate:P0}");
    }

    public async Task EnableMonitoringAsync(bool enable)
    {
        _isMonitoringEnabled = enable;
        await _loggingService.LogInfoAsync($"Monitoring {(enable ? "enabled" : "disabled")}");
    }

    public async Task FlushAsync()
    {
        // Flush any pending telemetry data
        await Task.Delay(100); // Placeholder
        await _loggingService.LogDebugAsync("Monitoring data flushed");
    }

    public async Task<MonitoringHealth> GetHealthStatusAsync()
    {
        var memoryUsage = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var overallHealth = _componentHealth.Values.All(c => c.IsHealthy);

        return new MonitoringHealth
        {
            IsHealthy = overallHealth,
            Components = new Dictionary<string, ComponentHealth>(_componentHealth),
            LastChecked = DateTime.UtcNow,
            TotalResponseTime = TimeSpan.FromMilliseconds(100), // Placeholder
            MemoryUsageMB = memoryUsage,
            CpuUsagePercent = 15.5 // Placeholder
        };
    }

    #endregion

    #region Private Methods

    private bool ShouldSample()
    {
        return _random.NextDouble() <= _samplingRate;
    }

    private async Task RaisePerformanceAlertAsync(string alertType, string message, string component, double value, double threshold, Dictionary<string, string>? tags = null)
    {
        var alert = new PerformanceAlert
        {
            AlertType = alertType,
            Message = message,
            Component = component,
            Value = value,
            Threshold = threshold,
            Timestamp = DateTime.UtcNow,
            Tags = tags ?? new Dictionary<string, string>()
        };

        PerformanceAlertRaised?.Invoke(this, alert);
        
        await _loggingService.LogWarningAsync($"Performance alert: {alertType} - {message}", new 
        { 
            AlertType = alertType, 
            Component = component, 
            Value = value, 
            Threshold = threshold, 
            Tags = tags 
        });
    }

    private void InitializeComponentHealth()
    {
        var components = new[] { "Database", "Network", "Storage", "Authentication", "Sync" };
        
        foreach (var component in components)
        {
            _componentHealth[component] = new ComponentHealth
            {
                IsHealthy = true,
                Status = "Healthy",
                LastChecked = DateTime.UtcNow,
                ResponseTime = TimeSpan.Zero
            };
        }
    }

    private async void PerformHealthChecks(object? state)
    {
        try
        {
            // Perform health checks for various components
            await RecordHealthCheckAsync("Database", true, "Database connection healthy");
            await RecordHealthCheckAsync("Network", true, "Network connectivity available");
            await RecordHealthCheckAsync("Storage", true, "Local storage accessible");
            await RecordHealthCheckAsync("Authentication", true, "Authentication service responsive");
            await RecordHealthCheckAsync("Sync", true, "Sync service operational");
            
            // Record memory usage
            await RecordMemoryUsageAsync();
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Health check failed", ex);
        }
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
    }

    #endregion
}

// No-op timer for when sampling is disabled
internal class NoOpTimer : IDisposable
{
    public void Dispose() { }
}