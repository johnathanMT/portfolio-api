using PortfolioApi.Common;
using PortfolioApi.DTOs.Article;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Services;

public class ArticleService : IArticleService
{
    private readonly IArticleRepository _articleRepo;
    private readonly IImageService      _imageService;
    private readonly IInteractionRepository _interactionRepo;
    private readonly ILogger<ArticleService> _logger;

    public ArticleService(
        IArticleRepository       articleRepo,
        IImageService            imageService,
        IInteractionRepository   interactionRepo,
        ILogger<ArticleService>  logger)
    {
        _articleRepo     = articleRepo;
        _imageService    = imageService;
        _interactionRepo = interactionRepo;
        _logger          = logger;
    }

    // ── GET ALL (paginated, searchable) ─────────────────────
    public async Task<ApiResponse<PagedResult<ArticleResponseDto>>> GetAllAsync(
        int    page      = 1,
        int    pageSize  = 10,
        bool?  published = true,
        string? tag      = null,
        string? search   = null,
        bool   isAdmin   = false,
        int?   viewerId  = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 50); // max 50 per page for safety

        var (items, total) = await _articleRepo.GetAllAsync(page, pageSize, published, tag, search, isAdmin, viewerId);

        var dtos = items.Select(MapToDto).ToList();
        await EnrichWithInteractionsAsync(dtos);

        var result = new PagedResult<ArticleResponseDto>
        {
            Items      = dtos,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };

