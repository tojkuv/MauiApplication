using MauiApp.Data.Models;

namespace MauiApp.Data.Repositories;

public interface ILocalTaskRepository : ILocalRepository<LocalTask>
{
    Task<IEnumerable<LocalTask>> GetByProjectIdAsync(Guid projectId);
    Task<IEnumerable<LocalTask>> GetByAssignedToIdAsync(Guid userId);
    Task<IEnumerable<LocalTask>> GetByStatusAsync(string status);
    Task<IEnumerable<LocalTask>> GetByPriorityAsync(string priority);
    Task<IEnumerable<LocalTask>> GetOverdueTasksAsync();
    Task<IEnumerable<LocalTask>> GetTasksDueTodayAsync();
    Task<IEnumerable<LocalTask>> GetTasksDueThisWeekAsync();
    Task<IEnumerable<LocalTask>> GetCompletedTasksAsync(DateTime? fromDate = null);
    Task<IEnumerable<LocalTask>> GetSubTasksAsync(Guid parentTaskId);
    Task<IEnumerable<LocalTask>> SearchTasksAsync(string searchTerm);
    Task<IEnumerable<LocalTask>> GetTasksByTagAsync(string tag);
}