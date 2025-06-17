using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface IFilesService
{
    // File operations
    Task<ProjectFileDto> UploadFileAsync(Stream fileStream, string fileName, string contentType, FileUploadRequest request, Guid userId);
    Task<ProjectFileDto> UpdateFileAsync(Guid fileId, FileUpdateRequest request, Guid userId);
    Task<bool> DeleteFileAsync(Guid fileId, Guid userId);
    Task<ProjectFileDto?> GetFileByIdAsync(Guid fileId, Guid userId);
    Task<IEnumerable<ProjectFileDto>> SearchFilesAsync(FileSearchRequest request, Guid userId);

    // File download and streaming
    Task<Stream> DownloadFileAsync(Guid fileId, Guid userId);
    Task<Stream> DownloadFileByShareTokenAsync(string shareToken);
    Task<string> GenerateDownloadUrlAsync(Guid fileId, Guid userId, TimeSpan? expiry = null);

    // File versions
    Task<ProjectFileDto> UploadFileVersionAsync(Guid fileId, Stream fileStream, string changeDescription, Guid userId);
    Task<IEnumerable<FileVersionDto>> GetFileVersionsAsync(Guid fileId, Guid userId);
    Task<Stream> DownloadFileVersionAsync(Guid fileId, Guid versionId, Guid userId);

    // File sharing
    Task<FileShareDto> CreateFileShareAsync(Guid fileId, CreateFileShareRequest request, Guid userId);
    Task<bool> RevokeFileShareAsync(Guid shareId, Guid userId);
    Task<IEnumerable<FileShareDto>> GetFileSharesAsync(Guid fileId, Guid userId);
    Task<ProjectFileDto?> GetSharedFileAsync(string shareToken);

    // File comments
    Task<FileCommentDto> CreateFileCommentAsync(Guid fileId, CreateFileCommentRequest request, Guid userId);
    Task<FileCommentDto> UpdateFileCommentAsync(Guid commentId, UpdateFileCommentRequest request, Guid userId);
    Task<bool> DeleteFileCommentAsync(Guid commentId, Guid userId);
    Task<IEnumerable<FileCommentDto>> GetFileCommentsAsync(Guid fileId, Guid userId);

    // Thumbnails and previews
    Task<Stream?> GetFileThumbnailAsync(Guid fileId, Guid userId);
    Task<bool> GenerateThumbnailAsync(Guid fileId);

    // File organization
    Task<bool> MoveFileAsync(Guid fileId, string newFolderPath, Guid userId);
    Task<bool> ArchiveFileAsync(Guid fileId, Guid userId);
    Task<bool> RestoreFileAsync(Guid fileId, Guid userId);

    // Storage statistics
    Task<FileStorageStatsDto> GetProjectStorageStatsAsync(Guid projectId, Guid userId);
    Task<FileStorageStatsDto> GetUserStorageStatsAsync(Guid userId);

    // File validation and processing
    Task<bool> ValidateFileAsync(string fileName, string contentType, long fileSize);
    string DetermineFileCategory(string contentType, string fileName);
    string GetFileType(string fileName);
}