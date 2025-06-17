namespace MauiApp.Data.Models;

public class LocalFile
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string LocalPath { get; set; } = string.Empty; // Local file path on device
    public string RemoteUrl { get; set; } = string.Empty; // Azure Blob URL
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid UploadedById { get; set; }
    public string FileHash { get; set; } = string.Empty; // For integrity checking
    public bool IsDownloaded { get; set; } = false;
    public bool IsUploaded { get; set; } = false;
    public DateTime UploadedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool HasLocalChanges { get; set; } = false;
}