using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioApi.Models;

/// <summary>
/// An anonymous "quick reaction" on an article. The <see cref="Reaction"/> value
/// must be one of a fixed, server-validated set (no free text) — this is the core
/// anti-spam guarantee. One of each reaction per visitor.
/// </summary>
public class ArticleReaction
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Article))]
    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>One of: love, clap, fire, idea, great, inspiring, helpful.</summary>
    [Required, MaxLength(20)]
    public string Reaction { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
