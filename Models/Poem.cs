using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.Models;

/// <summary>
/// A "techno-science" poem shown in the homepage flip-book. Public to read; only
/// an Admin (JWT, Role=Admin) may create / edit / delete — see PoetryController.
/// </summary>
public class Poem
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional small header line (e.g. "log_001 · ai"). May be empty.</summary>
    [MaxLength(80)]
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>The poem body. Newlines are preserved (stored as MySQL LONGTEXT).</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
