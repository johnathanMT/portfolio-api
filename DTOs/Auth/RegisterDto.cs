using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Auth;

public class RegisterDto
{
    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Must be 8-100 chars, contain upper, lower, digit, and special character.
    /// Validated by FluentValidation — see Validators/RegisterDtoValidator.cs.
    /// </summary>
    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Secret key required to register as Admin. Set this in appsettings.json.
    /// If omitted, user is registered as Guest.
    /// </summary>
    public string? AdminSecret { get; set; }
}
