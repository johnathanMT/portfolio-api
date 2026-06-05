using Microsoft.EntityFrameworkCore;
using PortfolioApi.Models;

namespace PortfolioApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>    Users    { get; set; }
    public DbSet<Article> Articles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ──────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email)
                  .IsUnique();

            entity.HasIndex(u => u.Username)
                  .IsUnique();

            entity.Property(u => u.Role)
                  .HasDefaultValue("Guest");
        });

        // ── Articles ───────────────────────────────────────────
        modelBuilder.Entity<Article>(entity =>
        {
            // Store full article body as MySQL LONGTEXT
            entity.Property(a => a.Content)
                  .HasColumnType("longtext");

            entity.Property(a => a.IsPublished)
                  .HasDefaultValue(true);

            // One User → many Articles
            entity.HasOne(a => a.User)
                  .WithMany(u => u.Articles)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index for fast queries by publication date
            entity.HasIndex(a => a.PublishedDate);
            entity.HasIndex(a => a.IsPublished);
        });
    }
}
