using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class ProjectFile : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UploadedById { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string FileCategory { get; set; } = "document"; // document, image, video, audio, archive
    public string FileType { get; set; } = string.Empty; // pdf, doc, jpg, etc.
    public string? Description { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    public string? FolderPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string StorageContainer { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public ApplicationUser UploadedBy { get; set; } = null!;
    public List<FileVersion> Versions { get; set; } = new();
    public List<FileShare> Shares { get; set; } = new();
    public List<FileComment> Comments { get; set; } = new();
}
