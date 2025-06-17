using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class Milestone : IHasId
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Project Project { get; set; } = null!;
}