using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class TimeEntry : IHasId
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsBillable { get; set; } = true;
    public decimal? HourlyRate { get; set; }
    
    // Navigation properties
    public virtual ProjectTask Task { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}