using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.FilesService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFilesService _filesService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFilesService filesService, ILogger<FilesController> logger)
    {
        _filesService = filesService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file to a project
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ProjectFileDto>> UploadFile([FromForm] FileUploadRequest request, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            var userId = GetCurrentUserId();
            
            using var stream = file.OpenReadStream();
            var result = await _filesService.UploadFileAsync(
                stream, 
                file.FileName, 
                file.ContentType, 
                request, 
                userId);

            _logger.LogInformation("File uploaded: {FileName} ({FileId}) by user: {UserId}", 
                file.FileName, result.Id, userId);

            return CreatedAtAction(nameof(GetFile), new { fileId = result.Id }, result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { message = "An error occurred while uploading the file" });
        }
    }

    /// <summary>
    /// Get file by ID
    /// </summary>
    [HttpGet("{fileId}")]
    public async Task<ActionResult<ProjectFileDto>> GetFile(Guid fileId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var file = await _filesService.GetFileByIdAsync(fileId, userId);

            if (file == null)
            {
                return NotFound(new { message = "File not found" });
            }

            return Ok(file);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file {FileId}", fileId);
            return StatusCode(500, new { message = "An error occurred while getting the file" });
        }
    }

    /// <summary>
    /// Download file
    /// </summary>
    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(Guid fileId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var file = await _filesService.GetFileByIdAsync(fileId, userId);

            if (file == null)
            {
                return NotFound(new { message = "File not found" });
            }

            var stream = await _filesService.DownloadFileAsync(fileId, userId);
            
            return File(stream, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileId);
            return StatusCode(500, new { message = "An error occurred while downloading the file" });
        }
    }

    /// <summary>
    /// Search files
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ProjectFileDto>>> SearchFiles([FromQuery] FileSearchRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var files = await _filesService.SearchFilesAsync(request, userId);
            return Ok(files);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching files");
            return StatusCode(500, new { message = "An error occurred while searching files" });
        }
    }

    /// <summary>
    /// Delete file
    /// </summary>
    [HttpDelete("{fileId}")]
    public async Task<ActionResult> DeleteFile(Guid fileId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _filesService.DeleteFileAsync(fileId, userId);

            if (!result)
            {
                return NotFound(new { message = "File not found" });
            }

            _logger.LogInformation("File deleted: {FileId} by user: {UserId}", fileId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", fileId);
            return StatusCode(500, new { message = "An error occurred while deleting the file" });
        }
    }

    /// <summary>
    /// Get file thumbnail
    /// </summary>
    [HttpGet("{fileId}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(Guid fileId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var thumbnailStream = await _filesService.GetFileThumbnailAsync(fileId, userId);

            if (thumbnailStream == null)
            {
                return NotFound(new { message = "Thumbnail not found" });
            }

            return File(thumbnailStream, "image/jpeg");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thumbnail for file {FileId}", fileId);
            return StatusCode(500, new { message = "An error occurred while getting the thumbnail" });
        }
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { Status = "Healthy", Service = "Files", Timestamp = DateTime.UtcNow });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token");
        }
        return userId;
    }
}