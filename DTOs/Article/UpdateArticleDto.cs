using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Article;

public class UpdateArticleDto
{
    [MaxLength(300)]
    public string? Title { get; set; }

    [MinLength(10)]
    public string? Content { get; set; }

    [MaxLength(150)]
    public string? Author { get; set; }

    /// <summary>
    /// New image to replace the existing one. Old Cloudinary asset is deleted.
    /// </summary>
    public IFormFile? Image { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? PublishedDate { get; set; }
}
