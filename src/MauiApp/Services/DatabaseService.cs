using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MauiApp.Data;
using MauiApp.Data.Repositories;

namespace MauiApp.Services;

public class DatabaseService : IDatabaseService
{
    private readonly LocalDbContext _context;
    private readonly ILocalProjectRepository _projectRepository;
    private readonly ILocalTaskRepository _taskRepository;
    private readonly IOfflineSyncService _syncService;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        LocalDbContext context,
        ILocalProjectRepository projectRepository,
        ILocalTaskRepository taskRepository,
        IOfflineSyncService syncService,
        ILogger<DatabaseService> logger)
    {
        _context = context;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing local database...");

            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Check if we need to run migrations
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
                await _context.Database.MigrateAsync();
            }

            _logger.LogInformation("Database initialized successfully");

            // Initialize with sample data if empty
            await SeedSampleDataIfEmptyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    public async Task<bool> IsDatabaseCreatedAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetDatabasePathAsync()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "app.db");
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        try
        {
            var dbPath = await GetDatabasePathAsync();
            if (File.Exists(dbPath))
            {
                var fileInfo = new FileInfo(dbPath);
                return fileInfo.Length;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database size");
        }
        
        return 0;
    }

    public async Task ClearAllDataAsync()
    {
        try
        {
            _logger.LogWarning("Clearing all local data...");

            // Delete all data but keep schema
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ChangeLogs");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SyncStatuses");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Notifications");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Messages");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Files");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM TimeEntries");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Tasks");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Projects");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users");

            _logger.LogInformation("All local data cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing database");
            throw;
        }
    }

    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        var info = new DatabaseInfo();
        
        try
        {
            info.Path = await GetDatabasePathAsync();
            info.SizeInBytes = await GetDatabaseSizeAsync();
            
            if (File.Exists(info.Path))
            {
                var fileInfo = new FileInfo(info.Path);
                info.CreatedAt = fileInfo.CreationTime;
                info.LastModified = fileInfo.LastWriteTime;
            }

            // Get counts
            info.ProjectCount = await _projectRepository.CountAsync();
            info.TaskCount = await _taskRepository.CountAsync();
            info.PendingChanges = await _syncService.GetPendingChangesCountAsync();

            // TODO: Add counts for other entities
            info.FileCount = 0;
            info.MessageCount = 0;
            info.NotificationCount = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database info");
        }

        return info;
    }

    private async Task SeedSampleDataIfEmptyAsync()
    {
        try
        {
            // Check if we already have data
            var projectCount = await _projectRepository.CountAsync();
            if (projectCount > 0)
                return;

            _logger.LogInformation("Seeding sample data...");

            // Create sample user (current user)
            var sampleUser = new MauiApp.Data.Models.LocalUser
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe",
                Role = "Project Manager",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsSynced = false,
                HasLocalChanges = true
            };

            await _context.Users.AddAsync(sampleUser);

            // Create sample projects
            var projects = new[]
            {
                new MauiApp.Data.Models.LocalProject
                {
                    Id = Guid.NewGuid(),
                    Name = "Mobile App Development",
                    Description = "Cross-platform mobile application using .NET MAUI",
                    OwnerId = sampleUser.Id,
                    Status = "Active",
                    StartDate = DateTime.Today.AddDays(-30),
                    DueDate = DateTime.Today.AddDays(60),
                    Budget = 50000,
                    Color = "#2196F3",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false,
                    HasLocalChanges = true
                },
                new MauiApp.Data.Models.LocalProject
                {
                    Id = Guid.NewGuid(),
                    Name = "Web Portal",
                    Description = "Customer management web portal",
                    OwnerId = sampleUser.Id,
                    Status = "Active",
                    StartDate = DateTime.Today.AddDays(-20),
                    DueDate = DateTime.Today.AddDays(40),
                    Budget = 30000,
                    Color = "#4CAF50",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false,
                    HasLocalChanges = true
                }
            };

            await _context.Projects.AddRangeAsync(projects);

            // Create sample tasks
            var tasks = new[]
            {
                new MauiApp.Data.Models.LocalTask
                {
                    Id = Guid.NewGuid(),
                    Title = "Setup project structure",
                    Description = "Initialize the project with proper folder structure and dependencies",
                    ProjectId = projects[0].Id,
                    AssignedToId = sampleUser.Id,
                    Status = "Completed",
                    Priority = "High",
                    DueDate = DateTime.Today.AddDays(-5),
                    CompletedDate = DateTime.Today.AddDays(-3),
                    ProgressPercentage = 100,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false,
                    HasLocalChanges = true
                },
                new MauiApp.Data.Models.LocalTask
                {
                    Id = Guid.NewGuid(),
                    Title = "Implement authentication",
                    Description = "Add OAuth2 authentication with Azure AD B2C",
                    ProjectId = projects[0].Id,
                    AssignedToId = sampleUser.Id,
                    Status = "In Progress",
                    Priority = "High",
                    DueDate = DateTime.Today.AddDays(5),
                    ProgressPercentage = 75,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false,
                    HasLocalChanges = true
                },
                new MauiApp.Data.Models.LocalTask
                {
                    Id = Guid.NewGuid(),
                    Title = "Design user interface",
                    Description = "Create wireframes and design system for the mobile app",
                    ProjectId = projects[0].Id,
                    AssignedToId = sampleUser.Id,
                    Status = "To Do",
                    Priority = "Medium",
                    DueDate = DateTime.Today.AddDays(10),
                    ProgressPercentage = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false,
                    HasLocalChanges = true
                }
            };

            await _context.Tasks.AddRangeAsync(tasks);

            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Sample data seeded: {projects.Length} projects, {tasks.Length} tasks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding sample data");
        }
    }
}