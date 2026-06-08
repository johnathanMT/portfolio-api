using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

public class InteractionRepository : IInteractionRepository
{
    private readonly AppDbContext _db;
    public InteractionRepository(AppDbContext db) => _db = db;

    public Task<bool> ArticleExistsAsync(int articleId) =>
        _db.Articles.AnyAsync(a => a.Id == articleId);

    // ── Likes ────────────────────────────────────────────────
    public async Task<bool> AddLikeAsync(int articleId, string visitorHash)
    {
        var exists = await _db.ArticleLikes
            .AnyAsync(l => l.ArticleId == articleId && l.VisitorHash == visitorHash);
        if (exists) return false;

        _db.ArticleLikes.Add(new ArticleLike { ArticleId = articleId, VisitorHash = visitorHash });
        try { await _db.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { return false; } // unique index race -> treat as already liked
    }

    public async Task<bool> RemoveLikeAsync(int articleId, string visitorHash)
    {
        var like = await _db.ArticleLikes
            .FirstOrDefaultAsync(l => l.ArticleId == articleId && l.VisitorHash == visitorHash);
        if (like is null) return false;
        _db.ArticleLikes.Remove(like);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<int> CountLikesAsync(int articleId) =>
        _db.ArticleLikes.CountAsync(l => l.ArticleId == articleId);

    public Task<bool> HasLikedAsync(int articleId, string visitorHash) =>
        _db.ArticleLikes.AnyAsync(l => l.ArticleId == articleId && l.VisitorHash == visitorHash);

    // ── Reactions ────────────────────────────────────────────
    public async Task<bool> AddReactionAsync(int articleId, string reaction, string visitorHash)
    {
        var exists = await _db.ArticleReactions.AnyAsync(r =>
            r.ArticleId == articleId && r.VisitorHash == visitorHash && r.Reaction == reaction);
        if (exists) return false;

        _db.ArticleReactions.Add(new ArticleReaction
        {
            ArticleId = articleId, Reaction = reaction, VisitorHash = visitorHash,
        });
        try { await _db.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { return false; }
    }

    public async Task<Dictionary<string, int>> GetReactionCountsAsync(int articleId)
    {
        var rows = await _db.ArticleReactions
            .Where(r => r.ArticleId == articleId)
            .GroupBy(r => r.Reaction)
            .Select(g => new { Reaction = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.Reaction, x => x.Count);
    }

    // ── Batch enrichment ─────────────────────────────────────
    public async Task<Dictionary<int, int>> GetLikeCountsAsync(IEnumerable<int> articleIds)
    {
        var ids = articleIds.ToList();
        if (ids.Count == 0) return new();
        var rows = await _db.ArticleLikes
            .Where(l => ids.Contains(l.ArticleId))
            .GroupBy(l => l.ArticleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.Key, x => x.Count);
    }

    public async Task<Dictionary<int, Dictionary<string, int>>> GetReactionCountsAsync(IEnumerable<int> articleIds)
    {
        var ids = articleIds.ToList();
        if (ids.Count == 0) return new();
        var rows = await _db.ArticleReactions
            .Where(r => ids.Contains(r.ArticleId))
            .GroupBy(r => new { r.ArticleId, r.Reaction })
            .Select(g => new { g.Key.ArticleId, g.Key.Reaction, Count = g.Count() })
            .ToListAsync();

        return rows
            .GroupBy(x => x.ArticleId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Reaction, x => x.Count));
    }
}
