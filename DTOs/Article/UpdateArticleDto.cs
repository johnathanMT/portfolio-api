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
    /// New primary image to replace the existing one. Old Cloudinary asset is deleted.
    /// </summary>
    public IFormFile? Image { get; set; }

    /// <summary>Additional gallery image FILES to upload and append.</summary>
    public List<IFormFile>? GalleryImages { get; set; }

    /// <summary>Additional gallery image URLs to append (already hosted).</summary>
    public List<string>? ImageUrls { get; set; }

    /// <summary>New video file to upload (replaces any existing video).</summary>
    public IFormFile? Video { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? PublishedDate { get; set; }
}
