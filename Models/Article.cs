using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioApi.Models;

/// <summary>
/// Represents a blog article / portfolio post.
/// </summary>
public class Article
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full article body. Stored as longtext in MySQL (no length cap).
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(150)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Secure URL returned by Cloudinary after upload.
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Cloudinary Public ID — needed to delete the old image on update.
    /// </summary>
    [MaxLength(300)]
    public string? ImagePublicId { get; set; }

    /// <summary>
    /// Comma-separated tags, e.g. "dotnet,csharp,api".
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    // Navigation
    public User? User { get; set; }

    /// <summary>Gallery images (1:N). The primary <see cref="ImageUrl"/> is the hero.</summary>
    public ICollection<ArticleImage> Images { get; set; } = new List<ArticleImage>();
}
