namespace MauiApp.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
    Task<bool> ExistsAsync(string key);
    Task ClearAsync();
}

public class CacheService : ICacheService
{
    private readonly Dictionary<string, CacheItem> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                return item.Value as T;
            }

            // Remove expired item
            if (item?.IsExpired == true)
            {
                _cache.Remove(key);
            }

            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        await _semaphore.WaitAsync();
        try
        {
            var item = new CacheItem
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expiration)
            };

            _cache[key] = item;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            _cache.Remove(key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        await _semaphore.WaitAsync();
        try
        {
            var keysToRemove = _cache.Keys
                .Where(key => key.Contains(pattern.Replace("*", "")))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _cache.TryGetValue(key, out var item) && !item.IsExpired;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private class CacheItem
    {
        public object? Value { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}