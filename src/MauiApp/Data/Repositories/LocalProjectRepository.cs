using Microsoft.EntityFrameworkCore;
using MauiApp.Data.Models;

namespace MauiApp.Data.Repositories;

public class LocalProjectRepository : LocalRepository<LocalProject>, ILocalProjectRepository
{
    public LocalProjectRepository(LocalDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<LocalProject>> GetByOwnerIdAsync(Guid ownerId)
    {
        return await _dbSet.Where(p => p.OwnerId == ownerId).ToListAsync();
    }

    public async Task<IEnumerable<LocalProject>> GetActiveProjectsAsync()
    {
        return await _dbSet.Where(p => !p.IsArchived).ToListAsync();
    }

    public async Task<IEnumerable<LocalProject>> GetArchivedProjectsAsync()
    {
        return await _dbSet.Where(p => p.IsArchived).ToListAsync();
    }

    public async Task<IEnumerable<LocalProject>> GetProjectsByStatusAsync(string status)
    {
        return await _dbSet.Where(p => p.Status == status).ToListAsync();
    }

    public async Task<IEnumerable<LocalProject>> SearchProjectsAsync(string searchTerm)
    {
        return await _dbSet.Where(p => 
            p.Name.Contains(searchTerm) || 
            p.Description.Contains(searchTerm))
            .ToListAsync();
    }

    public async Task<LocalProject?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Name == name);
    }
}