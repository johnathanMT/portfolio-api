namespace PortfolioApi.Models;

/// <summary>A querent (customer) account — separate from admin Users. Email-only
/// sign-up with email confirmation. Password stored as a BCrypt hash.</summary>
public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;        // lowercase, unique
    public string Username { get; set; } = string.Empty;     // editable by the customer
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string? VerifyToken { get; set; }
    public DateTime? VerifyExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
