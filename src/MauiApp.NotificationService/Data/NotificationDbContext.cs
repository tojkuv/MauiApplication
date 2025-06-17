using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;
using MauiApp.Core.DTOs;

namespace MauiApp.NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    // Notification-specific tables
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationDelivery> NotificationDeliveries { get; set; }
    public DbSet<DeviceToken> DeviceTokens { get; set; }
    public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public DbSet<NotificationQueue> NotificationQueue { get; set; }
    public DbSet<NotificationStats> NotificationStats { get; set; }
    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }

    // Read-only access to source data from other services
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> Tasks { get; set; }
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure notification entities
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
            entity.HasIndex(e => e.ScheduledAt);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ExternalId).HasMaxLength(100);

            entity.HasIndex(e => new { e.NotificationId, e.Channel });
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => e.NextRetry);
            entity.HasIndex(e => new { e.Channel, e.Status });

            entity.HasOne(e => e.Notification)
                .WithMany(n => n.Deliveries)
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(e => e.LastUsedAt);

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

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TitleTemplate).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageTemplate).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ActionUrlTemplate).HasMaxLength(500);
            entity.Property(e => e.DefaultDataJson).IsRequired();

            entity.HasIndex(e => new { e.Name, e.Type }).IsUnique();
            entity.HasIndex(e => new { e.Type, e.IsActive });

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationQueue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(e => new { e.IsProcessed, e.Priority });
            entity.HasIndex(e => e.NextRetry);
            entity.HasIndex(e => new { e.Channel, e.IsProcessed });

            entity.HasOne(e => e.Notification)
                .WithMany()
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationStats>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeliveryRate).HasColumnType("decimal(5,4)");
            entity.Property(e => e.ReadRate).HasColumnType("decimal(5,4)");

            entity.HasIndex(e => new { e.Date, e.Type });
            entity.HasIndex(e => new { e.Date, e.Platform });
            entity.HasIndex(e => new { e.Date, e.Channel });
        });

        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => new { e.UserId, e.EntityType, e.EntityId });
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.IsActive });

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the SubscribedTypes as JSON
            entity.Property(e => e.SubscribedTypes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<NotificationType>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<NotificationType>());
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.NotificationId, e.Action });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });

            entity.HasOne(e => e.Notification)
                .WithMany()
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}