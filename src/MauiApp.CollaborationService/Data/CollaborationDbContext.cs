using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.CollaborationService.Data;

public class CollaborationDbContext : DbContext
{
    public CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : base(options) { }

    // Chat entities
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<MessageReaction> MessageReactions { get; set; }
    public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }
    public DbSet<UserPresence> UserPresences { get; set; }
    public DbSet<UserConnection> UserConnections { get; set; }

    // Read-only entities from other services
    public DbSet<Project> Projects { get; set; }
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ChatMessage entity
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.MessageType).HasMaxLength(50);
            entity.Property(e => e.SentAt).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.SentAt);
            entity.HasIndex(e => new { e.ProjectId, e.SentAt });

            // Self-referencing relationship for replies
            entity.HasOne(e => e.ReplyToMessage)
                .WithMany()
                .HasForeignKey(e => e.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with sender
            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Navigation properties
            entity.HasMany(e => e.Reactions)
                .WithOne(r => r.Message)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ReadStatuses)
                .WithOne(rs => rs.Message)
                .HasForeignKey(rs => rs.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure MessageReaction entity
        modelBuilder.Entity<MessageReaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reaction).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).IsRequired();

            // Indexes
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.MessageId, e.UserId, e.Reaction }).IsUnique();

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure MessageReadStatus entity
        modelBuilder.Entity<MessageReadStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReadAt).IsRequired();

            // Indexes
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UserPresence entity
        modelBuilder.Entity<UserPresence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Activity).HasMaxLength(200);
            entity.Property(e => e.LastSeenAt).IsRequired();

            // Indexes
            entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.LastSeenAt);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserConnection entity
        modelBuilder.Entity<UserConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.ConnectedAt).IsRequired();
            entity.Property(e => e.LastSeenAt).IsRequired();

            // Indexes
            entity.HasIndex(e => e.ConnectionId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastSeenAt);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure read-only entities from other services
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("ProjectMembers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();

            // Relationships
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed data for testing (optional)
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // This method can be used to seed initial data for testing
        // Currently empty, but can be extended as needed
    }
}