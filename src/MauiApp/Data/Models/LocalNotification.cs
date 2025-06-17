namespace MauiApp.Data.Models;

public class LocalNotification
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Info, Warning, Error, Success
    public Guid UserId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty; // Project, Task, Message, etc.
    public string ActionUrl { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    public string Priority { get; set; } = string.Empty; // Low, Medium, High
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}