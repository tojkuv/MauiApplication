namespace MauiApp.Data.Models;

public class LocalProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Budget { get; set; }
    public string Color { get; set; } = string.Empty;
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}