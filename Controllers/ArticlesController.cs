using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Article;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Full CRUD for blog articles and portfolio posts.
/// Read operations are public. Create requires the Author or Admin role.
/// Update/Delete require the caller to be the article's author, or an Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("general")]
public class ArticlesController : ControllerBase
{
    private readonly IArticleService _articleService;

    public ArticlesController(IArticleService articleService) =>
        _articleService = articleService;

    // ──────────────────────────────────────────────────────────
    /// <summary>Get a paginated list of articles.</summary>
    /// <param name="page">Page number (default 1).</param>
    /// <param name="pageSize">Items per page (max 50, default 10).</param>
    /// <param name="published">Filter: true = published only, false = drafts only, null = all (Admin only).</param>
    /// <param name="tag">Filter by tag slug, e.g. "dotnet".</param>
    /// <param name="search">Full-text search across title, content, and author.</param>
    /// <response code="200">Paged list of articles.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ArticleResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 10,
        [FromQuery] bool?  published = true,
        [FromQuery] string? tag     = null,
        [FromQuery] string? search  = null)
    {
        // Visibility: Admins can filter by published state; Authors additionally see
        // their own drafts; anonymous/Guest visitors see only published articles.
        var isAdmin  = User.IsInRole("Admin");
        var viewerId = GetCurrentUserId();   // 0 if not authenticated

        var result = await _articleService.GetAllAsync(
            page, pageSize, published, tag, search,
            isAdmin,
            viewerId == 0 ? null : viewerId);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Get a single article by ID.</summary>
    /// <response code="200">Article found.</response>
    /// <response code="404">Article not found.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ArticleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _articleService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Create a new article. Requires Admin role.</summary>
    /// <remarks>
    /// Send as <c>multipart/form-data</c> so you can include the image file.
    ///
    ///     POST /api/articles
    ///     Content-Type: multipart/form-data
    ///
    ///     title     = "My Post"
    ///     content   = "Full article content here..."
    ///     author    = "Myo Thant Naing"
    ///     tags      = "dotnet,csharp"
    ///     image     = [file]
    /// </remarks>
    /// <response code="201">Article created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Admin role required.</response>
    [HttpPost]
    [Authorize(Roles = "Admin,Author")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ArticleResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromForm] CreateArticleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _articleService.CreateAsync(dto, userId);
        return result.Success
            ? StatusCode(201, result)
            : BadRequest(result);
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Update an existing article. Requires Admin role.</summary>
    /// <remarks>All fields are optional — only supplied fields are changed.</remarks>
    /// <response code="200">Article updated.</response>
    /// <response code="404">Article not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Author")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ArticleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateArticleDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _articleService.UpdateAsync(id, dto, userId, User.IsInRole("Admin"));
        return result.StatusCode switch
        {
            200 => Ok(result),
            404 => NotFound(result),
            403 => StatusCode(403, result),
            _   => BadRequest(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Delete an article. Requires Admin role.</summary>
    /// <response code="200">Article deleted.</response>
    /// <response code="404">Article not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Author")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _articleService.DeleteAsync(id, userId, User.IsInRole("Admin"));
        return result.StatusCode switch
        {
            200 => Ok(result),
            404 => NotFound(result),
            403 => StatusCode(403, result),
            _   => BadRequest(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Delete one gallery image. Author (owner) or Admin only.</summary>
    [HttpDelete("{articleId:int}/images/{imageId:int}")]
    [Authorize(Roles = "Admin,Author")]
    public async Task<IActionResult> DeleteImage(int articleId, int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _articleService.DeleteImageAsync(imageId, userId, User.IsInRole("Admin"));
        return result.StatusCode switch
        {
            200 => Ok(result),
            404 => NotFound(result),
            403 => StatusCode(403, result),
            _   => BadRequest(result),
        };
    }

    /// <summary>Reorder an article's gallery images by id. Author (owner) or Admin only.</summary>
    [HttpPut("{articleId:int}/images/reorder")]
    [Authorize(Roles = "Admin,Author")]
    public async Task<IActionResult> ReorderImages(int articleId, [FromBody] ReorderImagesDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _articleService.ReorderImagesAsync(articleId, dto.ImageIds, userId, User.IsInRole("Admin"));
        return result.StatusCode switch
        {
            200 => Ok(result),
            404 => NotFound(result),
            403 => StatusCode(403, result),
            _   => BadRequest(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return int.TryParse(claim, out var id) ? id : 0;
    }

    private ApiResponse<object> BuildValidationError() =>
        ApiResponse<object>.Fail(
            "Validation failed.",
            400,
            ModelState.Values
                      .SelectMany(v => v.Errors)
                      .Select(e => e.ErrorMessage)
                      .ToList());
}
