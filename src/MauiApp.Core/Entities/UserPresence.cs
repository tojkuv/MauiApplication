using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class UserPresence : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = "offline"; // online, offline, away, busy
    public string? Activity { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public Project Project { get; set; } = null!;
}