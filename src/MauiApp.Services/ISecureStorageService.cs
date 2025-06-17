namespace MauiApp.Services;

public interface ISecureStorageService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
    Task RemoveAllAsync();
    Task<bool> ContainsKeyAsync(string key);
}