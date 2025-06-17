using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.FilesService.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MauiApp.FilesService.Services;

public class FilesService : IFilesService
{
    private readonly FilesDbContext _context;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<FilesService> _logger;
    private const string FilesContainer = "project-files";
    private const string ThumbnailsContainer = "thumbnails";

    public FilesService(FilesDbContext context, BlobServiceClient blobServiceClient, ILogger<FilesService> logger)
    {
        _context = context;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<ProjectFileDto> UploadFileAsync(Stream fileStream, string fileName, string contentType, FileUploadRequest request, Guid userId)
    {
        // Verify project access
        await VerifyProjectAccessAsync(request.ProjectId, userId);

        // Validate file
        if (!await ValidateFileAsync(fileName, contentType, fileStream.Length))
        {
            throw new ArgumentException("Invalid file");
        }

        var fileId = Guid.NewGuid();
        var blobName = $"{request.ProjectId}/{fileId}/{fileName}";
        
        // Upload to blob storage
        var containerClient = _blobServiceClient.GetBlobContainerClient(FilesContainer);
        await containerClient.CreateIfNotExistsAsync();
        
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(fileStream, overwrite: true);

        // Create file entity
        var projectFile = new ProjectFile
        {
            Id = fileId,
            ProjectId = request.ProjectId,
            UploadedById = userId,
            FileName = fileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileStream.Length,
            BlobUrl = blobClient.Uri.ToString(),
            FileCategory = DetermineFileCategory(contentType, fileName),
            FileType = GetFileType(fileName),
            Description = request.Description,
            FolderPath = request.FolderPath,
            StorageContainer = FilesContainer,
            StoragePath = blobName
        };

        _context.ProjectFiles.Add(projectFile);
        await _context.SaveChangesAsync();

        // Generate thumbnail if requested and file is an image
        if (request.GenerateThumbnail && projectFile.FileCategory == "image")
        {
            try
            {
                await GenerateThumbnailAsync(fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate thumbnail for file {FileId}", fileId);
            }
        }

        return await GetFileByIdAsync(fileId, userId) ?? throw new InvalidOperationException("Failed to retrieve uploaded file");
    }

    public async Task<ProjectFileDto?> GetFileByIdAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .Include(f => f.Project)
            .Include(f => f.UploadedBy)
            .Include(f => f.Versions).ThenInclude(v => v.UploadedBy)
            .Include(f => f.Shares).ThenInclude(s => s.SharedBy)
            .Include(f => f.Shares).ThenInclude(s => s.SharedWithUser)
            .Include(f => f.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null) return null;

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        return MapToProjectFileDto(file);
    }

    public async Task<Stream> DownloadFileAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var containerClient = _blobServiceClient.GetBlobContainerClient(file.StorageContainer);
        var blobClient = containerClient.GetBlobClient(file.StoragePath);

        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task<bool> DeleteFileAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null) return false;

        // Verify access and permissions
        await VerifyProjectAccessAsync(file.ProjectId, userId);
        
