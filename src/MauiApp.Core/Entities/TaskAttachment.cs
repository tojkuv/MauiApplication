using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class TaskAttachment : IHasId
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public Guid TaskId { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ProjectTask Task { get; set; } = null!;
    public virtual ApplicationUser UploadedBy { get; set; } = null!;
}