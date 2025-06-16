using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MauiApp.Services;

public class DataService : IDataService
{
    private readonly ILogger<DataService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataService(ILogger<DataService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing data service");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing data service");
            throw;
        }
    }

    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            _logger.LogInformation("Getting item with key: {Key}", key);
            var json = await SecureStorage.GetAsync(key);
            
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item with key: {Key}", key);
            return default;
        }
    }

    public async Task SaveItemAsync<T>(string key, T item)
    {
        try
        {
            _logger.LogInformation("Saving item with key: {Key}", key);
            var json = JsonSerializer.Serialize(item, _jsonOptions);
            await SecureStorage.SetAsync(key, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving item with key: {Key}", key);
            throw;
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            _logger.LogInformation("Removing item with key: {Key}", key);
            SecureStorage.Remove(key);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item with key: {Key}", key);
            throw;
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            _logger.LogInformation("Clearing all data");
            SecureStorage.RemoveAll();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all data");
            throw;
        }
    }
}