using Microsoft.EntityFrameworkCore;
using MauiApp.Data.Models;

namespace MauiApp.Data;

public class LocalDbContext : DbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<LocalUser> Users { get; set; }
    public DbSet<LocalProject> Projects { get; set; }
    public DbSet<LocalTask> Tasks { get; set; }
    public DbSet<LocalTimeEntry> TimeEntries { get; set; }
    public DbSet<LocalFile> Files { get; set; }
    public DbSet<LocalMessage> Messages { get; set; }
    public DbSet<LocalNotification> Notifications { get; set; }

    // Sync tracking
    public DbSet<LocalSyncStatus> SyncStatuses { get; set; }
    public DbSet<LocalChangeLog> ChangeLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure LocalUser
        modelBuilder.Entity<LocalUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure LocalProject
        modelBuilder.Entity<LocalProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.OwnerId);
            entity.HasIndex(e => e.Name);
        });

        // Configure LocalTask
        modelBuilder.Entity<LocalTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(50);
            entity.HasOne<LocalProject>().WithMany().HasForeignKey(e => e.ProjectId);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.AssignedToId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.DueDate);
        });

        // Configure LocalTimeEntry
        modelBuilder.Entity<LocalTimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne<LocalTask>().WithMany().HasForeignKey(e => e.TaskId);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.StartTime);
        });

        // Configure LocalFile
        modelBuilder.Entity<LocalFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.LocalPath).HasMaxLength(500);
            entity.HasOne<LocalProject>().WithMany().HasForeignKey(e => e.ProjectId);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.UploadedById);
        });

        // Configure LocalMessage
        modelBuilder.Entity<LocalMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.MessageType).HasMaxLength(50);
            entity.HasOne<LocalProject>().WithMany().HasForeignKey(e => e.ProjectId);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.SenderId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Configure LocalNotification
        modelBuilder.Entity<LocalNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.HasOne<LocalUser>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsRead);
        });

        // Configure LocalSyncStatus
        modelBuilder.Entity<LocalSyncStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.SyncStatus).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).IsUnique();
        });

        // Configure LocalChangeLog
        modelBuilder.Entity<LocalChangeLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Data).HasColumnType("TEXT");
            entity.HasIndex(e => e.Timestamp);
        });
    }
}