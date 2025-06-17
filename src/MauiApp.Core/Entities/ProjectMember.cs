using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class ProjectMember : IHasId
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}

public enum ProjectRole
{
    Owner = 1,
    Admin = 2,
    Manager = 3,
    Developer = 4,
    Contributor = 5,
    Viewer = 6
}