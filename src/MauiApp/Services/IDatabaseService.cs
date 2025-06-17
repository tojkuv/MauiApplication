namespace MauiApp.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<bool> IsDatabaseCreatedAsync();
    Task<string> GetDatabasePathAsync();
    Task<long> GetDatabaseSizeAsync();
    Task ClearAllDataAsync();
    Task<DatabaseInfo> GetDatabaseInfoAsync();
}

public class DatabaseInfo
{
    public string Path { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public int ProjectCount { get; set; }
    public int TaskCount { get; set; }
    public int FileCount { get; set; }
    public int MessageCount { get; set; }
    public int NotificationCount { get; set; }
    public int PendingChanges { get; set; }
}