using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MauiApp.Core.Entities;

namespace MauiApp.IdentityService.Data;

public class IdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize table names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        });

        builder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        // Configure RefreshToken
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.Property(e => e.Token).HasMaxLength(500);
            entity.Property(e => e.JwtId).HasMaxLength(100);
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.JwtId);
            entity.HasIndex(e => e.UserId);
        });

        // Seed default roles
        SeedRoles(builder);
    }

    private static void SeedRoles(ModelBuilder builder)
    {
        var roles = new[]
        {
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "ProjectManager", NormalizedName = "PROJECTMANAGER" },
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "TeamLead", NormalizedName = "TEAMLEAD" },
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Developer", NormalizedName = "DEVELOPER" },
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" }
        };

        builder.Entity<IdentityRole<Guid>>().HasData(roles);
    }
}