        return ApiResponse<PagedResult<ArticleResponseDto>>.Ok(result);
    }

    // ── GET BY ID ────────────────────────────────────────────
    public async Task<ApiResponse<ArticleResponseDto>> GetByIdAsync(int id)
    {
        var article = await _articleRepo.GetByIdAsync(id);
        if (article is null)
            return ApiResponse<ArticleResponseDto>.Fail($"Article {id} not found.", 404);

        var dto = MapToDto(article);
        // Interaction counts are non-critical: never let them break article reads.
        try
        {
            dto.LikeCount = await _interactionRepo.CountLikesAsync(id);
            dto.Reactions = await _interactionRepo.GetReactionCountsAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load interaction counts for article {Id} (tables may be missing).", id);
        }

        return ApiResponse<ArticleResponseDto>.Ok(dto);
    }

    // Batch-fill like/reaction counts for a page of articles in two queries.
    // Wrapped defensively so a missing/locked interactions table can never break
    // the article list — counts simply fall back to zero.
    private async Task EnrichWithInteractionsAsync(List<ArticleResponseDto> dtos)
    {
        if (dtos.Count == 0) return;
        try
        {
            var ids = dtos.Select(d => d.Id).ToList();
            var likeCounts = await _interactionRepo.GetLikeCountsAsync(ids);
            var reactionCounts = await _interactionRepo.GetReactionCountsAsync(ids);
            foreach (var d in dtos)
            {
                d.LikeCount = likeCounts.TryGetValue(d.Id, out var lc) ? lc : 0;
                d.Reactions = reactionCounts.TryGetValue(d.Id, out var rc) ? rc : new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load interaction counts (tables may be missing); returning zero counts.");
        }
    }

    // ── CREATE ───────────────────────────────────────────────
    public async Task<ApiResponse<ArticleResponseDto>> CreateAsync(CreateArticleDto dto, int userId)
    {
        string? imageUrl      = null;
        string? imagePublicId = null;

        if (dto.Image is not null)
        {
            var (url, publicId) = await _imageService.UploadAsync(dto.Image, "portfolio/articles");
            imageUrl      = url;
            imagePublicId = publicId;
        }

        // Sanitise input — strip dangerous HTML tags
        var article = new Article
        {
            Title         = Sanitise(dto.Title),
            Content       = Sanitise(dto.Content),
            Author        = Sanitise(dto.Author),
            Tags          = dto.Tags?.Trim().ToLower(),
            ImageUrl      = imageUrl,
            ImagePublicId = imagePublicId,
            IsPublished   = dto.IsPublished,
            PublishedDate = dto.PublishedDate,
            UserId        = userId,
        };

        await _articleRepo.CreateAsync(article);
        _logger.LogInformation("Article created: {Title} by UserId {UserId}", article.Title, userId);

        return ApiResponse<ArticleResponseDto>.Created(MapToDto(article), "Article created successfully.");
    }

    // ── UPDATE ───────────────────────────────────────────────
    public async Task<ApiResponse<ArticleResponseDto>> UpdateAsync(int id, UpdateArticleDto dto, int userId, bool isAdmin)
    {
        var article = await _articleRepo.GetByIdAsync(id);
        if (article is null)
            return ApiResponse<ArticleResponseDto>.Fail($"Article {id} not found.", 404);

        // Ownership: only the article's author (or an Admin) may edit it.
        if (article.UserId != userId && !isAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to edit article {Id} owned by {OwnerId}", userId, id, article.UserId);
            return ApiResponse<ArticleResponseDto>.Fail("You can only edit your own articles.", 403);
        }

        // Apply partial updates (only fields that were supplied)
        if (dto.Title   is not null) article.Title   = Sanitise(dto.Title);
        if (dto.Content is not null) article.Content = Sanitise(dto.Content);
        if (dto.Author  is not null) article.Author  = Sanitise(dto.Author);
        if (dto.Tags    is not null) article.Tags    = dto.Tags.Trim().ToLower();
        if (dto.IsPublished.HasValue)    article.IsPublished   = dto.IsPublished.Value;
        if (dto.PublishedDate.HasValue)  article.PublishedDate = dto.PublishedDate.Value;

        // Replace image if a new one was uploaded
        if (dto.Image is not null)
        {
            // Delete old image from Cloudinary first
            if (!string.IsNullOrEmpty(article.ImagePublicId))
                await _imageService.DeleteAsync(article.ImagePublicId);

            var (url, publicId) = await _imageService.UploadAsync(dto.Image, "portfolio/articles");
            article.ImageUrl      = url;
            article.ImagePublicId = publicId;
        }

        await _articleRepo.UpdateAsync(article);
        _logger.LogInformation("Article updated: {Id} by UserId {UserId}", id, userId);

        return ApiResponse<ArticleResponseDto>.Ok(MapToDto(article), "Article updated successfully.");
    }

    // ── DELETE ───────────────────────────────────────────────
    public async Task<ApiResponse<object>> DeleteAsync(int id, int userId, bool isAdmin)
    {
        var article = await _articleRepo.GetByIdAsync(id);
        if (article is null)
            return ApiResponse<object>.Fail($"Article {id} not found.", 404);

        // Ownership: only the article's author (or an Admin) may delete it.
        if (article.UserId != userId && !isAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to delete article {Id} owned by {OwnerId}", userId, id, article.UserId);
            return ApiResponse<object>.Fail("You can only delete your own articles.", 403);
        }

        // Remove image from Cloudinary
        if (!string.IsNullOrEmpty(article.ImagePublicId))
            await _imageService.DeleteAsync(article.ImagePublicId);

        await _articleRepo.DeleteAsync(id);
        _logger.LogInformation("Article deleted: {Id} by UserId {UserId}", id, userId);

        return ApiResponse<object>.Ok(new { id }, "Article deleted successfully.");
    }

    // ── Private helpers ─────────────────────────────────────
    private static ArticleResponseDto MapToDto(Article a) => new()
    {
        Id            = a.Id,
        Title         = a.Title,
        Content       = a.Content,
        Author        = a.Author,
        ImageUrl      = a.ImageUrl,
        Tags          = a.Tags,
        IsPublished   = a.IsPublished,
        PublishedDate = a.PublishedDate,
        CreatedAt     = a.CreatedAt,
        UpdatedAt     = a.UpdatedAt,
        AuthorInfo    = a.User is not null
                        ? new ArticleAuthorDto { Id = a.User.Id, Username = a.User.Username }
                        : null,
    };

    /// <summary>
    /// Lightweight XSS protection: remove angle-bracket HTML tags.
    /// For richer sanitisation, add the HtmlAgilityPack or Ganss.Xss library.
    /// </summary>
    private static string Sanitise(string input) =>
        System.Text.RegularExpressions.Regex.Replace(input, @"<[^>]+>", string.Empty).Trim();
}
