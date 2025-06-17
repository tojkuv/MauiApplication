using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.DTOs;

public class ProjectFileDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string FileCategory { get; set; } = "document";
    public string FileType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsArchived { get; set; }
    public string? FolderPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FileVersionDto> Versions { get; set; } = new();
    public List<FileShareDto> Shares { get; set; } = new();
    public List<FileCommentDto> Comments { get; set; } = new();
    public int CommentCount { get; set; }
    public string FileSizeFormatted => FormatFileSize(FileSize);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class FileUploadRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? FolderPath { get; set; }

    public bool GenerateThumbnail { get; set; } = true;
}

public class FileUpdateRequest
{
    [StringLength(255)]
    public string? FileName { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? FolderPath { get; set; }
}

public class FileVersionDto
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string VersionNumber { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ChangeDescription { get; set; }
}

public class FileShareDto
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public Guid SharedById { get; set; }
    public string SharedByName { get; set; } = string.Empty;
    public Guid? SharedWithUserId { get; set; }
    public string? SharedWithUserName { get; set; }
    public string? SharedWithEmail { get; set; }
    public string ShareType { get; set; } = "view";
    public string? ShareToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFileShareRequest
{
    public Guid? SharedWithUserId { get; set; }

    [EmailAddress]
    public string? SharedWithEmail { get; set; }

    [Required]
    [StringLength(20)]
    public string ShareType { get; set; } = "view";

    public DateTime? ExpiresAt { get; set; }
}

public class FileCommentDto
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FileCommentDto> Replies { get; set; } = new();
}

public class CreateFileCommentRequest
{
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }
}

public class UpdateFileCommentRequest
{
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
}

public class FileSearchRequest
{
    public Guid? ProjectId { get; set; }
    public string? SearchTerm { get; set; }
    public string? FileCategory { get; set; }
    public string? FileType { get; set; }
    public string? FolderPath { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class FileStorageStatsDto
{
    public long TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public long UsedStorage { get; set; }
    public long AvailableStorage { get; set; }
    public double StorageUsagePercentage { get; set; }
    public Dictionary<string, int> FileTypeBreakdown { get; set; } = new();
    public Dictionary<string, long> CategorySizeBreakdown { get; set; } = new();
}