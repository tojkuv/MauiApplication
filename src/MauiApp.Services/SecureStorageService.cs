using Microsoft.Extensions.Logging;

namespace MauiApp.Services;

public class SecureStorageService : ISecureStorageService
{
    private readonly ILogger<SecureStorageService> _logger;

    public SecureStorageService(ILogger<SecureStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting secure storage value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secure storage value for key: {Key}", key);
            throw;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            SecureStorage.Remove(key);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing secure storage value for key: {Key}", key);
            throw;
        }
    }

    public async Task RemoveAllAsync()
    {
        try
        {
            SecureStorage.RemoveAll();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing all secure storage values");
            throw;
        }
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if secure storage contains key: {Key}", key);
            return false;
        }
    }
}