using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;
using MauiApp.Core.DTOs;
using System.Text.Json;

namespace MauiApp.SyncService.Data;

public class SyncDbContext : DbContext
{
    public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options)
    {
    }

    // Sync-specific tables
    public DbSet<SyncItem> SyncItems { get; set; }
    public DbSet<SyncClient> SyncClients { get; set; }
    public DbSet<SyncConflict> SyncConflicts { get; set; }
    public DbSet<SyncConfiguration> SyncConfigurations { get; set; }
    public DbSet<SyncLog> SyncLogs { get; set; }
    public DbSet<EntitySubscription> EntitySubscriptions { get; set; }

    // Read-only access to source data from other services
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> Tasks { get; set; }
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure sync-specific entities
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
            entity.HasIndex(e => new { e.UserId, e.Status });

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
            entity.HasIndex(e => new { e.UserId, e.IsActive });

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

        modelBuilder.Entity<SyncConflict>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ClientData).IsRequired();
            entity.Property(e => e.ServerData).IsRequired();
            entity.Property(e => e.ConflictReason).HasMaxLength(500);
            entity.Property(e => e.ResolutionData);

            entity.HasIndex(e => new { e.ClientId, e.IsResolved });
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Client)
                .WithMany(c => c.Conflicts)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ResolvedByUser)
                .WithMany()
                .HasForeignKey(e => e.ResolvedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SyncConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityConfigurations).IsRequired();

            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Operation).HasMaxLength(50);
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.LogLevel).HasMaxLength(20);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Details);

            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.LogLevel, e.Timestamp });

            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SyncItem)
                .WithMany()
                .HasForeignKey(e => e.SyncItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EntitySubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => new { e.ClientId, e.EntityType }).IsUnique();
            entity.HasIndex(e => new { e.EntityType, e.IsActive });

            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
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