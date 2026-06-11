using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

public class ArticleRepository : IArticleRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<ArticleRepository> _logger;

    public ArticleRepository(AppDbContext db, ILogger<ArticleRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // Core columns that exist in EVERY version of the schema (pre-dating the
    // multi-image / video migrations). Used as a graceful fallback so a read can
    // never 500 just because the ArticleImages table or Video* columns are not
    // yet present on the deployed database.
    private static readonly System.Linq.Expressions.Expression<Func<Article, Article>> CoreProjection =
        a => new Article
        {
            Id            = a.Id,
            Title         = a.Title,
            Content       = a.Content,
            Author        = a.Author,
            ImageUrl      = a.ImageUrl,
            ImagePublicId = a.ImagePublicId,
            Tags          = a.Tags,
            IsPublished   = a.IsPublished,
            PublishedDate = a.PublishedDate,
            CreatedAt     = a.CreatedAt,
            UpdatedAt     = a.UpdatedAt,
            UserId        = a.UserId,
        };

    public async Task<(IEnumerable<Article> Items, int TotalCount)> GetAllAsync(
        int     page,
        int     pageSize,
        bool?   publishedOnly = true,
        string? tag           = null,
        string? search        = null,
        bool    isAdmin       = false,
        int?    viewerId      = null)
    {
        // Build the filtered base query (no Includes yet) so we can reuse it for
        // both the rich query and the degraded fallback.
        IQueryable<Article> Filtered()
        {
            var query = _db.Articles.AsNoTracking().AsQueryable();

            // Visibility rules:
            //  - Admin: may filter by published state explicitly (true / false / null = all).
            //  - Author/logged-in: see all published articles PLUS their own drafts.
            //  - Anonymous / Guest: published articles only.
            if (isAdmin)
            {
                if (publishedOnly.HasValue)
                    query = query.Where(a => a.IsPublished == publishedOnly.Value);
            }
            else if (viewerId.HasValue)
            {
                query = query.Where(a => a.IsPublished || a.UserId == viewerId.Value);
            }
            else
            {
                query = query.Where(a => a.IsPublished);
            }

            // Filter by tag (simple contains check; extend with a tags table if needed)
            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(a => a.Tags != null && a.Tags.Contains(tag));

            // Full-text search on title and content
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(a =>
                    a.Title.ToLower().Contains(term) ||
                    a.Content.ToLower().Contains(term) ||
                    a.Author.ToLower().Contains(term));
            }

            return query;
        }

        // COUNT(*) touches no optional columns, so it is always safe.
        var total = await Filtered().CountAsync();

        try
        {
            // Preferred path: full data including author + gallery images.
            var items = await Filtered()
                .Include(a => a.User)
                .Include(a => a.Images)
                .OrderByDescending(a => a.PublishedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
        catch (Exception ex)
        {
            // Schema likely predates the multi-image/video migrations. Degrade to
            // core columns so the blog still renders (no gallery/video/author).
            _logger.LogWarning(ex,
                "Rich article query failed — falling back to core columns. " +
                "Apply the AddArticleImagesTable + AddArticleMediaSupport migrations to restore galleries/video.");

            var items = await Filtered()
                .OrderByDescending(a => a.PublishedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(CoreProjection)
                .ToListAsync();

            return (items, total);
        }
    }

    public async Task<Article?> GetByIdAsync(int id)
    {
        try
        {
            return await _db.Articles
                            .Include(a => a.User)
                            .Include(a => a.Images)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Rich single-article query failed for {Id} — falling back to core columns.", id);

            return await _db.Articles
                            .AsNoTracking()
                            .Where(a => a.Id == id)
                            .Select(CoreProjection)
                            .FirstOrDefaultAsync();
        }
    }

    public async Task<Article> CreateAsync(Article article)
    {
        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
        return article;
    }

    public async Task<Article> UpdateAsync(Article article)
    {
        article.UpdatedAt = DateTime.UtcNow;
        _db.Articles.Update(article);
        await _db.SaveChangesAsync();
        return article;
    }

    public async Task DeleteAsync(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article is not null)
        {
            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id) =>
        await _db.Articles.AnyAsync(a => a.Id == id);

    // ── Gallery image management ─────────────────────────────
    public Task<ArticleImage?> GetImageWithArticleAsync(int imageId) =>
        _db.ArticleImages.Include(i => i.Article).FirstOrDefaultAsync(i => i.Id == imageId);

    public async Task DeleteImageAsync(ArticleImage image)
    {
        _db.ArticleImages.Remove(image);
        await _db.SaveChangesAsync();
    }

    public async Task ReorderImagesAsync(int articleId, List<int> orderedIds)
    {
        var images = await _db.ArticleImages.Where(i => i.ArticleId == articleId).ToListAsync();
        for (var idx = 0; idx < orderedIds.Count; idx++)
        {
            var img = images.FirstOrDefault(i => i.Id == orderedIds[idx]);
            if (img is not null) img.SortOrder = idx;
        }
        await _db.SaveChangesAsync();
    }
}
