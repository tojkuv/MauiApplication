using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace MauiApp.Data.Repositories;

public class LocalRepository<T> : ILocalRepository<T> where T : class
{
    protected readonly LocalDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public LocalRepository(LocalDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public virtual async Task<IEnumerable<T>> GetPagedAsync(int skip, int take)
    {
        return await _dbSet.Skip(skip).Take(take).ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int skip, int take)
    {
        return await _dbSet.Where(predicate).Skip(skip).Take(take).ToListAsync();
    }

    public virtual async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        SetTimestamps(entity, isNew: true);
        SetSyncProperties(entity, hasChanges: true);
        
        var result = await _dbSet.AddAsync(entity);
        return result.Entity;
    }

    public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        var entityList = entities.ToList();
        
        foreach (var entity in entityList)
        {
            SetTimestamps(entity, isNew: true);
            SetSyncProperties(entity, hasChanges: true);
        }
        
        await _dbSet.AddRangeAsync(entityList);
        return entityList;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        SetTimestamps(entity, isNew: false);
        SetSyncProperties(entity, hasChanges: true);
        
        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    public virtual async Task<IEnumerable<T>> UpdateRangeAsync(IEnumerable<T> entities)
    {
        var entityList = entities.ToList();
        
        foreach (var entity in entityList)
        {
            SetTimestamps(entity, isNew: false);
            SetSyncProperties(entity, hasChanges: true);
        }
        
        _dbSet.UpdateRange(entityList);
        return await Task.FromResult(entityList);
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            await DeleteAsync(entity);
        }
    }

    public virtual async Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
        await Task.CompletedTask;
    }

    public virtual async Task<IEnumerable<T>> GetUnsyncedAsync()
    {
        var hasLocalChangesProperty = typeof(T).GetProperty("HasLocalChanges");
        if (hasLocalChangesProperty != null)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, hasLocalChangesProperty);
            var comparison = Expression.Equal(property, Expression.Constant(true));
            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);

            return await _dbSet.Where(lambda).ToListAsync();
        }
        
        return await GetAllAsync();
    }

    public virtual async Task<IEnumerable<T>> GetChangedSinceAsync(DateTime timestamp)
    {
        var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
        if (updatedAtProperty != null)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, updatedAtProperty);
            var comparison = Expression.GreaterThan(property, Expression.Constant(timestamp));
            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);

            return await _dbSet.Where(lambda).ToListAsync();
        }
        
        return await GetAllAsync();
    }

    public virtual async Task MarkAsSyncedAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            SetSyncProperties(entity, hasChanges: false, isSynced: true);
            await UpdateAsync(entity);
        }
    }

    public virtual async Task MarkAsChangedAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            SetSyncProperties(entity, hasChanges: true, isSynced: false);
            await UpdateAsync(entity);
        }
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    private void SetTimestamps(T entity, bool isNew)
    {
        var now = DateTime.UtcNow;
        
        if (isNew)
        {
            var createdAtProperty = typeof(T).GetProperty("CreatedAt");
            createdAtProperty?.SetValue(entity, now);
        }
        
        var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
        updatedAtProperty?.SetValue(entity, now);
    }

    private void SetSyncProperties(T entity, bool hasChanges, bool? isSynced = null)
    {
        var hasLocalChangesProperty = typeof(T).GetProperty("HasLocalChanges");
        hasLocalChangesProperty?.SetValue(entity, hasChanges);
        
        if (isSynced.HasValue)
        {
            var isSyncedProperty = typeof(T).GetProperty("IsSynced");
            isSyncedProperty?.SetValue(entity, isSynced.Value);
            
            if (isSynced.Value)
            {
                var lastSyncedAtProperty = typeof(T).GetProperty("LastSyncedAt");
                lastSyncedAtProperty?.SetValue(entity, DateTime.UtcNow);
            }
        }
    }
}