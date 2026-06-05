using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.Models;

/// <summary>
/// Represents a registered user. Only Admins can write/manage content.
/// </summary>
public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(255), EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Stored as a BCrypt hash — never the raw password.
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Allowed values: "Admin", "Guest".
    /// </summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "Guest";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
