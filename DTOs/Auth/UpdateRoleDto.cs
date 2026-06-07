using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Auth;

/// <summary>
/// Request body for changing a user's role (Admin-only operation).
/// Allowed values: "Guest", "Author", "Admin".
/// </summary>
public class UpdateRoleDto
{
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;
}
