using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.ProjectsService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Get user's projects
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var projects = await _projectService.GetUserProjectsAsync(userId, page, pageSize);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user projects");
            return StatusCode(500, new { message = "An error occurred while getting projects" });
        }
    }

    /// <summary>
    /// Get project by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.GetProjectByIdAsync(id, userId);
            
            if (project == null)
            {
                return NotFound(new { message = "Project not found" });
            }
            
            return Ok(project);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectId}", id);
            return StatusCode(500, new { message = "An error occurred while getting the project" });
        }
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.CreateProjectAsync(request, userId);
            
            _logger.LogInformation("Project created: {ProjectId} by user: {UserId}", project.Id, userId);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return StatusCode(500, new { message = "An error occurred while creating the project" });
        }
    }

    /// <summary>
    /// Update project
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.UpdateProjectAsync(id, request, userId);
            
            _logger.LogInformation("Project updated: {ProjectId} by user: {UserId}", id, userId);
            return Ok(project);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the project" });
        }
    }

    /// <summary>
    /// Delete project
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _projectService.DeleteProjectAsync(id, userId);
            
            if (!result)
            {
                return NotFound(new { message = "Project not found" });
            }
            
            _logger.LogInformation("Project deleted: {ProjectId} by user: {UserId}", id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the project" });
        }
    }

    /// <summary>
    /// Get project members
    /// </summary>
    [HttpGet("{id}/members")]
    public async Task<ActionResult<IEnumerable<ProjectMemberDto>>> GetProjectMembers(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var members = await _projectService.GetProjectMembersAsync(id, userId);
            return Ok(members);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project members for {ProjectId}", id);
            return StatusCode(500, new { message = "An error occurred while getting project members" });
        }
    }

    /// <summary>
    /// Add project member
    /// </summary>
    [HttpPost("{id}/members")]
    public async Task<ActionResult<ProjectDto>> AddMember(Guid id, [FromBody] AddProjectMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.AddMemberAsync(id, request, userId);
            
            _logger.LogInformation("Member added to project: {ProjectId} by user: {UserId}", id, userId);
            return Ok(project);
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
            _logger.LogError(ex, "Error adding member to project {ProjectId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the member" });
        }
    }

    /// <summary>
    /// Get user project statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ProjectStatsDto>> GetProjectStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _projectService.GetUserProjectStatsAsync(userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project stats for user");
            return StatusCode(500, new { message = "An error occurred while getting project statistics" });
        }
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