using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MauiApp.Core.DTOs;
using MauiApp.Core.Interfaces;
using System.Security.Claims;

namespace MauiApp.TasksService.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Get tasks with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks(
        [FromQuery] Guid? projectId = null, 
        [FromQuery] Guid? userId = null, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // If no specific userId is provided, use current user for filtering
            var filterUserId = userId ?? currentUserId;
            
            var tasks = await _taskService.GetTasksAsync(projectId, filterUserId, page, pageSize);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks");
            return StatusCode(500, new { message = "An error occurred while getting tasks" });
        }
    }

    /// <summary>
    /// Get task by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> GetTask(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.GetTaskByIdAsync(id, userId);
            
            if (task == null)
            {
                return NotFound(new { message = "Task not found" });
            }
            
            return Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while getting the task" });
        }
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.CreateTaskAsync(request, userId);
            
            _logger.LogInformation("Task created: {TaskId} by user: {UserId}", task.Id, userId);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
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
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, new { message = "An error occurred while creating the task" });
        }
    }

    /// <summary>
    /// Update task
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TaskDto>> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.UpdateTaskAsync(id, request, userId);
            
            _logger.LogInformation("Task updated: {TaskId} by user: {UserId}", id, userId);
            return Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the task" });
        }
    }

    /// <summary>
    /// Update task status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<TaskDto>> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.UpdateTaskStatusAsync(id, request, userId);
            
            _logger.LogInformation("Task status updated: {TaskId} to {Status} by user: {UserId}", id, request.Status, userId);
            return Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task status {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the task status" });
        }
    }

    /// <summary>
    /// Delete task
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTask(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _taskService.DeleteTaskAsync(id, userId);
            
            if (!result)
            {
                return NotFound(new { message = "Task not found" });
            }
            
            _logger.LogInformation("Task deleted: {TaskId} by user: {UserId}", id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the task" });
        }
    }

    /// <summary>
    /// Move task (for Kanban board)
    /// </summary>
    [HttpPatch("{id}/move")]
    public async Task<ActionResult<TaskDto>> MoveTask(Guid id, [FromBody] MoveTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.MoveTaskAsync(id, request, userId);
            
            _logger.LogInformation("Task moved: {TaskId} to {Status} by user: {UserId}", id, request.NewStatus, userId);
            return Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while moving the task" });
        }
    }

    /// <summary>
    /// Get Kanban board for project
    /// </summary>
    [HttpGet("kanban/{projectId}")]
    public async Task<ActionResult<KanbanBoardDto>> GetKanbanBoard(Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var board = await _taskService.GetKanbanBoardAsync(projectId, userId);
            return Ok(board);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Kanban board for project {ProjectId}", projectId);
            return StatusCode(500, new { message = "An error occurred while getting the Kanban board" });
        }
    }

    /// <summary>
    /// Get task comments
    /// </summary>
    [HttpGet("{id}/comments")]
    public async Task<ActionResult<IEnumerable<TaskCommentDto>>> GetTaskComments(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var comments = await _taskService.GetTaskCommentsAsync(id, userId);
            return Ok(comments);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while getting task comments" });
        }
    }

    /// <summary>
    /// Create task comment
    /// </summary>
    [HttpPost("{id}/comments")]
    public async Task<ActionResult<TaskCommentDto>> CreateComment(Guid id, [FromBody] CreateTaskCommentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var comment = await _taskService.CreateCommentAsync(id, request, userId);
            
            _logger.LogInformation("Comment created for task: {TaskId} by user: {UserId}", id, userId);
            return CreatedAtAction(nameof(GetTaskComments), new { id }, comment);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while creating the comment" });
        }
    }

    /// <summary>
    /// Update task comment
    /// </summary>
    [HttpPut("{id}/comments/{commentId}")]
    public async Task<ActionResult<TaskCommentDto>> UpdateComment(Guid id, Guid commentId, [FromBody] UpdateTaskCommentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var comment = await _taskService.UpdateCommentAsync(id, commentId, request, userId);
            
            _logger.LogInformation("Comment updated: {CommentId} for task: {TaskId} by user: {UserId}", commentId, id, userId);
            return Ok(comment);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment {CommentId} for task {TaskId}", commentId, id);
            return StatusCode(500, new { message = "An error occurred while updating the comment" });
        }
    }

    /// <summary>
    /// Delete task comment
    /// </summary>
    [HttpDelete("{id}/comments/{commentId}")]
    public async Task<ActionResult> DeleteComment(Guid id, Guid commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _taskService.DeleteCommentAsync(id, commentId, userId);
            
            if (!result)
            {
                return NotFound(new { message = "Comment not found" });
            }
            
            _logger.LogInformation("Comment deleted: {CommentId} from task: {TaskId} by user: {UserId}", commentId, id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId} from task {TaskId}", commentId, id);
            return StatusCode(500, new { message = "An error occurred while deleting the comment" });
        }
    }

    /// <summary>
    /// Get task time entries
    /// </summary>
    [HttpGet("{id}/time-entries")]
    public async Task<ActionResult<IEnumerable<TimeEntryDto>>> GetTaskTimeEntries(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var timeEntries = await _taskService.GetTaskTimeEntriesAsync(id, userId);
            return Ok(timeEntries);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time entries for task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while getting time entries" });
        }
    }

    /// <summary>
    /// Create task time entry
    /// </summary>
    [HttpPost("{id}/time-entries")]
    public async Task<ActionResult<TimeEntryDto>> CreateTimeEntry(Guid id, [FromBody] CreateTimeEntryRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var timeEntry = await _taskService.CreateTimeEntryAsync(id, request, userId);
            
            _logger.LogInformation("Time entry created for task: {TaskId} by user: {UserId}", id, userId);
            return CreatedAtAction(nameof(GetTaskTimeEntries), new { id }, timeEntry);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating time entry for task {TaskId}", id);
            return StatusCode(500, new { message = "An error occurred while creating the time entry" });
        }
    }

    /// <summary>
    /// Get user task statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<TaskStatsDto>> GetUserTaskStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _taskService.GetUserTaskStatsAsync(userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task stats for user");
            return StatusCode(500, new { message = "An error occurred while getting task statistics" });
        }
    }

    /// <summary>
    /// Get project task statistics
    /// </summary>
    [HttpGet("stats/project/{projectId}")]
    public async Task<ActionResult<TaskStatsDto>> GetProjectTaskStats(Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _taskService.GetProjectTaskStatsAsync(projectId, userId);
            return Ok(stats);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task stats for project {ProjectId}", projectId);
            return StatusCode(500, new { message = "An error occurred while getting project task statistics" });
        }
    }

    /// <summary>
    /// Get overdue tasks for current user
    /// </summary>
    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetOverdueTasks()
    {
        try
        {
            var userId = GetCurrentUserId();
            var tasks = await _taskService.GetOverdueTasksAsync(userId);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue tasks for user");
            return StatusCode(500, new { message = "An error occurred while getting overdue tasks" });
        }
    }

    /// <summary>
    /// Get upcoming tasks for current user
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetUpcomingTasks([FromQuery] int days = 7)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tasks = await _taskService.GetUpcomingTasksAsync(userId, days);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming tasks for user");
            return StatusCode(500, new { message = "An error occurred while getting upcoming tasks" });
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