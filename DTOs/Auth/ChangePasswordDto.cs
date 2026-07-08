using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Auth;

/// <summary>
/// Payload for an authenticated password change (POST /api/auth/change-password).
/// The user must prove they know the current password before setting a new one.
/// </summary>
public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Must be 8–100 chars with upper, lower, digit, and special character, and
    /// different from the current password. Validated by FluentValidation —
    /// see Validators/ChangePasswordDtoValidator.cs.
    /// </summary>
    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}
