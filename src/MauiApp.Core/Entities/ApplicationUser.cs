using Microsoft.AspNetCore.Identity;
using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class ApplicationUser : IdentityUser<Guid>, IHasId
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    PendingVerification = 4
}