using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface ITaskService
{
    // Task CRUD operations
    Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, Guid userId);
    Task<TaskDto?> GetTaskByIdAsync(Guid id, Guid userId);
    Task<IEnumerable<TaskDto>> GetTasksAsync(Guid? projectId = null, Guid? userId = null, int page = 1, int pageSize = 20);
    Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request, Guid userId);
    Task<TaskDto> UpdateTaskStatusAsync(Guid id, UpdateTaskStatusRequest request, Guid userId);
    Task<bool> DeleteTaskAsync(Guid id, Guid userId);

    // Kanban board operations
    Task<KanbanBoardDto> GetKanbanBoardAsync(Guid projectId, Guid userId);
    Task<TaskDto> MoveTaskAsync(Guid taskId, MoveTaskRequest request, Guid userId);

    // Task comments
    Task<TaskCommentDto> CreateCommentAsync(Guid taskId, CreateTaskCommentRequest request, Guid userId);
    Task<TaskCommentDto> UpdateCommentAsync(Guid taskId, Guid commentId, UpdateTaskCommentRequest request, Guid userId);
    Task<bool> DeleteCommentAsync(Guid taskId, Guid commentId, Guid userId);
    Task<IEnumerable<TaskCommentDto>> GetTaskCommentsAsync(Guid taskId, Guid userId);

    // Task attachments
    Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, string fileName, string contentType, long fileSize, string blobUrl, Guid userId);
    Task<bool> RemoveAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId);
    Task<IEnumerable<TaskAttachmentDto>> GetTaskAttachmentsAsync(Guid taskId, Guid userId);

    // Time tracking
    Task<TimeEntryDto> CreateTimeEntryAsync(Guid taskId, CreateTimeEntryRequest request, Guid userId);
    Task<IEnumerable<TimeEntryDto>> GetTaskTimeEntriesAsync(Guid taskId, Guid userId);
    Task<bool> DeleteTimeEntryAsync(Guid taskId, Guid timeEntryId, Guid userId);

    // Task dependencies
    Task<TaskDependencyDto> CreateDependencyAsync(Guid taskId, CreateTaskDependencyRequest request, Guid userId);
    Task<bool> RemoveDependencyAsync(Guid taskId, Guid dependencyId, Guid userId);
    Task<IEnumerable<TaskDependencyDto>> GetTaskDependenciesAsync(Guid taskId, Guid userId);

    // Statistics and reporting
    Task<TaskStatsDto> GetUserTaskStatsAsync(Guid userId);
    Task<TaskStatsDto> GetProjectTaskStatsAsync(Guid projectId, Guid userId);
    Task<IEnumerable<TaskDto>> GetOverdueTasksAsync(Guid userId);
    Task<IEnumerable<TaskDto>> GetUpcomingTasksAsync(Guid userId, int days = 7);
}