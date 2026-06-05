namespace PortfolioApi.DTOs.Article;

/// <summary>
/// Read-only projection returned to clients. Never exposes DB internals.
/// </summary>
public class ArticleResponseDto
{
    public int       Id            { get; set; }
    public string    Title         { get; set; } = string.Empty;
    public string    Content       { get; set; } = string.Empty;
    public string    Author        { get; set; } = string.Empty;
    public string?   ImageUrl      { get; set; }
    public string?   Tags          { get; set; }
    public bool      IsPublished   { get; set; }
    public DateTime  PublishedDate { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  UpdatedAt     { get; set; }

    /// <summary>Minimal author info — does NOT expose password hash or email.</summary>
    public ArticleAuthorDto? AuthorInfo { get; set; }
}

public class ArticleAuthorDto
{
    public int    Id       { get; set; }
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Paged list wrapper returned by GET /articles.
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items      { get; set; } = Enumerable.Empty<T>();
    public int            TotalCount { get; set; }
    public int            Page       { get; set; }
    public int            PageSize   { get; set; }
    public int            TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool           HasNext    => Page < TotalPages;
    public bool           HasPrev    => Page > 1;
}
