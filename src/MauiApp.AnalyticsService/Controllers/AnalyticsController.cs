using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.AnalyticsService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
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
        return Ok(new { Status = "Healthy", Service = "Analytics", Timestamp = DateTime.UtcNow });
    }

    // Project Analytics
    [HttpGet("projects/{projectId}")]
    public async Task<ActionResult<ProjectAnalyticsDto>> GetProjectAnalytics(
        Guid projectId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string granularity = "daily")
    {
        try
        {
            var request = new AnalyticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                ProjectId = projectId,
                Granularity = granularity
            };

            var result = await _analyticsService.GetProjectAnalyticsAsync(projectId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project analytics for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("projects/multiple")]
    public async Task<ActionResult<IEnumerable<ProjectAnalyticsDto>>> GetMultipleProjectAnalytics(
        [FromBody] List<Guid> projectIds,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string granularity = "daily")
    {
        try
        {
            var request = new AnalyticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                Granularity = granularity
            };

            var result = await _analyticsService.GetMultipleProjectAnalyticsAsync(projectIds, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple project analytics");
            return StatusCode(500, "Internal server error");
        }
    }

    // User Analytics
    [HttpGet("users/{userId}/productivity")]
    public async Task<ActionResult<UserProductivityDto>> GetUserProductivity(
        Guid userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string granularity = "daily")
    {
        try
        {
            var request = new AnalyticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                Granularity = granularity
            };

            var result = await _analyticsService.GetUserProductivityAsync(userId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user productivity for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("users/me/productivity")]
    public async Task<ActionResult<UserProductivityDto>> GetMyProductivity(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string granularity = "daily")
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        return await GetUserProductivity(userId, startDate, endDate, granularity);
    }

    [HttpGet("projects/{projectId}/team/productivity")]
    public async Task<ActionResult<IEnumerable<UserProductivityDto>>> GetTeamProductivity(
        Guid projectId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string granularity = "daily")
    {
        try
        {
            var request = new AnalyticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                ProjectId = projectId,
                Granularity = granularity
            };

            var result = await _analyticsService.GetTeamProductivityAsync(projectId, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team productivity for project {ProjectId}", projectId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Quick Stats & Dashboard
    [HttpGet("users/me/quick-stats")]
    public async Task<ActionResult<QuickStats>> GetMyQuickStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _analyticsService.GetQuickStatsAsync(userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quick stats for user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("users/me/recent-activities")]
    public async Task<ActionResult<List<RecentActivity>>> GetMyRecentActivities(
        [FromQuery] int count = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _analyticsService.GetRecentActivitiesAsync(userId, count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activities for user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("users/me/upcoming-deadlines")]
    public async Task<ActionResult<List<UpcomingDeadline>>> GetMyUpcomingDeadlines(
        [FromQuery] int days = 7)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var result = await _analyticsService.GetUpcomingDeadlinesAsync(userId, days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming deadlines for user");
            return StatusCode(500, "Internal server error");
        }
    }

    // Cache Management
    [HttpPost("cache/refresh")]
    public async Task<ActionResult> RefreshCache(
        [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? userId = null)
    {
        try
        {
            await _analyticsService.RefreshAnalyticsCacheAsync(projectId, userId);
            return Ok(new { Message = "Cache refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing analytics cache");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("cache/status")]
    public async Task<ActionResult<bool>> CheckCacheStatus([FromQuery] string cacheKey)
    {
        try
        {
            var isValid = await _analyticsService.IsCacheValidAsync(cacheKey);
            return Ok(new { CacheKey = cacheKey, IsValid = isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache status for key {CacheKey}", cacheKey);
            return StatusCode(500, "Internal server error");
        }
    }

    // Data Aggregation Endpoints (typically called by background services)
    [HttpPost("aggregation/daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessDailyAggregation([FromQuery] DateTime date)
    {
        try
        {
            await _analyticsService.ProcessDailyAggregationAsync(date);
            return Ok(new { Message = $"Daily aggregation processed for {date:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing daily aggregation for date {Date}", date);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("aggregation/weekly")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessWeeklyAggregation([FromQuery] DateTime weekStart)
    {
        try
        {
            await _analyticsService.ProcessWeeklyAggregationAsync(weekStart);
            return Ok(new { Message = $"Weekly aggregation processed for week starting {weekStart:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weekly aggregation for week {WeekStart}", weekStart);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("aggregation/monthly")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessMonthlyAggregation([FromQuery] DateTime monthStart)
    {
        try
        {
            await _analyticsService.ProcessMonthlyAggregationAsync(monthStart);
            return Ok(new { Message = $"Monthly aggregation processed for month starting {monthStart:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing monthly aggregation for month {MonthStart}", monthStart);
            return StatusCode(500, "Internal server error");
        }
    }

    // Test endpoint for development
    [HttpGet("test")]
    public async Task<ActionResult> TestEndpoint()
    {
        try
        {
            var userId = GetCurrentUserId();
            var quickStats = await _analyticsService.GetQuickStatsAsync(userId);
            
            return Ok(new 
            { 
                Message = "Analytics Service is working",
                UserId = userId,
                QuickStats = quickStats,
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