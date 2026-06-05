using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

public class ArticleRepository : IArticleRepository
{
    private readonly AppDbContext _db;

    public ArticleRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<Article> Items, int TotalCount)> GetAllAsync(
        int     page,
        int     pageSize,
        bool?   publishedOnly = true,
        string? tag           = null,
        string? search        = null)
    {
        var query = _db.Articles
                       .Include(a => a.User)
                       .AsNoTracking()
                       .AsQueryable();

        // Filter by published status
        if (publishedOnly.HasValue)
            query = query.Where(a => a.IsPublished == publishedOnly.Value);

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

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.PublishedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Article?> GetByIdAsync(int id) =>
        await _db.Articles
                 .Include(a => a.User)
                 .AsNoTracking()
                 .FirstOrDefaultAsync(a => a.Id == id);

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
}
