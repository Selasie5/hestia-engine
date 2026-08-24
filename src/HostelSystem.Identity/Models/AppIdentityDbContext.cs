using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HostelSystem.Identity.Models;

public class AppIdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(r => r.Id);

            // Unique token lookup (refresh flow)
            b.HasIndex(r => r.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token");

            // Per-user active token queries
            b.HasIndex(r => r.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");

            // Expiration cleanup / validation
            b.HasIndex(r => r.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

            // Composite for "active tokens for user" queries
            b.HasIndex(r => new { r.UserId, r.Token })
                .HasDatabaseName("IX_RefreshTokens_UserId_Token");

            b.Property(r => r.Token).IsRequired().HasMaxLength(512);
            b.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        });
    }
}
