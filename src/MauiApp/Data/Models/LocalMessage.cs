namespace MauiApp.Data.Models;

public class LocalMessage
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? ChannelId { get; set; }
    public string MessageType { get; set; } = string.Empty; // Text, File, System, etc.
    public string Attachments { get; set; } = string.Empty; // JSON array of file IDs
    public Guid? ReplyToMessageId { get; set; }
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime Timestamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}