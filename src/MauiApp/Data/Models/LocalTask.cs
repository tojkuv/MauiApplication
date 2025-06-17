namespace MauiApp.Data.Models;

public class LocalTask
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? AssignedToId { get; set; }
    public string Status { get; set; } = string.Empty; // To Do, In Progress, Review, Completed
    public string Priority { get; set; } = string.Empty; // Low, Medium, High, Critical
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string Tags { get; set; } = string.Empty; // JSON array of tags
    public Guid? ParentTaskId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}