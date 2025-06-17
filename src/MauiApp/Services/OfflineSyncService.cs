using Microsoft.Extensions.Logging;
using MauiApp.Data;
using MauiApp.Data.Repositories;
using MauiApp.Data.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MauiApp.Services;

public class OfflineSyncService : IOfflineSyncService
{
    private readonly LocalDbContext _localDbContext;
    private readonly IApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILogger<OfflineSyncService> _logger;
    private readonly Timer? _backgroundSyncTimer;
    
    private readonly ILocalProjectRepository _projectRepository;
    private readonly ILocalTaskRepository _taskRepository;
    
    private bool _isSyncing = false;
    private bool _isBackgroundSyncEnabled = false;

    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    public OfflineSyncService(
        LocalDbContext localDbContext,
        IApiService apiService,
        ISecureStorageService secureStorageService,
        ILocalProjectRepository projectRepository,
        ILocalTaskRepository taskRepository,
        ILogger<OfflineSyncService> logger)
    {
        _localDbContext = localDbContext;
        _apiService = apiService;
        _secureStorageService = secureStorageService;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            var connectivity = Connectivity.Current;
            if (connectivity.NetworkAccess != NetworkAccess.Internet)
                return false;

            // Ping the API to verify connectivity
            var response = await _apiService.GetAsync<object>("health");
            return response.IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        try
        {
            var lastSyncString = await _secureStorageService.GetAsync("LastSyncTime");
            if (DateTime.TryParse(lastSyncString, out var lastSync))
                return lastSync;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last sync time");
        }
        
        return null;
    }

    public async Task<int> GetPendingChangesCountAsync()
    {
        try
        {
            var projects = await _projectRepository.GetUnsyncedAsync();
            var tasks = await _taskRepository.GetUnsyncedAsync();
            
            return projects.Count() + tasks.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending changes count");
            return 0;
        }
    }

