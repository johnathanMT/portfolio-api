using Microsoft.EntityFrameworkCore;
using PortfolioApi.Models;

namespace PortfolioApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>            Users            { get; set; }
    public DbSet<Article>         Articles         { get; set; }
    public DbSet<ArticleImage>    ArticleImages    { get; set; }
    public DbSet<ArticleLike>     ArticleLikes     { get; set; }
    public DbSet<ArticleReaction> ArticleReactions { get; set; }
    public DbSet<MemoryTag>       MemoryTags       { get; set; }
    public DbSet<FarewellRsvp>    FarewellRsvps    { get; set; }
    public DbSet<Poem>            Poems            { get; set; }
    public DbSet<RemedyRequest>   RemedyRequests   { get; set; }
    public DbSet<QuerentChart>    QuerentCharts    { get; set; }
    public DbSet<PdfRequest>      PdfRequests      { get; set; }
    public DbSet<Customer>        Customers        { get; set; }
    public DbSet<CustomerChart>   CustomerCharts   { get; set; }
    public DbSet<AiReading>       AiReadings       { get; set; }
    public DbSet<ResearchPrediction>   ResearchPredictions   { get; set; }
    public DbSet<ResearchJournalEntry> ResearchJournalEntries { get; set; }
    public DbSet<ReadingRequest>       ReadingRequests       { get; set; }
    public DbSet<ConsultationMessage>  ConsultationMessages  { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Sanctuary memory tags ──────────────────────────────
        modelBuilder.Entity<MemoryTag>(entity =>
        {
            entity.HasIndex(m => m.OperatorToken);            // fast ownership lookups
            entity.HasIndex(m => m.CreatedAt);
            entity.Property(m => m.Landmark).HasDefaultValue("tree");
        });

        // ── Farewell RSVPs / living monuments ──────────────────
        modelBuilder.Entity<FarewellRsvp>(entity =>
        {
            entity.HasIndex(f => f.OperatorToken);            // one monument per person + ownership
            entity.HasIndex(f => f.CreatedAt);
            entity.Property(f => f.PlantType).HasDefaultValue("sakura");
        });

        // ── Poems (techno-science flip-book) ───────────────────
        modelBuilder.Entity<Poem>(entity =>
        {
            entity.Property(p => p.Content).HasColumnType("longtext");  // multi-line bodies
            entity.HasIndex(p => p.CreatedDate);
        });

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

            // One Article → many ArticleImages
            entity.HasMany(a => a.Images)
                  .WithOne(i => i.Article)
                  .HasForeignKey(i => i.ArticleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleImage>(entity =>
        {
            entity.HasIndex(i => i.ArticleId);
        });

        // ── Anonymous interactions ─────────────────────────────
        modelBuilder.Entity<ArticleLike>(entity =>
        {
            // One like per visitor per article (enables toggle/unlike)
            entity.HasIndex(l => new { l.ArticleId, l.VisitorHash }).IsUnique();
            entity.HasOne(l => l.Article)
                  .WithMany()
                  .HasForeignKey(l => l.ArticleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleReaction>(entity =>
        {
            // One of each reaction type per visitor per article
            entity.HasIndex(r => new { r.ArticleId, r.VisitorHash, r.Reaction }).IsUnique();
            entity.HasIndex(r => r.ArticleId);
            entity.HasOne(r => r.Article)
                  .WithMany()
                  .HasForeignKey(r => r.ArticleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
