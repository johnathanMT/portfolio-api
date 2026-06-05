using PortfolioApi.Common;
using PortfolioApi.DTOs.Article;

namespace PortfolioApi.Interfaces;

public interface IArticleService
{
    Task<ApiResponse<PagedResult<ArticleResponseDto>>> GetAllAsync(
        int    page        = 1,
        int    pageSize    = 10,
        bool?  published   = true,
        string? tag        = null,
        string? search     = null);

    Task<ApiResponse<ArticleResponseDto>> GetByIdAsync(int id);

    Task<ApiResponse<ArticleResponseDto>> CreateAsync(CreateArticleDto dto, int userId);

    Task<ApiResponse<ArticleResponseDto>> UpdateAsync(int id, UpdateArticleDto dto, int userId);

    Task<ApiResponse<object>> DeleteAsync(int id, int userId);
}
