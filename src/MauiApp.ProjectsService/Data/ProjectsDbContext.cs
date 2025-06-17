using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.ProjectsService.Data;

public class ProjectsDbContext : DbContext
{
    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<Milestone> Milestones { get; set; }
    public DbSet<ApplicationUser> Users { get; set; } // Read-only access to users

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(500);

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Members)
                .WithOne(pm => pm.Project)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Milestones)
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ProjectMember entity
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
        });

        // Configure Milestone entity
        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Configure ApplicationUser (read-only)
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        });
    }
}