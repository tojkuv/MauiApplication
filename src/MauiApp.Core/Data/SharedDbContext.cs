using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;
using MauiApp.Core.DTOs;
using System.Text.Json;

namespace MauiApp.Core.Data;

/// <summary>
/// Shared database context that all microservices inherit from.
/// Provides access to all entities while allowing services to focus on their domain.
/// </summary>
public class SharedDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public SharedDbContext(DbContextOptions options) : base(options)
    {
    }

    // ========================================================
    // CORE ENTITIES (All services can access)
    // ========================================================
    
    // Users are inherited from IdentityDbContext
    public new DbSet<ApplicationUser> Users { get; set; }
    
    // Projects
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<Milestone> Milestones { get; set; }
    
    // Tasks
    public DbSet<ProjectTask> Tasks { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<TaskAttachment> TaskAttachments { get; set; }
    public DbSet<TaskDependency> TaskDependencies { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    
    // Files
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<FileVersion> FileVersions { get; set; }
    public DbSet<Entities.FileShare> FileShares { get; set; }
    public DbSet<FileComment> FileComments { get; set; }
    
    // Collaboration
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<MessageReaction> MessageReactions { get; set; }
    
    // Notifications
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationDelivery> NotificationDeliveries { get; set; }
    public DbSet<DeviceToken> DeviceTokens { get; set; }
    public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public DbSet<NotificationQueue> NotificationQueue { get; set; }
    public DbSet<NotificationStats> NotificationStats { get; set; }
    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }
    
    // Sync
    public DbSet<SyncItem> SyncItems { get; set; }
    public DbSet<SyncClient> SyncClients { get; set; }
    public DbSet<SyncConflict> SyncConflicts { get; set; }
    public DbSet<SyncConfiguration> SyncConfigurations { get; set; }
    public DbSet<SyncLog> SyncLogs { get; set; }
    public DbSet<EntitySubscription> EntitySubscriptions { get; set; }
    
    // Analytics
    public DbSet<AnalyticsCache> AnalyticsCache { get; set; }
    public DbSet<DailyMetric> DailyMetrics { get; set; }
    public DbSet<WeeklyMetric> WeeklyMetrics { get; set; }
    public DbSet<MonthlyMetric> MonthlyMetrics { get; set; }
    public DbSet<UserActivityLog> UserActivityLogs { get; set; }
    
    // Identity
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    
    // Audit
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========================================================
        // IDENTITY CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========================================================
        // PROJECT CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(500);
            
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartDate);
            
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.DueDate);
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Milestones)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========================================================
        // TASK CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.AssigneeId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.CreatedById);
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Assignee)
                .WithMany()
                .HasForeignKey(e => e.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
            
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.AuthorId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(e => e.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Author)
                .WithMany()
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
            
            entity.HasIndex(e => e.TaskId);
            
            entity.HasOne(e => e.Task)
                .WithMany(t => t.Attachments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TaskId, e.DependsOnTaskId }).IsUnique();
            
            entity.HasOne(e => e.Task)
                .WithMany(t => t.Dependencies)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.DependsOnTask)
                .WithMany(t => t.DependentTasks)
                .HasForeignKey(e => e.DependsOnTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartTime);
            
            entity.HasOne(e => e.Task)
                .WithMany(t => t.TimeEntries)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========================================================
        // FILE CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<ProjectFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UploadedById);
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Files)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========================================================
        // COLLABORATION CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.MessageType).HasMaxLength(50);
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.AuthorId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Author)
                .WithMany()
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ReplyToMessage)
                .WithMany()
                .HasForeignKey(e => e.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ========================================================
        // NOTIFICATION CONFIGURATION
        // ========================================================
        
        ConfigureNotificationEntities(modelBuilder);
        
        // ========================================================
        // SYNC CONFIGURATION
        // ========================================================
        
        ConfigureSyncEntities(modelBuilder);
        
        // ========================================================
        // ANALYTICS CONFIGURATION
        // ========================================================
        
        ConfigureAnalyticsEntities(modelBuilder);
        
        // ========================================================
        // AUDIT CONFIGURATION
        // ========================================================
        
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TableName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(10);
            
            entity.HasIndex(e => new { e.TableName, e.RecordId });
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ========================================================
        // BUSINESS CONSTRAINTS
        // ========================================================
        
        // Project end date must be after start date
        modelBuilder.Entity<Project>()
            .ToTable(t => t.HasCheckConstraint("CK_Projects_EndDateAfterStart", 
                "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));
        
        // Time entries must have positive duration
        modelBuilder.Entity<TimeEntry>()
            .ToTable(t => t.HasCheckConstraint("CK_TimeEntries_ValidDuration", 
                "[DurationMinutes] >= 0"));
        
        // File sizes must be positive
        modelBuilder.Entity<ProjectFile>()
            .ToTable(t => t.HasCheckConstraint("CK_ProjectFiles_PositiveFileSize", 
                "[FileSize] > 0"));
    }

    private void ConfigureNotificationEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.DataJson).IsRequired();
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.ReadAt });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Type, e.Status });

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.AppVersion).HasMaxLength(50);
            entity.Property(e => e.DeviceModel).HasMaxLength(100);
            entity.Property(e => e.OSVersion).HasMaxLength(50);

            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => new { e.Token, e.Platform }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationPreferences>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TypePreferencesJson).IsRequired();
            entity.Property(e => e.QuietHoursDaysJson).IsRequired();

            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureSyncEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(e => new { e.ClientId, e.Status });
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.Timestamp);

            entity.HasOne(e => e.Client)
                .WithMany(c => c.SyncItems)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SyncClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientInfo).HasMaxLength(500);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastSeenAt);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure JSON property for EntityLastSyncTimestamps
            entity.Property(e => e.EntityLastSyncTimestamps)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, DateTime>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, DateTime>());
        });
    }

    private void ConfigureAnalyticsEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalyticsCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CacheKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.DataType).HasMaxLength(100);

            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<DailyMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);

            entity.HasIndex(e => new { e.Date, e.MetricType, e.EntityType, e.EntityId }).IsUnique();
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EntityType).HasMaxLength(100);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.UserId, e.ActivityType });

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Override SaveChanges to add audit logging
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await AddAuditLogs();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task AddAuditLogs()
    {
        var auditEntries = new List<AuditLog>();
        
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditLog = new AuditLog
            {
                TableName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                // UserId would be set from current user context in real implementation
                UserId = null
            };

            if (entry.Entity is IHasId hasId)
            {
                auditLog.RecordId = hasId.Id;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditLog.NewValues = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                    break;
                case EntityState.Modified:
                    auditLog.OldValues = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                    auditLog.NewValues = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                    break;
                case EntityState.Deleted:
                    auditLog.OldValues = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                    break;
            }

            auditEntries.Add(auditLog);
        }

        if (auditEntries.Any())
        {
            AuditLogs.AddRange(auditEntries);
        }
    }
}

/// <summary>
/// Interface for entities that have an Id property for audit logging
/// </summary>
public interface IHasId
{
    Guid Id { get; set; }
}

/// <summary>
/// Audit log entity for tracking all database changes
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
}