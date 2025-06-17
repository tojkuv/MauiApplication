namespace MauiApp.Data.Models;

public class LocalSyncStatus
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string SyncStatus { get; set; } = string.Empty; // Pending, InProgress, Completed, Failed, Conflict
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime LastSyncAttempt { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}