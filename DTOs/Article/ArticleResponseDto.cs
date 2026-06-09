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

    /// <summary>Primary/hero image (kept for backward compatibility).</summary>
    public string?   ImageUrl      { get; set; }

    /// <summary>All images as plain URLs: primary first, then gallery in order (public consumers).</summary>
    public List<string> ImageUrls   { get; set; } = new();

    /// <summary>Gallery images with ids — for management UIs (delete / reorder).</summary>
    public List<ArticleImageDto> Images { get; set; } = new();

    /// <summary>Optional article video (Cloudinary secure URL).</summary>
    public string?   VideoUrl      { get; set; }

    public string?   Tags          { get; set; }
    public bool      IsPublished   { get; set; }
    public DateTime  PublishedDate { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  UpdatedAt     { get; set; }

    /// <summary>Anonymous interaction counts (populated on read).</summary>
    public int                     LikeCount { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = new();

    /// <summary>Minimal author info — does NOT expose password hash or email.</summary>
    public ArticleAuthorDto? AuthorInfo { get; set; }
}

public class ArticleAuthorDto
{
    public int    Id       { get; set; }
    public string Username { get; set; } = string.Empty;
}

/// <summary>A single gallery image with its id (for delete/reorder).</summary>
public class ArticleImageDto
{
    public int    Id        { get; set; }
    public string ImageUrl  { get; set; } = string.Empty;
    public int    SortOrder { get; set; }
}

/// <summary>Body for reordering an article's gallery images.</summary>
public class ReorderImagesDto
{
    public List<int> ImageIds { get; set; } = new();
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
