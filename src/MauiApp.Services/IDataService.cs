namespace MauiApp.Services;

public interface IDataService
{
    Task InitializeAsync();
    Task<T?> GetItemAsync<T>(string key);
    Task SaveItemAsync<T>(string key, T item);
    Task RemoveItemAsync(string key);
    Task ClearAllAsync();
}