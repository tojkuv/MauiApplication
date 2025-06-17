namespace MauiApp.Services;

public interface IOfflineSyncService
{
    // Sync status
    Task<bool> IsOnlineAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
    Task<int> GetPendingChangesCountAsync();
    
    // Manual sync operations
    Task<SyncResult> SyncAllAsync();
    Task<SyncResult> SyncProjectsAsync();
    Task<SyncResult> SyncTasksAsync();
    Task<SyncResult> SyncFilesAsync();
    Task<SyncResult> SyncMessagesAsync();
    Task<SyncResult> SyncNotificationsAsync();
    
    // Push local changes to server
    Task<SyncResult> PushLocalChangesAsync();
    
    // Pull server changes to local
    Task<SyncResult> PullServerChangesAsync();
    
    // Conflict resolution
    Task<SyncResult> ResolveConflictsAsync();
    Task<IEnumerable<SyncConflict>> GetConflictsAsync();
    
    // Background sync
    Task StartBackgroundSyncAsync();
    Task StopBackgroundSyncAsync();
    
    // Events
    event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
}

public class SyncResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int SyncedItems { get; set; }
    public int FailedItems { get; set; }
    public int ConflictItems { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class SyncConflict
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string LocalData { get; set; } = string.Empty;
    public string ServerData { get; set; } = string.Empty;
    public DateTime LocalModifiedAt { get; set; }
    public DateTime ServerModifiedAt { get; set; }
    public string ConflictType { get; set; } = string.Empty; // Update, Delete, etc.
}

public class SyncProgressEventArgs : EventArgs
{
    public string EntityType { get; set; } = string.Empty;
    public int Current { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = new();
}