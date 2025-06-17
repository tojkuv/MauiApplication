using Microsoft.EntityFrameworkCore;
using MauiApp.Data.Models;

namespace MauiApp.Data.Repositories;

public class LocalTaskRepository : LocalRepository<LocalTask>, ILocalTaskRepository
{
    public LocalTaskRepository(LocalDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<LocalTask>> GetByProjectIdAsync(Guid projectId)
    {
        return await _dbSet.Where(t => t.ProjectId == projectId).OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetByAssignedToIdAsync(Guid userId)
    {
        return await _dbSet.Where(t => t.AssignedToId == userId).OrderBy(t => t.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetByStatusAsync(string status)
    {
        return await _dbSet.Where(t => t.Status == status).OrderBy(t => t.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetByPriorityAsync(string priority)
    {
        return await _dbSet.Where(t => t.Priority == priority).OrderBy(t => t.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetOverdueTasksAsync()
    {
        var today = DateTime.Today;
        return await _dbSet.Where(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date < today && 
            t.Status != "Completed")
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetTasksDueTodayAsync()
    {
        var today = DateTime.Today;
        return await _dbSet.Where(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date == today && 
            t.Status != "Completed")
            .OrderBy(t => t.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetTasksDueThisWeekAsync()
    {
        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);
        
        return await _dbSet.Where(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date >= startOfWeek && 
            t.DueDate.Value.Date < endOfWeek && 
            t.Status != "Completed")
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetCompletedTasksAsync(DateTime? fromDate = null)
    {
        var query = _dbSet.Where(t => t.Status == "Completed");
        
        if (fromDate.HasValue)
        {
            query = query.Where(t => t.CompletedDate.HasValue && t.CompletedDate.Value >= fromDate.Value);
        }
        
        return await query.OrderByDescending(t => t.CompletedDate).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetSubTasksAsync(Guid parentTaskId)
    {
        return await _dbSet.Where(t => t.ParentTaskId == parentTaskId).OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> SearchTasksAsync(string searchTerm)
    {
        return await _dbSet.Where(t => 
            t.Title.Contains(searchTerm) || 
            t.Description.Contains(searchTerm))
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LocalTask>> GetTasksByTagAsync(string tag)
    {
        return await _dbSet.Where(t => t.Tags.Contains(tag)).OrderBy(t => t.DueDate).ToListAsync();
    }
}