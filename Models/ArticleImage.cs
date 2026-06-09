using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioApi.Models;

/// <summary>
/// An additional image belonging to an article (gallery). One Article has many
/// ArticleImages; each ArticleImage belongs to exactly one Article (1:N).
/// </summary>
public class ArticleImage
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Article))]
    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    [Required, MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Cloudinary public ID (if uploaded as a file) — used to delete the asset.</summary>
    [MaxLength(300)]
    public string? ImagePublicId { get; set; }

    /// <summary>Display order within the gallery (ascending).</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
