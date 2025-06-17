using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class Project : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ApplicationUser Owner { get; set; } = null!;
    public virtual ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public virtual ICollection<ProjectFile> Files { get; set; } = new List<ProjectFile>();
}

public enum ProjectStatus
{
    Planning = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}