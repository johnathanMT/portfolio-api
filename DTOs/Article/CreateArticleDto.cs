using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Article;

public class CreateArticleDto
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MinLength(10)]
    public string Content { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Optional primary/hero image file. Uploaded to Cloudinary; URL stored in the DB.
    /// </summary>
    public IFormFile? Image { get; set; }

    /// <summary>
    /// Optional gallery image FILES (multiple). Each is uploaded to Cloudinary
    /// and stored as an ArticleImage row. Send repeated "GalleryImages" form fields.
    /// </summary>
    public List<IFormFile>? GalleryImages { get; set; }

    /// <summary>
    /// Optional gallery image URLs (already hosted). Also stored as ArticleImage rows.
    /// </summary>
    public List<string>? ImageUrls { get; set; }

    /// <summary>Optional video file (e.g. .mp4). Uploaded to Cloudinary.</summary>
    public IFormFile? Video { get; set; }

    /// <summary>
    /// Comma-separated tags, e.g. "dotnet,api,tutorial".
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
}
