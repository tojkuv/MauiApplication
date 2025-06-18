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

    public async Task<ProjectFileDto> UpdateFileAsync(Guid fileId, FileUpdateRequest request, Guid userId)
    {
        var file = await _context.ProjectFiles
            .Include(f => f.Project)
            .Include(f => f.UploadedBy)
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can update
        var canUpdate = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canUpdate)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to update this file");
        }

        // Update metadata
        if (!string.IsNullOrEmpty(request.Description))
        {
            file.Description = request.Description;
        }

        if (!string.IsNullOrEmpty(request.FolderPath))
        {
            file.FolderPath = request.FolderPath;
        }

        if (!string.IsNullOrEmpty(request.FileName) && request.FileName != file.FileName)
        {
            file.FileName = request.FileName;
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToProjectFileDto(file);
    }

    public async Task<Stream> DownloadFileByShareTokenAsync(string shareToken)
    {
        var share = await _context.FileShares
            .Include(s => s.File)
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken && 
                                    s.IsActive && 
                                    !s.File.IsDeleted &&
                                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));

        if (share == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired share token");
        }

        // Update download count
        share.DownloadCount++;
        share.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var containerClient = _blobServiceClient.GetBlobContainerClient(share.File.StorageContainer);
        var blobClient = containerClient.GetBlobClient(share.File.StoragePath);

        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task<string> GenerateDownloadUrlAsync(Guid fileId, Guid userId, TimeSpan? expiry = null)
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

        // Generate SAS URL with expiry (default 1 hour)
        var expiryTime = expiry ?? TimeSpan.FromHours(1);
        var sasUrl = blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiryTime));
        
        return sasUrl.ToString();
    }

    public async Task<ProjectFileDto> UploadFileVersionAsync(Guid fileId, Stream fileStream, string changeDescription, Guid userId)
    {
        var file = await _context.ProjectFiles
            .Include(f => f.Project)
            .Include(f => f.UploadedBy)
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can upload new versions
        var canUpdate = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canUpdate)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to upload new version");
        }

        var versionId = Guid.NewGuid();
        var versionNumber = (file.Versions?.Max(v => v.VersionNumber) ?? 0) + 1;
        var blobName = $"{file.ProjectId}/{fileId}/versions/{versionId}/{file.OriginalFileName}";
        
        // Upload new version to blob storage
        var containerClient = _blobServiceClient.GetBlobContainerClient(FilesContainer);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(fileStream, overwrite: true);

        // Create file version entity
        var fileVersion = new FileVersion
        {
            Id = versionId,
            FileId = fileId,
            UploadedById = userId,
            VersionNumber = versionNumber,
            ChangeDescription = changeDescription,
            FileSize = fileStream.Length,
            BlobUrl = blobClient.Uri.ToString(),
            StoragePath = blobName,
            CreatedAt = DateTime.UtcNow
        };

        _context.FileVersions.Add(fileVersion);

        // Update main file metadata
        file.FileSize = fileStream.Length;
        file.BlobUrl = blobClient.Uri.ToString();
        file.StoragePath = blobName;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToProjectFileDto(file);
    }

    public async Task<IEnumerable<FileVersionDto>> GetFileVersionsAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var versions = await _context.FileVersions
            .Include(v => v.UploadedBy)
            .Where(v => v.FileId == fileId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

        return versions.Select(v => new FileVersionDto
        {
            Id = v.Id,
            FileId = v.FileId,
            VersionNumber = v.VersionNumber,
            ChangeDescription = v.ChangeDescription,
            FileSize = v.FileSize,
            UploadedById = v.UploadedById,
            UploadedByName = v.UploadedBy?.FullName ?? "Unknown",
            CreatedAt = v.CreatedAt
        });
    }

    public async Task<Stream> DownloadFileVersionAsync(Guid fileId, Guid versionId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var version = await _context.FileVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FileId == fileId);

        if (version == null)
        {
            throw new FileNotFoundException("File version not found");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(FilesContainer);
        var blobClient = containerClient.GetBlobClient(version.StoragePath);

        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task<FileShareDto> CreateFileShareAsync(Guid fileId, CreateFileShareRequest request, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can create shares
        var canShare = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canShare)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to share this file");
        }

        var shareToken = Guid.NewGuid().ToString("N")[..16]; // 16 character token
        var share = new FileShare
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            SharedById = userId,
            ShareToken = shareToken,
            SharedWithEmail = request.SharedWithEmail,
            SharedWithUserId = request.SharedWithUserId,
            ShareType = request.ShareType,
            CanDownload = request.CanDownload,
            CanView = request.CanView,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.FileShares.Add(share);
        await _context.SaveChangesAsync();

        // Load with related data
        var savedShare = await _context.FileShares
            .Include(s => s.SharedBy)
            .Include(s => s.SharedWithUser)
            .Include(s => s.File)
            .FirstAsync(s => s.Id == share.Id);

        return new FileShareDto
        {
            Id = savedShare.Id,
            FileId = savedShare.FileId,
            FileName = savedShare.File.FileName,
            SharedById = savedShare.SharedById,
            SharedByName = savedShare.SharedBy?.FullName ?? "Unknown",
            SharedWithUserId = savedShare.SharedWithUserId,
            SharedWithUserName = savedShare.SharedWithUser?.FullName,
            SharedWithEmail = savedShare.SharedWithEmail,
            ShareToken = savedShare.ShareToken,
            ShareType = savedShare.ShareType,
            CanDownload = savedShare.CanDownload,
            CanView = savedShare.CanView,
            ExpiresAt = savedShare.ExpiresAt,
            IsActive = savedShare.IsActive,
            DownloadCount = savedShare.DownloadCount,
            LastAccessedAt = savedShare.LastAccessedAt,
            CreatedAt = savedShare.CreatedAt
        };
    }

    public async Task<bool> RevokeFileShareAsync(Guid shareId, Guid userId)
    {
        var share = await _context.FileShares
            .Include(s => s.File)
            .FirstOrDefaultAsync(s => s.Id == shareId);

        if (share == null)
        {
            return false;
        }

        // Verify access to the file's project
        await VerifyProjectAccessAsync(share.File.ProjectId, userId);

        // Only share creator, file owner, or project admin can revoke
        var canRevoke = share.SharedById == userId || 
                       share.File.UploadedById == userId || 
                       await IsProjectAdminAsync(share.File.ProjectId, userId);

        if (!canRevoke)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to revoke this share");
        }

        share.IsActive = false;
        share.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<FileShareDto>> GetFileSharesAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var shares = await _context.FileShares
            .Include(s => s.SharedBy)
            .Include(s => s.SharedWithUser)
            .Include(s => s.File)
            .Where(s => s.FileId == fileId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return shares.Select(s => new FileShareDto
        {
            Id = s.Id,
            FileId = s.FileId,
            FileName = s.File.FileName,
            SharedById = s.SharedById,
            SharedByName = s.SharedBy?.FullName ?? "Unknown",
            SharedWithUserId = s.SharedWithUserId,
            SharedWithUserName = s.SharedWithUser?.FullName,
            SharedWithEmail = s.SharedWithEmail,
            ShareToken = s.ShareToken,
            ShareType = s.ShareType,
            CanDownload = s.CanDownload,
            CanView = s.CanView,
            ExpiresAt = s.ExpiresAt,
            IsActive = s.IsActive,
            DownloadCount = s.DownloadCount,
            LastAccessedAt = s.LastAccessedAt,
            CreatedAt = s.CreatedAt
        });
    }

    public async Task<ProjectFileDto?> GetSharedFileAsync(string shareToken)
    {
        var share = await _context.FileShares
            .Include(s => s.File)
            .ThenInclude(f => f.Project)
            .Include(s => s.File)
            .ThenInclude(f => f.UploadedBy)
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken && 
                                    s.IsActive && 
                                    !s.File.IsDeleted &&
                                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));

        if (share == null)
        {
            return null;
        }

        return MapToProjectFileDto(share.File);
    }

    public async Task<FileCommentDto> CreateFileCommentAsync(Guid fileId, CreateFileCommentRequest request, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var comment = new FileComment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            AuthorId = userId,
            Content = request.Content,
            ParentCommentId = request.ParentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.FileComments.Add(comment);
        await _context.SaveChangesAsync();

        // Load with related data
        var savedComment = await _context.FileComments
            .Include(c => c.Author)
            .Include(c => c.ParentComment)
            .ThenInclude(pc => pc!.Author)
            .FirstAsync(c => c.Id == comment.Id);

        return new FileCommentDto
        {
            Id = savedComment.Id,
            FileId = savedComment.FileId,
            AuthorId = savedComment.AuthorId,
            AuthorName = savedComment.Author?.FullName ?? "Unknown",
            Content = savedComment.Content,
            ParentCommentId = savedComment.ParentCommentId,
            ParentComment = savedComment.ParentComment != null ? new FileCommentDto
            {
                Id = savedComment.ParentComment.Id,
                AuthorId = savedComment.ParentComment.AuthorId,
                AuthorName = savedComment.ParentComment.Author?.FullName ?? "Unknown",
                Content = savedComment.ParentComment.Content,
                CreatedAt = savedComment.ParentComment.CreatedAt
            } : null,
            IsEdited = savedComment.IsEdited,
            IsDeleted = savedComment.IsDeleted,
            CreatedAt = savedComment.CreatedAt,
            UpdatedAt = savedComment.UpdatedAt
        };
    }

    public async Task<FileCommentDto> UpdateFileCommentAsync(Guid commentId, UpdateFileCommentRequest request, Guid userId)
    {
        var comment = await _context.FileComments
            .Include(c => c.Author)
            .Include(c => c.File)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

        if (comment == null)
        {
            throw new InvalidOperationException("Comment not found");
        }

        // Verify access to file's project
        await VerifyProjectAccessAsync(comment.File.ProjectId, userId);

        // Only comment author can update
        if (comment.AuthorId != userId)
        {
            throw new UnauthorizedAccessException("Only comment author can update the comment");
        }

        comment.Content = request.Content;
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new FileCommentDto
        {
            Id = comment.Id,
            FileId = comment.FileId,
            AuthorId = comment.AuthorId,
            AuthorName = comment.Author?.FullName ?? "Unknown",
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            IsEdited = comment.IsEdited,
            IsDeleted = comment.IsDeleted,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }

    public async Task<bool> DeleteFileCommentAsync(Guid commentId, Guid userId)
    {
        var comment = await _context.FileComments
            .Include(c => c.File)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

        if (comment == null)
        {
            return false;
        }

        // Verify access to file's project
        await VerifyProjectAccessAsync(comment.File.ProjectId, userId);

        // Only comment author or project admin can delete
        var canDelete = comment.AuthorId == userId || await IsProjectAdminAsync(comment.File.ProjectId, userId);
        if (!canDelete)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to delete this comment");
        }

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<FileCommentDto>> GetFileCommentsAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            throw new FileNotFoundException("File not found");
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        var comments = await _context.FileComments
            .Include(c => c.Author)
            .Include(c => c.ParentComment)
            .ThenInclude(pc => pc!.Author)
            .Where(c => c.FileId == fileId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(c => new FileCommentDto
        {
            Id = c.Id,
            FileId = c.FileId,
            AuthorId = c.AuthorId,
            AuthorName = c.Author?.FullName ?? "Unknown",
            Content = c.Content,
            ParentCommentId = c.ParentCommentId,
            ParentComment = c.ParentComment != null ? new FileCommentDto
            {
                Id = c.ParentComment.Id,
                AuthorId = c.ParentComment.AuthorId,
                AuthorName = c.ParentComment.Author?.FullName ?? "Unknown",
                Content = c.ParentComment.Content,
                CreatedAt = c.ParentComment.CreatedAt
            } : null,
            IsEdited = c.IsEdited,
            IsDeleted = c.IsDeleted,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        });
    }

    public async Task<Stream?> GetFileThumbnailAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            return null;
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        if (string.IsNullOrEmpty(file.ThumbnailUrl))
        {
            return null;
        }

        try
        {
            var thumbnailContainer = _blobServiceClient.GetBlobContainerClient(ThumbnailsContainer);
            var thumbnailName = $"{file.ProjectId}/{file.Id}/thumbnail.jpg";
            var thumbnailClient = thumbnailContainer.GetBlobClient(thumbnailName);

            var response = await thumbnailClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve thumbnail for file {FileId}", fileId);
            return null;
        }
    }

    public async Task<bool> MoveFileAsync(Guid fileId, string newFolderPath, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            return false;
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can move
        var canMove = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canMove)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to move this file");
        }

        file.FolderPath = newFolderPath;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveFileAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            return false;
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can archive
        var canArchive = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canArchive)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to archive this file");
        }

        file.IsArchived = true;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreFileAsync(Guid fileId, Guid userId)
    {
        var file = await _context.ProjectFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
        {
            return false;
        }

        // Verify access
        await VerifyProjectAccessAsync(file.ProjectId, userId);

        // Only file uploader or project admin can restore
        var canRestore = file.UploadedById == userId || await IsProjectAdminAsync(file.ProjectId, userId);
        if (!canRestore)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to restore this file");
        }

        file.IsArchived = false;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<FileStorageStatsDto> GetProjectStorageStatsAsync(Guid projectId, Guid userId)
    {
        // Verify access
        await VerifyProjectAccessAsync(projectId, userId);

        var files = await _context.ProjectFiles
            .Where(f => f.ProjectId == projectId && !f.IsDeleted)
            .ToListAsync();

        var totalFiles = files.Count;
        var totalSize = files.Sum(f => f.FileSize);
        var archivedFiles = files.Count(f => f.IsArchived);
        var archivedSize = files.Where(f => f.IsArchived).Sum(f => f.FileSize);

        var categoryBreakdown = files
            .GroupBy(f => f.FileCategory)
            .ToDictionary(g => g.Key, g => new FileCategoryStatsDto
            {
                Category = g.Key,
                FileCount = g.Count(),
                TotalSize = g.Sum(f => f.FileSize),
                AverageSize = g.Average(f => f.FileSize)
            });

        return new FileStorageStatsDto
        {
            TotalFiles = totalFiles,
            TotalSize = totalSize,
            ArchivedFiles = archivedFiles,
            ArchivedSize = archivedSize,
            ActiveFiles = totalFiles - archivedFiles,
            ActiveSize = totalSize - archivedSize,
            CategoryBreakdown = categoryBreakdown.Values.ToList(),
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<FileStorageStatsDto> GetUserStorageStatsAsync(Guid userId)
    {
        // Get all projects the user has access to
        var userProjectIds = await _context.ProjectMembers
            .Where(pm => pm.UserId == userId && pm.IsActive)
            .Select(pm => pm.ProjectId)
            .ToListAsync();

        var files = await _context.ProjectFiles
            .Where(f => userProjectIds.Contains(f.ProjectId) && !f.IsDeleted)
            .ToListAsync();

        var userUploadedFiles = files.Where(f => f.UploadedById == userId).ToList();

        var totalFiles = files.Count;
        var totalSize = files.Sum(f => f.FileSize);
        var userFiles = userUploadedFiles.Count;
        var userSize = userUploadedFiles.Sum(f => f.FileSize);

        var categoryBreakdown = userUploadedFiles
            .GroupBy(f => f.FileCategory)
            .ToDictionary(g => g.Key, g => new FileCategoryStatsDto
            {
                Category = g.Key,
                FileCount = g.Count(),
                TotalSize = g.Sum(f => f.FileSize),
                AverageSize = g.Average(f => f.FileSize)
            });

        return new FileStorageStatsDto
        {
            TotalFiles = userFiles,
            TotalSize = userSize,
            ArchivedFiles = userUploadedFiles.Count(f => f.IsArchived),
            ArchivedSize = userUploadedFiles.Where(f => f.IsArchived).Sum(f => f.FileSize),
            ActiveFiles = userUploadedFiles.Count(f => !f.IsArchived),
            ActiveSize = userUploadedFiles.Where(f => !f.IsArchived).Sum(f => f.FileSize),
            CategoryBreakdown = categoryBreakdown.Values.ToList(),
            LastUpdated = DateTime.UtcNow
        };
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