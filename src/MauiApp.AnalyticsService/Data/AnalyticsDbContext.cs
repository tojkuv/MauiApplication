using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.AnalyticsService.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options)
    {
    }

    // Read-only access to source data from other services
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> Tasks { get; set; }
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    // Analytics-specific tables for computed data and caching
    public DbSet<AnalyticsCache> AnalyticsCache { get; set; }
    public DbSet<DailyMetric> DailyMetrics { get; set; }
    public DbSet<WeeklyMetric> WeeklyMetrics { get; set; }
    public DbSet<MonthlyMetric> MonthlyMetrics { get; set; }
    public DbSet<UserActivityLog> UserActivityLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure analytics-specific entities
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

        modelBuilder.Entity<WeeklyMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);

            entity.HasIndex(e => new { e.WeekStart, e.MetricType, e.EntityType, e.EntityId }).IsUnique();
            entity.HasIndex(e => e.WeekStart);
        });

        modelBuilder.Entity<MonthlyMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);

            entity.HasIndex(e => new { e.MonthStart, e.MetricType, e.EntityType, e.EntityId }).IsUnique();
            entity.HasIndex(e => e.MonthStart);
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
        });

        // Configure read-only entities with proper table mappings
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.ToTable("Tasks");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("ProjectMembers");
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.ToTable("TaskComments");
            entity.Property(e => e.Content).HasMaxLength(1000);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.ToTable("TimeEntries");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ProjectFile>(entity =>
        {
            entity.ToTable("ProjectFiles");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.MessageType).HasMaxLength(50);
        });
    }
}

// Analytics-specific entities
public class AnalyticsCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CacheKey { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty; // JSON data
    public string DataType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

public class DailyMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; }
    public string MetricType { get; set; } = string.Empty; // "productivity", "completion", "time"
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty; // "user", "project", "global"
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; } // JSON for additional data
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WeeklyMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime WeekStart { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MonthlyMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime MonthStart { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty; // "task_created", "comment_added", etc.
    public string Description { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? AdditionalData { get; set; } // JSON for extra context
}