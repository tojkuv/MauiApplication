using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.TasksService.Data;

public class TasksDbContext : DbContext
{
    public TasksDbContext(DbContextOptions<TasksDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectTask> Tasks { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<TaskAttachment> TaskAttachments { get; set; }
    public DbSet<TaskDependency> TaskDependencies { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<Project> Projects { get; set; } // Read-only access
    public DbSet<ApplicationUser> Users { get; set; } // Read-only access
    public DbSet<ProjectMember> ProjectMembers { get; set; } // Read-only access

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ProjectTask entity
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Assignee)
                .WithMany()
                .HasForeignKey(e => e.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.Task)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TimeEntries)
                .WithOne(t => t.Task)
                .HasForeignKey(t => t.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Dependencies)
                .WithOne(d => d.Task)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.DependentTasks)
                .WithOne(d => d.DependsOnTask)
                .HasForeignKey(d => d.DependsOnTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.AssigneeId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DueDate);
        });

        // Configure TaskComment entity
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);

            entity.HasOne(e => e.Author)
                .WithMany()
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure TaskAttachment entity
        modelBuilder.Entity<TaskAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);

            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TaskId);
        });

        // Configure TaskDependency entity
        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.TaskId, e.DependsOnTaskId }).IsUnique();
        });

        // Configure TimeEntry entity
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartTime);
        });

        // Configure read-only entities
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(500);
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
    }
}