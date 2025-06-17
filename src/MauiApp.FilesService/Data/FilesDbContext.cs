using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.FilesService.Data;

public class FilesDbContext : DbContext
{
    public FilesDbContext(DbContextOptions<FilesDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<FileVersion> FileVersions { get; set; }
    public DbSet<MauiApp.Core.Entities.FileShare> FileShares { get; set; }
    public DbSet<FileComment> FileComments { get; set; }
    
    // Read-only access to shared entities
    public DbSet<Project> Projects { get; set; }
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ProjectFile entity
        modelBuilder.Entity<ProjectFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.FileCategory).HasMaxLength(50);
            entity.Property(e => e.FileType).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FolderPath).HasMaxLength(500);
            entity.Property(e => e.StorageContainer).HasMaxLength(100);
            entity.Property(e => e.StoragePath).HasMaxLength(500);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Versions)
                .WithOne(v => v.File)
                .HasForeignKey(v => v.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Shares)
                .WithOne(s => s.File)
                .HasForeignKey(s => s.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Comments)
                .WithOne(c => c.File)
                .HasForeignKey(c => c.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UploadedById);
            entity.HasIndex(e => e.FileCategory);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.FolderPath);
        });

        // Configure FileVersion entity
        modelBuilder.Entity<FileVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VersionNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ChangeDescription).HasMaxLength(500);

            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure FileShare entity
        modelBuilder.Entity<MauiApp.Core.Entities.FileShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShareType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ShareToken).HasMaxLength(100);
            entity.Property(e => e.SharedWithEmail).HasMaxLength(255);

            entity.HasOne(e => e.SharedBy)
                .WithMany()
                .HasForeignKey(e => e.SharedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SharedWithUser)
                .WithMany()
                .HasForeignKey(e => e.SharedWithUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.ShareToken).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Configure FileComment entity
        modelBuilder.Entity<FileComment>(entity =>
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

            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.CreatedAt);
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