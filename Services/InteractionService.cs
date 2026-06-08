using System.Security.Cryptography;
using System.Text;
using PortfolioApi.Common;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

public class InteractionService : IInteractionService
{
    private readonly IInteractionRepository _repo;
    private readonly string _salt;

    public InteractionService(IInteractionRepository repo, IConfiguration config)
    {
        _repo = repo;
        // Reuse a configured salt; fall back to the JWT key so hashes aren't guessable.
        _salt = config["Interactions:Salt"] ?? config["Jwt:Key"] ?? "mtn-interaction-salt";
    }

    // SHA-256 of (salt + visitor token). Stores no raw IP / token.
    private string Hash(string visitorKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_salt + "|" + visitorKey));
        return Convert.ToHexString(bytes); // 64 chars
    }

    public async Task<ApiResponse<object>> LikeAsync(int articleId, string visitorKey)
    {
        if (!await _repo.ArticleExistsAsync(articleId))
            return ApiResponse<object>.Fail($"Article {articleId} not found.", 404);

        await _repo.AddLikeAsync(articleId, Hash(visitorKey));
        var count = await _repo.CountLikesAsync(articleId);
        return ApiResponse<object>.Ok(new { likeCount = count, liked = true }, "Liked.");
    }

    public async Task<ApiResponse<object>> UnlikeAsync(int articleId, string visitorKey)
    {
        if (!await _repo.ArticleExistsAsync(articleId))
            return ApiResponse<object>.Fail($"Article {articleId} not found.", 404);

        await _repo.RemoveLikeAsync(articleId, Hash(visitorKey));
        var count = await _repo.CountLikesAsync(articleId);
        return ApiResponse<object>.Ok(new { likeCount = count, liked = false }, "Unliked.");
    }

    public async Task<ApiResponse<object>> ReactAsync(int articleId, string reaction, string visitorKey)
    {
        // THE anti-spam gate: only fixed, known reaction keys are ever accepted.
        var key = (reaction ?? string.Empty).Trim().ToLowerInvariant();
        if (!IInteractionService.AllowedReactions.Contains(key))
            return ApiResponse<object>.Fail(
                $"Invalid reaction. Allowed: {string.Join(", ", IInteractionService.AllowedReactions)}.", 400);

        if (!await _repo.ArticleExistsAsync(articleId))
            return ApiResponse<object>.Fail($"Article {articleId} not found.", 404);

        await _repo.AddReactionAsync(articleId, key, Hash(visitorKey));
        var counts = await _repo.GetReactionCountsAsync(articleId);
        var total = counts.Values.Sum();
        return ApiResponse<object>.Ok(new { reactions = counts, total }, "Reaction recorded.");
    }
}
