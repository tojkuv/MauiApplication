using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MauiApp.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            _logger.LogInformation("GET request to: {Endpoint}", endpoint);
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET request to: {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        try
        {
            _logger.LogInformation("POST request to: {Endpoint}", endpoint);
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST request to: {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        try
        {
            _logger.LogInformation("PUT request to: {Endpoint}", endpoint);
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PUT request to: {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task DeleteAsync(string endpoint)
    {
        try
        {
            _logger.LogInformation("DELETE request to: {Endpoint}", endpoint);
            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DELETE request to: {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            _logger.LogInformation("Checking API health");
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API health check failed");
            return false;
        }
    }
}