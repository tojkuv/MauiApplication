using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.IsActive).IsRequired();
        });

        // Seed data
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Name = "Admin User",
                Email = "admin@example.com",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        );
    }
}