using PortfolioApi.Common;

namespace PortfolioApi.Interfaces;

public interface IInteractionService
{
    /// <summary>The fixed, server-validated set of allowed quick reactions.</summary>
    static readonly IReadOnlySet<string> AllowedReactions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "love", "clap", "fire", "idea", "great", "inspiring", "helpful" };

    Task<ApiResponse<object>> LikeAsync(int articleId, string visitorKey);
    Task<ApiResponse<object>> UnlikeAsync(int articleId, string visitorKey);
    Task<ApiResponse<object>> ReactAsync(int articleId, string reaction, string visitorKey);
}
