using System.ComponentModel.DataAnnotations;
using MauiApp.Core.Entities;

namespace MauiApp.Core.DTOs;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MauiApp.Core.Entities.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public string AssigneeEmail { get; set; } = string.Empty;
    public string? AssigneeAvatarUrl { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
    public List<TaskCommentDto> Comments { get; set; } = new();
    public List<TaskAttachmentDto> Attachments { get; set; } = new();
    public List<TimeEntryDto> TimeEntries { get; set; } = new();
    public List<TaskDependencyDto> Dependencies { get; set; } = new();
}

public class CreateTaskRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid ProjectId { get; set; }

    public Guid? AssigneeId { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }

    public int EstimatedHours { get; set; } = 0;

    public List<Guid> DependsOnTaskIds { get; set; } = new();
}

public class UpdateTaskRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public MauiApp.Core.Entities.TaskStatus Status { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? AssigneeId { get; set; }

    public int EstimatedHours { get; set; }
}

public class UpdateTaskStatusRequest
{
    [Required]
    public MauiApp.Core.Entities.TaskStatus Status { get; set; }
}

public class TaskCommentDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public Guid? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
    public List<TaskCommentDto> Replies { get; set; } = new();
}

public class CreateTaskCommentRequest
{
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }
}

public class UpdateTaskCommentRequest
{
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
}

public class TaskAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TimeEntryDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsBillable { get; set; }
    public decimal? HourlyRate { get; set; }
}

public class CreateTimeEntryRequest
{
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsBillable { get; set; } = true;

    public decimal? HourlyRate { get; set; }
}

public class TaskDependencyDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public string DependsOnTaskTitle { get; set; } = string.Empty;
    public MauiApp.Core.Entities.TaskStatus DependsOnTaskStatus { get; set; }
    public DependencyType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTaskDependencyRequest
{
    [Required]
    public Guid DependsOnTaskId { get; set; }

    public DependencyType Type { get; set; } = DependencyType.FinishToStart;
}

public class TaskStatsDto
{
    public int TotalTasks { get; set; }
    public int TodoTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int ReviewTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int AssignedToUser { get; set; }
    public int CreatedByUser { get; set; }
}

public class KanbanBoardDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public List<KanbanColumnDto> Columns { get; set; } = new();
}

public class KanbanColumnDto
{
    public MauiApp.Core.Entities.TaskStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public List<TaskDto> Tasks { get; set; } = new();
}

public class MoveTaskRequest
{
    [Required]
    public MauiApp.Core.Entities.TaskStatus NewStatus { get; set; }

    public int? Position { get; set; }
}