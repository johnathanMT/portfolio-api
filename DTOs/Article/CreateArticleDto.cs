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
    /// Optional image file. Uploaded to Cloudinary; URL stored in the DB.
    /// </summary>
    public IFormFile? Image { get; set; }

    /// <summary>
    /// Comma-separated tags, e.g. "dotnet,api,tutorial".
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
}