        // Only file uploader or project admin can delete
        var canDelete = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canDelete)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to delete this file");
        }

        // Soft delete
        file.IsDeleted = true;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProjectFileDto>> SearchFilesAsync(FileSearchRequest request, Guid userId)
    {
        var query = _context.ProjectFiles
            .Include(f => f.Project)
            .Include(f => f.UploadedBy)
            .Where(f => !f.IsDeleted);

        if (request.ProjectId.HasValue)
        {
            // Verify access to specific project
            await VerifyProjectAccessAsync(request.ProjectId.Value, userId);
            query = query.Where(f => f.ProjectId == request.ProjectId.Value);
        }
        else
        {
            // Filter to only projects user has access to
            query = query.Where(f => f.Project.Members.Any(m => m.UserId == userId && m.IsActive));
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            query = query.Where(f => f.FileName.Contains(request.SearchTerm) || 
                                   f.Description != null && f.Description.Contains(request.SearchTerm));
        }

        if (!string.IsNullOrEmpty(request.FileCategory))
        {
            query = query.Where(f => f.FileCategory == request.FileCategory);
        }

        if (!string.IsNullOrEmpty(request.FileType))
        {
            query = query.Where(f => f.FileType == request.FileType);
        }

        if (!string.IsNullOrEmpty(request.FolderPath))
        {
            query = query.Where(f => f.FolderPath == request.FolderPath);
        }

        if (request.CreatedAfter.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= request.CreatedAfter.Value);
        }

        if (request.CreatedBefore.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= request.CreatedBefore.Value);
        }

        if (request.MinFileSize.HasValue)
        {
            query = query.Where(f => f.FileSize >= request.MinFileSize.Value);
        }

        if (request.MaxFileSize.HasValue)
        {
            query = query.Where(f => f.FileSize <= request.MaxFileSize.Value);
        }

        // Apply sorting
        query = request.SortBy.ToLower() switch
        {
            "filename" => request.SortDescending ? query.OrderByDescending(f => f.FileName) : query.OrderBy(f => f.FileName),
            "filesize" => request.SortDescending ? query.OrderByDescending(f => f.FileSize) : query.OrderBy(f => f.FileSize),
            "createdat" => request.SortDescending ? query.OrderByDescending(f => f.CreatedAt) : query.OrderBy(f => f.CreatedAt),
            _ => query.OrderByDescending(f => f.CreatedAt)
        };

        var files = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return files.Select(MapToProjectFileDto);
    }

    public async Task<bool> GenerateThumbnailAsync(Guid fileId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null || file.FileCategory != "image")
        {
            return false;
        }

        try
        {
            // Download original image
            var containerClient = _blobServiceClient.GetBlobContainerClient(file.StorageContainer);
            var blobClient = containerClient.GetBlobClient(file.StoragePath);
            var stream = await blobClient.OpenReadAsync();

            // Generate thumbnail
            using var image = await Image.LoadAsync(stream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(200, 200),
                Mode = ResizeMode.Crop
            }));

            // Upload thumbnail
            var thumbnailContainer = _blobServiceClient.GetBlobContainerClient(ThumbnailsContainer);
            await thumbnailContainer.CreateIfNotExistsAsync();
            
            var thumbnailName = $"{file.ProjectId}/{file.Id}/thumbnail.jpg";
            var thumbnailClient = thumbnailContainer.GetBlobClient(thumbnailName);

            using var thumbnailStream = new MemoryStream();
            await image.SaveAsJpegAsync(thumbnailStream);
            thumbnailStream.Position = 0;

            await thumbnailClient.UploadAsync(thumbnailStream, overwrite: true);

            // Update file with thumbnail URL
            file.ThumbnailUrl = thumbnailClient.Uri.ToString();
            file.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for file {FileId}", fileId);
            return false;
        }
    }

    // Additional implementation methods would go here...
    // For brevity, I'm including just the core methods

    public Task<ProjectFileDto> UpdateFileAsync(Guid fileId, FileUpdateRequest request, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadFileByShareTokenAsync(string shareToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GenerateDownloadUrlAsync(Guid fileId, Guid userId, TimeSpan? expiry = null)
    {
        throw new NotImplementedException();
    }

    public Task<ProjectFileDto> UploadFileVersionAsync(Guid fileId, Stream fileStream, string changeDescription, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<FileVersionDto>> GetFileVersionsAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadFileVersionAsync(Guid fileId, Guid versionId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<FileShareDto> CreateFileShareAsync(Guid fileId, CreateFileShareRequest request, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> RevokeFileShareAsync(Guid shareId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<FileShareDto>> GetFileSharesAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<ProjectFileDto?> GetSharedFileAsync(string shareToken)
    {
        throw new NotImplementedException();
    }

    public Task<FileCommentDto> CreateFileCommentAsync(Guid fileId, CreateFileCommentRequest request, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<FileCommentDto> UpdateFileCommentAsync(Guid commentId, UpdateFileCommentRequest request, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteFileCommentAsync(Guid commentId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<FileCommentDto>> GetFileCommentsAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<Stream?> GetFileThumbnailAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> MoveFileAsync(Guid fileId, string newFolderPath, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ArchiveFileAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> RestoreFileAsync(Guid fileId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<FileStorageStatsDto> GetProjectStorageStatsAsync(Guid projectId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<FileStorageStatsDto> GetUserStorageStatsAsync(Guid userId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ValidateFileAsync(string fileName, string contentType, long fileSize)
    {
        // Check file size (50MB max)
        if (fileSize > 50 * 1024 * 1024)
        {
            return false;
        }

        // Check allowed file types
        var allowedTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf", "text/plain", "text/csv",
            "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/zip", "application/x-zip-compressed"
        };

        return allowedTypes.Contains(contentType.ToLower());
    }

    public string DetermineFileCategory(string contentType, string fileName)
    {
        return contentType.ToLower() switch
        {
            var ct when ct.StartsWith("image/") => "image",
            var ct when ct.StartsWith("video/") => "video",
            var ct when ct.StartsWith("audio/") => "audio",
            "application/pdf" => "document",
            var ct when ct.Contains("word") || ct.Contains("excel") || ct.Contains("powerpoint") => "document",
            var ct when ct.Contains("zip") || ct.Contains("rar") || ct.Contains("7z") => "archive",
            _ => "document"
        };
    }

    public string GetFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        return extension.TrimStart('.');
    }

    // Helper methods
    private async Task VerifyProjectAccessAsync(Guid projectId, Guid userId)
    {
        var hasAccess = await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this project");
        }
    }

    private async Task<bool> IsProjectAdminAsync(Guid projectId, Guid userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && 
                           pm.UserId == userId && 
                           pm.IsActive &&
                           (pm.Role == ProjectRole.Owner || pm.Role == ProjectRole.Admin));
    }

    private static ProjectFileDto MapToProjectFileDto(ProjectFile file)
    {
        return new ProjectFileDto
        {
            Id = file.Id,
            ProjectId = file.ProjectId,
            ProjectName = file.Project?.Name ?? "Unknown",
            UploadedById = file.UploadedById,
            UploadedByName = file.UploadedBy?.FullName ?? "Unknown",
            UploadedByEmail = file.UploadedBy?.Email ?? "",
            FileName = file.FileName,
            OriginalFileName = file.OriginalFileName,
            ContentType = file.ContentType,
            FileSize = file.FileSize,
            BlobUrl = file.BlobUrl,
            ThumbnailUrl = file.ThumbnailUrl,
            FileCategory = file.FileCategory,
            FileType = file.FileType,
            Description = file.Description,
            IsDeleted = file.IsDeleted,
            IsArchived = file.IsArchived,
            FolderPath = file.FolderPath,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt,
            CommentCount = file.Comments?.Count ?? 0
        };
    }
}