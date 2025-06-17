namespace MauiApp.Core.Entities;

public class FileShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FileId { get; set; }
    public Guid SharedById { get; set; }
    public Guid? SharedWithUserId { get; set; }
    public string? SharedWithEmail { get; set; }
    public string ShareType { get; set; } = "view"; // view, edit, download
    public string? ShareToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ProjectFile File { get; set; } = null!;
    public ApplicationUser SharedBy { get; set; } = null!;
    public ApplicationUser? SharedWithUser { get; set; }
}