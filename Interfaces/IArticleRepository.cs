using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

public interface IArticleRepository
{
    Task<(IEnumerable<Article> Items, int TotalCount)> GetAllAsync(
        int    page,
        int    pageSize,
        bool?  publishedOnly = true,
        string? tag          = null,
        string? search       = null,
        bool   isAdmin       = false,
        int?   viewerId      = null);

    Task<Article?> GetByIdAsync(int id);
    Task<Article>  CreateAsync(Article article);
    Task<Article>  UpdateAsync(Article article);
    Task           DeleteAsync(int id);
    Task<bool>     ExistsAsync(int id);

    // Gallery image management
    Task<ArticleImage?> GetImageWithArticleAsync(int imageId);
    Task                DeleteImageAsync(ArticleImage image);
    Task                ReorderImagesAsync(int articleId, List<int> orderedIds);
}
