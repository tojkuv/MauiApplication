using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class UserConnection : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
}