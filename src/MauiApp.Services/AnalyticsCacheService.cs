using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MauiApp.Services;

public class AnalyticsCacheService : IAnalyticsCacheService
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
    private readonly ILogger<AnalyticsCacheService> _logger;
    private readonly Timer _cleanupTimer;

    public AnalyticsCacheService(ILogger<AnalyticsCacheService> logger)
    {
        _logger = logger;
        
        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (item.ExpiresAt > DateTime.UtcNow)
                {
                    var value = JsonSerializer.Deserialize<T>(item.Value);
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return Task.FromResult(value);
                }
                else
                {
                    // Item expired, remove it
                    _cache.TryRemove(key, out _);
                    _logger.LogDebug("Cache item expired and removed: {Key}", key);
                }
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var expiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(15));
            var serializedValue = JsonSerializer.Serialize(value);
            
            var cacheItem = new CacheItem
            {
                Value = serializedValue,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            _cache.AddOrUpdate(key, cacheItem, (k, v) => cacheItem);
            _logger.LogDebug("Cache item set: {Key}, expires at {ExpiresAt}", key, expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache item: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            if (_cache.TryRemove(key, out _))
            {
                _logger.LogDebug("Cache item removed: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache item: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var keysToRemove = _cache.Keys.Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.LogDebug("Cache items removed by pattern: {Pattern}, count: {Count}", pattern, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache items by pattern: {Pattern}", pattern);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        try
        {
            var exists = _cache.ContainsKey(key) && _cache[key].ExpiresAt > DateTime.UtcNow;
            _logger.LogDebug("Cache exists check: {Key} = {Exists}", key, exists);
            return Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task<TimeSpan?> GetTtlAsync(string key)
    {
        try
        {
            if (_cache.TryGetValue(key, out var item))
            {
                var ttl = item.ExpiresAt - DateTime.UtcNow;
                return Task.FromResult<TimeSpan?>(ttl > TimeSpan.Zero ? ttl : null);
            }

            return Task.FromResult<TimeSpan?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TTL for cache item: {Key}", key);
            return Task.FromResult<TimeSpan?>(null);
        }
    }

    public async Task RefreshAsync(string key)
    {
        try
        {
            if (_cache.TryGetValue(key, out var item))
            {
                // Extend expiration by original duration
                var originalDuration = item.ExpiresAt - item.CreatedAt;
                item.ExpiresAt = DateTime.UtcNow.Add(originalDuration);
                _logger.LogDebug("Cache item refreshed: {Key}, new expiry: {ExpiresAt}", key, item.ExpiresAt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache item: {Key}", key);
        }

        await Task.CompletedTask;
    }

    public Task WarmUpAsync(List<string> keys)
    {
        try
        {
            _logger.LogInformation("Cache warm-up initiated for {Count} keys", keys.Count);
            
            // In a real implementation, this would pre-load frequently accessed data
            // For now, we'll just log the operation
            foreach (var key in keys)
            {
                _logger.LogDebug("Warming up cache key: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warm-up");
        }

        return Task.CompletedTask;
    }

    private void CleanupExpiredItems(object? state)
    {
        try
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired cache items", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache.Clear();
    }

    private class CacheItem
    {
        public string Value { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}