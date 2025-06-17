namespace MauiApp.Data.Models;

public class LocalTimeEntry
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsBillable { get; set; } = true;
    public decimal HourlyRate { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}