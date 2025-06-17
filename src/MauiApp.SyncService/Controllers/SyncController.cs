using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.SyncService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { Status = "Healthy", Service = "Sync", Timestamp = DateTime.UtcNow });
    }

    // Main sync operations
    [HttpPost("sync")]
    public async Task<ActionResult<SyncResponseDto>> ProcessSync([FromBody] SyncRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var response = await _syncService.ProcessSyncRequestAsync(request, userId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sync request");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("delta")]
    public async Task<ActionResult<DeltaSyncResponseDto>> GetDeltaChanges([FromBody] DeltaSyncRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var response = await _syncService.GetDeltaChangesAsync(request, userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delta changes");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("status/{clientId}")]
    public async Task<ActionResult<SyncStatusDto>> GetSyncStatus(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var status = await _syncService.GetSyncStatusAsync(clientId, userId);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Conflict resolution
    [HttpGet("conflicts/{clientId}")]
    public async Task<ActionResult<List<ConflictResolutionDto>>> GetPendingConflicts(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var conflicts = await _syncService.GetPendingConflictsAsync(clientId, userId);
            return Ok(conflicts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending conflicts for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("conflicts/resolve")]
    public async Task<ActionResult<ConflictResolutionResponseDto>> ResolveConflict([FromBody] ConflictResolutionRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            request.UserId = userId;
            var response = await _syncService.ResolveConflictAsync(request, userId);
            
            if (!response.Success)
                return BadRequest(response);
                
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict for entity {EntityId}", request.EntityId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("conflicts/{clientId}/auto-resolve")]
    public async Task<ActionResult<bool>> AutoResolveConflicts(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _syncService.AutoResolveConflictsAsync(clientId, userId);
            return Ok(new { Success = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-resolving conflicts for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Sync item management
    [HttpPost("items")]
    public async Task<ActionResult<SyncItemDto>> CreateSyncItem([FromBody] CreateSyncItemRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var syncItem = await _syncService.CreateSyncItemAsync(
                request.EntityType, 
                request.EntityId, 
                request.Operation, 
                request.Data, 
                userId);
                
            return Ok(syncItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sync item");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("items/{syncItemId}/complete")]
    public async Task<ActionResult> MarkSyncItemCompleted(Guid syncItemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _syncService.MarkSyncItemCompletedAsync(syncItemId, userId);
            if (!success)
                return NotFound();
                
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sync item {SyncItemId} as completed", syncItemId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("items/{syncItemId}/failed")]
    public async Task<ActionResult> MarkSyncItemFailed(Guid syncItemId, [FromBody] MarkFailedRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _syncService.MarkSyncItemFailedAsync(syncItemId, request.ErrorMessage, userId);
            if (!success)
                return NotFound();
                
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sync item {SyncItemId} as failed", syncItemId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("items/{clientId}/pending")]
    public async Task<ActionResult<List<SyncItemDto>>> GetPendingSyncItems(Guid clientId, [FromQuery] int maxItems = 100)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var items = await _syncService.GetPendingSyncItemsAsync(clientId, userId, maxItems);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending sync items for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Configuration
    [HttpGet("configuration")]
    public async Task<ActionResult<SyncConfigurationDto>> GetSyncConfiguration()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var config = await _syncService.GetSyncConfigurationAsync(userId);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync configuration");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("configuration")]
    public async Task<ActionResult> UpdateSyncConfiguration([FromBody] SyncConfigurationDto configuration)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _syncService.UpdateSyncConfigurationAsync(configuration, userId);
            if (!success)
                return BadRequest("Failed to update configuration");
                
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync configuration");
            return StatusCode(500, "Internal server error");
        }
    }

    // Health and monitoring
    [HttpGet("health/{clientId}")]
    public async Task<ActionResult<SyncHealth>> GetSyncHealth(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var health = await _syncService.GetSyncHealthAsync(clientId, userId);
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync health for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("maintenance/{clientId}/retry-failed")]
    public async Task<ActionResult> RetryFailedItems(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            await _syncService.ProcessFailedSyncItemsAsync(clientId, userId);
            return Ok(new { Message = "Failed items queued for retry" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed sync items for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("maintenance/cleanup")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CleanupOldData([FromQuery] int daysOld = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            await _syncService.CleanupOldSyncDataAsync(cutoffDate);
            return Ok(new { Message = $"Cleaned up sync data older than {daysOld} days" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old sync data");
            return StatusCode(500, "Internal server error");
        }
    }

    // Client management
    [HttpPost("clients/register")]
    public async Task<ActionResult<Guid>> RegisterClient([FromBody] RegisterClientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var clientId = await _syncService.RegisterClientAsync(request.ClientInfo, userId);
            return Ok(new { ClientId = clientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering client");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("clients/{clientId}/heartbeat")]
    public async Task<ActionResult> UpdateClientHeartbeat(Guid clientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var success = await _syncService.UpdateClientLastSeenAsync(clientId, userId);
            if (!success)
                return NotFound();
                
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client heartbeat for {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("clients/active")]
    public async Task<ActionResult<List<Guid>>> GetActiveClients()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var clients = await _syncService.GetActiveClientsAsync(userId);
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active clients");
            return StatusCode(500, "Internal server error");
        }
    }

    // Test endpoint
    [HttpGet("test")]
    public async Task<ActionResult> TestEndpoint()
    {
        try
        {
            var userId = GetCurrentUserId();
            var activeClients = await _syncService.GetActiveClientsAsync(userId);
            
            return Ok(new 
            { 
                Message = "Sync Service is working",
                UserId = userId,
                ActiveClientsCount = activeClients.Count,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test endpoint error");
            return StatusCode(500, ex.Message);
        }
    }
}

// Request models
public class CreateSyncItemRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class MarkFailedRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}

public class RegisterClientRequest
{
    public string ClientInfo { get; set; } = string.Empty;
}