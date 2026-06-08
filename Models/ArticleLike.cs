using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioApi.Models;

/// <summary>
/// An anonymous "like" on an article. No user account required — a hashed
/// per-visitor token enforces one like per visitor and enables un-liking.
/// </summary>
public class ArticleLike
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Article))]
    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>SHA-256 hash of (salt + visitor token / IP). Never the raw value.</summary>
    [Required, MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
