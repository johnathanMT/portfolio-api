namespace PortfolioApi.Interfaces;

public interface IInteractionRepository
{
    Task<bool> ArticleExistsAsync(int articleId);

    // Likes
    Task<bool> AddLikeAsync(int articleId, string visitorHash);     // true if newly added
    Task<bool> RemoveLikeAsync(int articleId, string visitorHash);  // true if removed
    Task<int>  CountLikesAsync(int articleId);
    Task<bool> HasLikedAsync(int articleId, string visitorHash);

    // Reactions
    Task<bool> AddReactionAsync(int articleId, string reaction, string visitorHash);
    Task<Dictionary<string, int>> GetReactionCountsAsync(int articleId);

    // Batch (for enriching article lists)
    Task<Dictionary<int, int>> GetLikeCountsAsync(IEnumerable<int> articleIds);
    Task<Dictionary<int, Dictionary<string, int>>> GetReactionCountsAsync(IEnumerable<int> articleIds);
}
