using Microsoft.EntityFrameworkCore;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.TasksService.Data;

namespace MauiApp.TasksService.Services;

public class TaskService : ITaskService
{
    private readonly TasksDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(TasksDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, Guid userId)
    {
        // Verify user has access to the project
        await VerifyProjectAccessAsync(request.ProjectId, userId);

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            ProjectId = request.ProjectId,
            AssigneeId = request.AssigneeId,
            Priority = request.Priority,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours,
            CreatedById = userId,
            Status = MauiApp.Core.Entities.TaskStatus.ToDo
        };

        _context.Tasks.Add(task);

        // Add dependencies if specified
        foreach (var dependsOnTaskId in request.DependsOnTaskIds)
        {
            var dependency = new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                DependsOnTaskId = dependsOnTaskId,
                Type = DependencyType.FinishToStart
            };
            _context.TaskDependencies.Add(dependency);
        }

        await _context.SaveChangesAsync();

        return await GetTaskByIdAsync(task.Id, userId) ?? throw new InvalidOperationException("Failed to retrieve created task");
    }

    public async Task<TaskDto?> GetTaskByIdAsync(Guid id, Guid userId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Author)
            .Include(t => t.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(t => t.TimeEntries).ThenInclude(te => te.User)
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOnTask)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            return null;
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        return MapToTaskDto(task);
    }

    public async Task<IEnumerable<TaskDto>> GetTasksAsync(Guid? projectId = null, Guid? userId = null, int page = 1, int pageSize = 20)
    {
        var query = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == projectId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == userId.Value || t.CreatedById == userId.Value);
        }

        var tasks = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return tasks.Select(MapToTaskDto);
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request, Guid userId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        task.Title = request.Title;
        task.Description = request.Description;
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.AssigneeId = request.AssigneeId;
        task.EstimatedHours = request.EstimatedHours;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTaskByIdAsync(id, userId) ?? throw new InvalidOperationException("Failed to retrieve updated task");
    }

    public async Task<TaskDto> UpdateTaskStatusAsync(Guid id, UpdateTaskStatusRequest request, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        task.Status = request.Status;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTaskByIdAsync(id, userId) ?? throw new InvalidOperationException("Failed to retrieve updated task");
    }

    public async Task<bool> DeleteTaskAsync(Guid id, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            return false;
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        // Only creator or project admin can delete tasks
        var canDelete = task.CreatedById == userId || await IsProjectAdminAsync(task.ProjectId, userId);
        if (!canDelete)
        {
            throw new UnauthorizedAccessException("User does not have permission to delete this task");
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<KanbanBoardDto> GetKanbanBoardAsync(Guid projectId, Guid userId)
    {
        // Verify user has access to the project
        await VerifyProjectAccessAsync(projectId, userId);

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        var tasks = await _context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        var columns = new List<KanbanColumnDto>();
        var statuses = Enum.GetValues<MauiApp.Core.Entities.TaskStatus>();

        foreach (var status in statuses)
        {
            var statusTasks = tasks.Where(t => t.Status == status).Select(MapToTaskDto).ToList();
            columns.Add(new KanbanColumnDto
            {
                Status = status,
                Title = status.ToString(),
                TaskCount = statusTasks.Count,
                Tasks = statusTasks
            });
        }

        return new KanbanBoardDto
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            Columns = columns
        };
    }

    public async Task<TaskDto> MoveTaskAsync(Guid taskId, MoveTaskRequest request, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        task.Status = request.NewStatus;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTaskByIdAsync(taskId, userId) ?? throw new InvalidOperationException("Failed to retrieve updated task");
    }

    public async Task<TaskCommentDto> CreateCommentAsync(Guid taskId, CreateTaskCommentRequest request, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Content = request.Content,
            AuthorId = userId,
            ParentCommentId = request.ParentCommentId
        };

        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync();

        // Return the created comment with author details
        var createdComment = await _context.TaskComments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == comment.Id);

        return MapToTaskCommentDto(createdComment!);
    }

    public async Task<TaskCommentDto> UpdateCommentAsync(Guid taskId, Guid commentId, UpdateTaskCommentRequest request, Guid userId)
    {
        var comment = await _context.TaskComments
            .Include(c => c.Task)
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);

        if (comment == null)
        {
            throw new InvalidOperationException("Comment not found");
        }

        // Only the author can edit their comment
        if (comment.AuthorId != userId)
        {
            throw new UnauthorizedAccessException("User can only edit their own comments");
        }

        comment.Content = request.Content;
        comment.UpdatedAt = DateTime.UtcNow;
        comment.IsEdited = true;

        await _context.SaveChangesAsync();

        return MapToTaskCommentDto(comment);
    }

    public async Task<bool> DeleteCommentAsync(Guid taskId, Guid commentId, Guid userId)
    {
        var comment = await _context.TaskComments
            .Include(c => c.Task)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);

        if (comment == null)
        {
            return false;
        }

        // Only the author or project admin can delete comments
        var canDelete = comment.AuthorId == userId || await IsProjectAdminAsync(comment.Task.ProjectId, userId);
        if (!canDelete)
        {
            throw new UnauthorizedAccessException("User does not have permission to delete this comment");
        }

        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<TaskCommentDto>> GetTaskCommentsAsync(Guid taskId, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var comments = await _context.TaskComments
            .Include(c => c.Author)
            .Include(c => c.Replies).ThenInclude(r => r.Author)
            .Where(c => c.TaskId == taskId && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(MapToTaskCommentDto);
    }

    public async Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, string fileName, string contentType, long fileSize, string blobUrl, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            FileName = fileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            BlobUrl = blobUrl,
            UploadedById = userId
        };

        _context.TaskAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        var createdAttachment = await _context.TaskAttachments
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == attachment.Id);

        return MapToTaskAttachmentDto(createdAttachment!);
    }

    public async Task<bool> RemoveAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId)
    {
        var attachment = await _context.TaskAttachments
            .Include(a => a.Task)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == taskId);

        if (attachment == null)
        {
            return false;
        }

        // Only the uploader or project admin can remove attachments
        var canRemove = attachment.UploadedById == userId || await IsProjectAdminAsync(attachment.Task.ProjectId, userId);
        if (!canRemove)
        {
            throw new UnauthorizedAccessException("User does not have permission to remove this attachment");
        }

        _context.TaskAttachments.Remove(attachment);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<TaskAttachmentDto>> GetTaskAttachmentsAsync(Guid taskId, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var attachments = await _context.TaskAttachments
            .Include(a => a.UploadedBy)
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        return attachments.Select(MapToTaskAttachmentDto);
    }

    public async Task<TimeEntryDto> CreateTimeEntryAsync(Guid taskId, CreateTimeEntryRequest request, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var timeEntry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            DurationMinutes = request.DurationMinutes,
            UserId = userId,
            IsBillable = request.IsBillable,
            HourlyRate = request.HourlyRate
        };

        _context.TimeEntries.Add(timeEntry);
        await _context.SaveChangesAsync();

        var createdTimeEntry = await _context.TimeEntries
            .Include(te => te.User)
            .FirstOrDefaultAsync(te => te.Id == timeEntry.Id);

        return MapToTimeEntryDto(createdTimeEntry!);
    }

    public async Task<IEnumerable<TimeEntryDto>> GetTaskTimeEntriesAsync(Guid taskId, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var timeEntries = await _context.TimeEntries
            .Include(te => te.User)
            .Where(te => te.TaskId == taskId)
            .OrderByDescending(te => te.StartTime)
            .ToListAsync();

        return timeEntries.Select(MapToTimeEntryDto);
    }

    public async Task<bool> DeleteTimeEntryAsync(Guid taskId, Guid timeEntryId, Guid userId)
    {
        var timeEntry = await _context.TimeEntries
            .Include(te => te.Task)
            .FirstOrDefaultAsync(te => te.Id == timeEntryId && te.TaskId == taskId);

        if (timeEntry == null)
        {
            return false;
        }

        // Only the time entry owner can delete it
        if (timeEntry.UserId != userId)
        {
            throw new UnauthorizedAccessException("User can only delete their own time entries");
        }

        _context.TimeEntries.Remove(timeEntry);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<TaskDependencyDto> CreateDependencyAsync(Guid taskId, CreateTaskDependencyRequest request, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        // Check if dependency already exists
        var existingDependency = await _context.TaskDependencies
            .FirstOrDefaultAsync(d => d.TaskId == taskId && d.DependsOnTaskId == request.DependsOnTaskId);

        if (existingDependency != null)
        {
            throw new InvalidOperationException("Dependency already exists");
        }

        var dependency = new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = request.DependsOnTaskId,
            Type = request.Type
        };

        _context.TaskDependencies.Add(dependency);
        await _context.SaveChangesAsync();

        var createdDependency = await _context.TaskDependencies
            .Include(d => d.DependsOnTask)
            .FirstOrDefaultAsync(d => d.Id == dependency.Id);

        return MapToTaskDependencyDto(createdDependency!);
    }

    public async Task<bool> RemoveDependencyAsync(Guid taskId, Guid dependencyId, Guid userId)
    {
        var dependency = await _context.TaskDependencies
            .Include(d => d.Task)
            .FirstOrDefaultAsync(d => d.Id == dependencyId && d.TaskId == taskId);

        if (dependency == null)
        {
            return false;
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(dependency.Task.ProjectId, userId);

        _context.TaskDependencies.Remove(dependency);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<TaskDependencyDto>> GetTaskDependenciesAsync(Guid taskId, Guid userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException("Task not found");
        }

        // Verify user has access to the project
        await VerifyProjectAccessAsync(task.ProjectId, userId);

        var dependencies = await _context.TaskDependencies
            .Include(d => d.DependsOnTask)
            .Where(d => d.TaskId == taskId)
            .ToListAsync();

        return dependencies.Select(MapToTaskDependencyDto);
    }

    public async Task<TaskStatsDto> GetUserTaskStatsAsync(Guid userId)
    {
        var userTasks = await _context.Tasks
            .Where(t => t.AssigneeId == userId || t.CreatedById == userId)
            .ToListAsync();

        return new TaskStatsDto
        {
            TotalTasks = userTasks.Count,
            TodoTasks = userTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.ToDo),
            InProgressTasks = userTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.InProgress),
            ReviewTasks = userTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Review),
            CompletedTasks = userTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done),
            OverdueTasks = userTasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.UtcNow && t.Status != MauiApp.Core.Entities.TaskStatus.Done),
            AssignedToUser = userTasks.Count(t => t.AssigneeId == userId),
            CreatedByUser = userTasks.Count(t => t.CreatedById == userId)
        };
    }

    public async Task<TaskStatsDto> GetProjectTaskStatsAsync(Guid projectId, Guid userId)
    {
        // Verify user has access to the project
        await VerifyProjectAccessAsync(projectId, userId);

        var projectTasks = await _context.Tasks
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        return new TaskStatsDto
        {
            TotalTasks = projectTasks.Count,
            TodoTasks = projectTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.ToDo),
            InProgressTasks = projectTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.InProgress),
            ReviewTasks = projectTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Review),
            CompletedTasks = projectTasks.Count(t => t.Status == MauiApp.Core.Entities.TaskStatus.Done),
            OverdueTasks = projectTasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.UtcNow && t.Status != MauiApp.Core.Entities.TaskStatus.Done),
            AssignedToUser = projectTasks.Count(t => t.AssigneeId == userId),
            CreatedByUser = projectTasks.Count(t => t.CreatedById == userId)
        };
    }

    public async Task<IEnumerable<TaskDto>> GetOverdueTasksAsync(Guid userId)
    {
        var overdueTasks = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Where(t => (t.AssigneeId == userId || t.CreatedById == userId) &&
                       t.DueDate.HasValue &&
                       t.DueDate < DateTime.UtcNow &&
                       t.Status != MauiApp.Core.Entities.TaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        return overdueTasks.Select(MapToTaskDto);
    }

    public async Task<IEnumerable<TaskDto>> GetUpcomingTasksAsync(Guid userId, int days = 7)
    {
        var upcomingDate = DateTime.UtcNow.AddDays(days);
        var upcomingTasks = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Where(t => (t.AssigneeId == userId || t.CreatedById == userId) &&
                       t.DueDate.HasValue &&
                       t.DueDate <= upcomingDate &&
                       t.DueDate > DateTime.UtcNow &&
                       t.Status != MauiApp.Core.Entities.TaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        return upcomingTasks.Select(MapToTaskDto);
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

    private static TaskDto MapToTaskDto(ProjectTask task)
    {
        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            ProjectId = task.ProjectId,
            ProjectName = task.Project?.Name ?? "Unknown",
            AssigneeId = task.AssigneeId,
            AssigneeName = task.Assignee?.FullName ?? "Unassigned",
            AssigneeEmail = task.Assignee?.Email ?? "",
            AssigneeAvatarUrl = task.Assignee?.AvatarUrl,
            CreatedById = task.CreatedById,
            CreatedByName = task.CreatedBy?.FullName ?? "Unknown",
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            EstimatedHours = task.EstimatedHours,
            ActualHours = task.ActualHours,
            CommentCount = task.Comments?.Count ?? 0,
            AttachmentCount = task.Attachments?.Count ?? 0,
            Comments = task.Comments?.Select(MapToTaskCommentDto).ToList() ?? new(),
            Attachments = task.Attachments?.Select(MapToTaskAttachmentDto).ToList() ?? new(),
            TimeEntries = task.TimeEntries?.Select(MapToTimeEntryDto).ToList() ?? new(),
            Dependencies = task.Dependencies?.Select(MapToTaskDependencyDto).ToList() ?? new()
        };
    }

    private static TaskCommentDto MapToTaskCommentDto(TaskComment comment)
    {
        return new TaskCommentDto
        {
            Id = comment.Id,
            Content = comment.Content,
            AuthorId = comment.AuthorId,
            AuthorName = comment.Author?.FullName ?? "Unknown",
            AuthorEmail = comment.Author?.Email ?? "",
            AuthorAvatarUrl = comment.Author?.AvatarUrl,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            IsEdited = comment.IsEdited,
            Replies = comment.Replies?.Select(MapToTaskCommentDto).ToList() ?? new()
        };
    }

    private static TaskAttachmentDto MapToTaskAttachmentDto(TaskAttachment attachment)
    {
        return new TaskAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            BlobUrl = attachment.BlobUrl,
            ThumbnailUrl = attachment.ThumbnailUrl,
            UploadedById = attachment.UploadedById,
            UploadedByName = attachment.UploadedBy?.FullName ?? "Unknown",
            CreatedAt = attachment.CreatedAt
        };
    }

    private static TimeEntryDto MapToTimeEntryDto(TimeEntry timeEntry)
    {
        return new TimeEntryDto
        {
            Id = timeEntry.Id,
            Description = timeEntry.Description,
            StartTime = timeEntry.StartTime,
            EndTime = timeEntry.EndTime,
            DurationMinutes = timeEntry.DurationMinutes,
            UserId = timeEntry.UserId,
            UserName = timeEntry.User?.FullName ?? "Unknown",
            CreatedAt = timeEntry.CreatedAt,
            IsBillable = timeEntry.IsBillable,
            HourlyRate = timeEntry.HourlyRate
        };
    }

    private static TaskDependencyDto MapToTaskDependencyDto(TaskDependency dependency)
    {
        return new TaskDependencyDto
        {
            Id = dependency.Id,
            TaskId = dependency.TaskId,
            DependsOnTaskId = dependency.DependsOnTaskId,
            DependsOnTaskTitle = dependency.DependsOnTask?.Title ?? "Unknown",
            DependsOnTaskStatus = dependency.DependsOnTask?.Status ?? MauiApp.Core.Entities.TaskStatus.ToDo,
            Type = dependency.Type,
            CreatedAt = dependency.CreatedAt
        };
    }
}