using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class RefreshToken : IHasId
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; } = false;
    public bool IsInvalidated { get; set; } = false;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}