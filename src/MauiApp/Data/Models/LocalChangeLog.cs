namespace MauiApp.Data.Models;

public class LocalChangeLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Data { get; set; } = string.Empty; // JSON serialized entity data
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsSynced { get; set; } = false;
    public DateTime? SyncedAt { get; set; }
}