using MauiApp.Data.Models;

namespace MauiApp.Data.Repositories;

public interface ILocalProjectRepository : ILocalRepository<LocalProject>
{
    Task<IEnumerable<LocalProject>> GetByOwnerIdAsync(Guid ownerId);
    Task<IEnumerable<LocalProject>> GetActiveProjectsAsync();
    Task<IEnumerable<LocalProject>> GetArchivedProjectsAsync();
    Task<IEnumerable<LocalProject>> GetProjectsByStatusAsync(string status);
    Task<IEnumerable<LocalProject>> SearchProjectsAsync(string searchTerm);
    Task<LocalProject?> GetByNameAsync(string name);
}