    public async Task<SyncResult> SyncAllAsync()
    {
        if (_isSyncing)
        {
            return new SyncResult { IsSuccess = false, ErrorMessage = "Sync already in progress" };
        }

        _isSyncing = true;
        var stopwatch = Stopwatch.StartNew();
        var result = new SyncResult { SyncedAt = DateTime.UtcNow };

        try
        {
            _logger.LogInformation("Starting full sync");
            ReportProgress("Sync", 0, 100, "Starting sync...");

            if (!await IsOnlineAsync())
            {
                result.ErrorMessage = "No internet connection";
                return result;
            }

            // Push local changes first
            ReportProgress("Sync", 10, 100, "Pushing local changes...");
            var pushResult = await PushLocalChangesAsync();
            result.TotalItems += pushResult.TotalItems;
            result.SyncedItems += pushResult.SyncedItems;
            result.FailedItems += pushResult.FailedItems;
            result.ConflictItems += pushResult.ConflictItems;

            // Then pull server changes
            ReportProgress("Sync", 50, 100, "Pulling server changes...");
            var pullResult = await PullServerChangesAsync();
            result.TotalItems += pullResult.TotalItems;
            result.SyncedItems += pullResult.SyncedItems;
            result.FailedItems += pullResult.FailedItems;
            result.ConflictItems += pullResult.ConflictItems;

            // Save last sync time
            await _secureStorageService.SetAsync("LastSyncTime", DateTime.UtcNow.ToString());

            result.IsSuccess = result.FailedItems == 0;
            ReportProgress("Sync", 100, 100, "Sync completed");

            _logger.LogInformation($"Sync completed: {result.SyncedItems}/{result.TotalItems} items synced");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync");
            result.ErrorMessage = ex.Message;
            result.IsSuccess = false;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            _isSyncing = false;
            
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = result });
        }

        return result;
    }

    public async Task<SyncResult> SyncProjectsAsync()
    {
        return await SyncEntityTypeAsync<LocalProject>("projects", _projectRepository);
    }

    public async Task<SyncResult> SyncTasksAsync()
    {
        return await SyncEntityTypeAsync<LocalTask>("tasks", _taskRepository);
    }

    public async Task<SyncResult> SyncFilesAsync()
    {
        // TODO: Implement file sync with blob storage
        return new SyncResult { IsSuccess = true };
    }

    public async Task<SyncResult> SyncMessagesAsync()
    {
        // TODO: Implement message sync
        return new SyncResult { IsSuccess = true };
    }

    public async Task<SyncResult> SyncNotificationsAsync()
    {
        // TODO: Implement notification sync
        return new SyncResult { IsSuccess = true };
    }

    public async Task<SyncResult> PushLocalChangesAsync()
    {
        var result = new SyncResult { SyncedAt = DateTime.UtcNow };
        
        try
        {
            _logger.LogInformation("Pushing local changes to server");

            // Push projects
            var projectResult = await PushEntityChangesAsync<LocalProject>("projects", _projectRepository);
            result.TotalItems += projectResult.TotalItems;
            result.SyncedItems += projectResult.SyncedItems;
            result.FailedItems += projectResult.FailedItems;

            // Push tasks
            var taskResult = await PushEntityChangesAsync<LocalTask>("tasks", _taskRepository);
            result.TotalItems += taskResult.TotalItems;
            result.SyncedItems += taskResult.SyncedItems;
            result.FailedItems += taskResult.FailedItems;

            result.IsSuccess = result.FailedItems == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing local changes");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<SyncResult> PullServerChangesAsync()
    {
        var result = new SyncResult { SyncedAt = DateTime.UtcNow };
        
        try
        {
            _logger.LogInformation("Pulling server changes to local");

            var lastSync = await GetLastSyncTimeAsync() ?? DateTime.MinValue;

            // Pull projects
            var projectResult = await PullEntityChangesAsync<LocalProject>("projects", _projectRepository, lastSync);
            result.TotalItems += projectResult.TotalItems;
            result.SyncedItems += projectResult.SyncedItems;
            result.FailedItems += projectResult.FailedItems;

            // Pull tasks
            var taskResult = await PullEntityChangesAsync<LocalTask>("tasks", _taskRepository, lastSync);
            result.TotalItems += taskResult.TotalItems;
            result.SyncedItems += taskResult.SyncedItems;
            result.FailedItems += taskResult.FailedItems;

            result.IsSuccess = result.FailedItems == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling server changes");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<SyncResult> ResolveConflictsAsync()
    {
        // TODO: Implement conflict resolution
        return new SyncResult { IsSuccess = true };
    }

    public async Task<IEnumerable<SyncConflict>> GetConflictsAsync()
    {
        // TODO: Implement conflict detection
        return new List<SyncConflict>();
    }

    public async Task StartBackgroundSyncAsync()
    {
        _isBackgroundSyncEnabled = true;
        // TODO: Implement background sync timer
        _logger.LogInformation("Background sync started");
    }

    public async Task StopBackgroundSyncAsync()
    {
        _isBackgroundSyncEnabled = false;
        // TODO: Stop background sync timer
        _logger.LogInformation("Background sync stopped");
    }

    private async Task<SyncResult> SyncEntityTypeAsync<T>(string endpoint, ILocalRepository<T> repository) where T : class
    {
        var result = new SyncResult { SyncedAt = DateTime.UtcNow };
        
        try
        {
            // Push local changes
            var pushResult = await PushEntityChangesAsync(endpoint, repository);
            result.TotalItems += pushResult.TotalItems;
            result.SyncedItems += pushResult.SyncedItems;
            result.FailedItems += pushResult.FailedItems;

            // Pull server changes
            var lastSync = await GetLastSyncTimeAsync() ?? DateTime.MinValue;
            var pullResult = await PullEntityChangesAsync(endpoint, repository, lastSync);
            result.TotalItems += pullResult.TotalItems;
            result.SyncedItems += pullResult.SyncedItems;
            result.FailedItems += pullResult.FailedItems;

            result.IsSuccess = result.FailedItems == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error syncing {typeof(T).Name}");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<SyncResult> PushEntityChangesAsync<T>(string endpoint, ILocalRepository<T> repository) where T : class
    {
        var result = new SyncResult();
        
        try
        {
            var unsyncedEntities = await repository.GetUnsyncedAsync();
            result.TotalItems = unsyncedEntities.Count();

            foreach (var entity in unsyncedEntities)
            {
                try
                {
                    // TODO: Determine if this is create, update, or delete
                    // For now, assume it's an update
                    var apiResult = await _apiService.PutAsync(endpoint, entity);
                    
                    if (apiResult.IsSuccess)
                    {
                        // Mark as synced
                        var idProperty = typeof(T).GetProperty("Id");
                        if (idProperty != null)
                        {
                            var id = (Guid)idProperty.GetValue(entity)!;
                            await repository.MarkAsSyncedAsync(id);
                        }
                        
                        result.SyncedItems++;
                    }
                    else
                    {
                        result.FailedItems++;
                        _logger.LogWarning($"Failed to sync {typeof(T).Name}: {apiResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedItems++;
                    _logger.LogError(ex, $"Error syncing individual {typeof(T).Name}");
                }
            }

            await repository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error pushing {typeof(T).Name} changes");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<SyncResult> PullEntityChangesAsync<T>(string endpoint, ILocalRepository<T> repository, DateTime lastSync) where T : class
    {
        var result = new SyncResult();
        
        try
        {
            // TODO: Call API to get changes since lastSync
            var apiResult = await _apiService.GetAsync<IEnumerable<T>>($"{endpoint}?since={lastSync:yyyy-MM-ddTHH:mm:ssZ}");
            
            if (apiResult.IsSuccess && apiResult.Data != null)
            {
                var serverEntities = apiResult.Data;
                result.TotalItems = serverEntities.Count();

                foreach (var serverEntity in serverEntities)
                {
                    try
                    {
                        // TODO: Check for local conflicts and merge
                        await repository.UpdateAsync(serverEntity);
                        result.SyncedItems++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedItems++;
                        _logger.LogError(ex, $"Error updating local {typeof(T).Name}");
                    }
                }

                await repository.SaveChangesAsync();
            }
            else
            {
                result.ErrorMessage = apiResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error pulling {typeof(T).Name} changes");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private void ReportProgress(string entityType, int current, int total, string status)
    {
        SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            EntityType = entityType,
            Current = current,
            Total = total,
            Status = status
        });
    }
}