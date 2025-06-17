using System.Linq.Expressions;

namespace MauiApp.Data.Repositories;

public interface ILocalRepository<T> where T : class
{
    // Basic CRUD operations
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate);
    
    // Pagination
    Task<IEnumerable<T>> GetPagedAsync(int skip, int take);
    Task<IEnumerable<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int skip, int take);
    
    // Count operations
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    
    // Modification operations
    Task<T> AddAsync(T entity);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities);
    Task<T> UpdateAsync(T entity);
    Task<IEnumerable<T>> UpdateRangeAsync(IEnumerable<T> entities);
    Task DeleteAsync(T entity);
    Task DeleteAsync(Guid id);
    Task DeleteRangeAsync(IEnumerable<T> entities);
    
    // Sync-specific operations
    Task<IEnumerable<T>> GetUnsyncedAsync();
    Task<IEnumerable<T>> GetChangedSinceAsync(DateTime timestamp);
    Task MarkAsSyncedAsync(Guid id);
    Task MarkAsChangedAsync(Guid id);
    
    // Save changes
    Task<int> SaveChangesAsync();
}